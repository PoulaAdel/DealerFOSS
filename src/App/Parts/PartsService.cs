// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   PartsService — the catalogue, the stock on each shelf, and what a part costs.
//
// Usage:
//   Through IParts.
//
// Coding Instructions:
//   Three things here are load-bearing.
//
//   Quantity is always SUM(RemainingQuantity) over the layers. There is no
//   stored total to drift from it. If that ever becomes a performance problem
//   the fix is an index or a projection, not a denormalized column somebody
//   has to remember to keep in step.
//
//   IssueAsync does NOT save. It is called from inside RepairOrders' invoice
//   transaction, and saving here would commit the stock movement separately
//   from the invoice and the ledger entry — which is exactly the disagreement
//   the whole design is trying to prevent.
//
//   Stock cannot go negative. A workshop that can sell parts it does not have
//   does not have a stock figure at all, and the refusal is what makes
//   somebody go and book the delivery in.

using Microsoft.EntityFrameworkCore;
using DealerFOSS.Core;
using DealerFOSS.Data;
using DealerFOSS.Identity;

namespace DealerFOSS.Parts;

public sealed class PartsService(
    TenantDb db,
    IAccessDirectory access,
    ICurrentUser currentUser,
    IAuditSink audit,
    IClock clock)
    : IParts
{
    private const string ReadPermission = Permissions.PartsRead;
    private const string ManagePermission = Permissions.PartsManage;

    private const int MaxResults = 200;

    private readonly TenantDb _db = db;
    private readonly IAccessDirectory _access = access;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditSink _audit = audit;
    private readonly IClock _clock = clock;

    public async Task<Result<IReadOnlyList<PartSummary>>> ListAsync(
        PartQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<IReadOnlyList<PartSummary>>(PartErrors.Forbidden);
        }

        if (query.RooftopId is { } requested && !scope.Covers(requested))
        {
            return Result.Failure<IReadOnlyList<PartSummary>>(PartErrors.Forbidden);
        }

        var method = await MethodAsync(cancellationToken);
        var take = Math.Clamp(query.Limit <= 0 ? 100 : query.Limit, 1, MaxResults);

        var parts = _db.Parts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Matched against the normalized number so "MZ-690411", "mz690411",
            // and "MZ 690411" all find the same part, and against the description
            // because half the time nobody has the number.
            var normalized = Part.Normalize(query.Search);
            var text = query.Search.Trim();

            parts = parts.Where(p =>
                p.PartNumber.Contains(normalized) || p.Description.Contains(text));
        }

        var rows = await parts
            .OrderBy(p => p.PartNumber)
            .Take(take)
            .ToListAsync(cancellationToken);

        var partIds = rows.Select(p => p.Id).ToList();

        var layerQuery = _db.StockReceipts.AsNoTracking().Where(r => partIds.Contains(r.PartId));
        layerQuery = Restrict(layerQuery, scope, query.RooftopId);

        var layers = await layerQuery.ToListAsync(cancellationToken);

        var summaries = new List<PartSummary>();

        foreach (var part in rows)
        {
            // One row per rooftop that has ever held the part: two lots holding
            // the same number hold two different piles of it.
            var byRooftop = layers
                .Where(l => l.PartId == part.Id)
                .GroupBy(l => l.RooftopId)
                .ToList();

            if (byRooftop.Count == 0)
            {
                // Never stocked anywhere in scope. It still belongs on the list:
                // a part nobody can see is a part nobody can book stock onto.
                if (!query.InStockOnly)
                {
                    summaries.Add(new PartSummary(
                        part.Id, part.PartNumber, part.Description, null, 0m, 0m, "USD"));
                }

                continue;
            }

            foreach (var group in byRooftop)
            {
                var onHand = group.Sum(l => l.RemainingQuantity);
                if (query.InStockOnly && onHand <= 0)
                {
                    continue;
                }

                var shelf = group.ToList();

                summaries.Add(new PartSummary(
                    part.Id,
                    part.PartNumber,
                    part.Description,
                    group.Key,
                    onHand,
                    UnitCost(method, shelf),
                    shelf[0].UnitCostCurrency));
            }
        }

        return Result.Success<IReadOnlyList<PartSummary>>(
            summaries.OrderBy(s => s.PartNumber, StringComparer.Ordinal).ToList());
    }

    public async Task<Result<PartDetail>> GetAsync(Guid partId, CancellationToken cancellationToken)
    {
        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<PartDetail>(PartErrors.Forbidden);
        }

        var part = await _db.Parts.AsNoTracking().SingleOrDefaultAsync(p => p.Id == partId, cancellationToken);
        if (part is null)
        {
            return Result.Failure<PartDetail>(PartErrors.NotFound);
        }

        return Result.Success(await DescribeAsync(part, scope, cancellationToken));
    }

    public async Task<Result<PartDetail>> AddAsync(NewPart part, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(part);

        // Adding to the catalogue is an organization-level act: the number means
        // the same thing at every lot, so one lot must not be able to define it
        // differently from another.
        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ManagePermission, cancellationToken);
        if (!scope.IsOrganizationWide)
        {
            await DenyAsync(ManagePermission, "Attempted to add a part to the catalogue.", null, cancellationToken);
            return Result.Failure<PartDetail>(PartErrors.CatalogueForbidden);
        }

        var number = Part.Normalize(part.PartNumber ?? string.Empty);

        if (await _db.Parts.AnyAsync(p => p.PartNumber == number, cancellationToken))
        {
            return Result.Failure<PartDetail>(PartErrors.NumberTaken);
        }

        Part created;
        try
        {
            created = new Part(Guid.NewGuid(), part.PartNumber ?? string.Empty, part.Description ?? string.Empty);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<PartDetail>(Error.Validation("parts.invalid", ex.Message));
        }

        _db.Parts.Add(created);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(await DescribeAsync(created, scope, cancellationToken));
    }

    public async Task<Result<PartDetail>> ReceiveAsync(
        Guid partId,
        StockDelivery delivery,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        // Booking stock in is a rooftop act, checked at that rooftop — the stock
        // lands on that shelf and nobody else's.
        if (!await _access.IsAuthorizedAsync(_currentUser.Id, ManagePermission, delivery.RooftopId, cancellationToken))
        {
            await DenyAsync(ManagePermission, "Attempted to book parts in.", delivery.RooftopId, cancellationToken);
            return Result.Failure<PartDetail>(PartErrors.Forbidden);
        }

        var part = await _db.Parts.AsNoTracking().SingleOrDefaultAsync(p => p.Id == partId, cancellationToken);
        if (part is null)
        {
            return Result.Failure<PartDetail>(PartErrors.NotFound);
        }

        StockReceipt receipt;
        try
        {
            receipt = new StockReceipt(
                Guid.NewGuid(),
                partId,
                delivery.RooftopId,
                delivery.Quantity,
                new Money(delivery.UnitCost, delivery.Currency),
                _clock.UtcNow,
                delivery.Reference);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<PartDetail>(Error.Validation("parts.invalid_delivery", ex.Message));
        }

        _db.StockReceipts.Add(receipt);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(
                _currentUser.Id, "Parts.Received", AuditOutcome.Allowed, "Part", partId.ToString(),
                delivery.RooftopId.Value,
                $"Booked in {delivery.Quantity} × {part.PartNumber} at {delivery.UnitCost:0.00} each.",
                null, null),
            cancellationToken);

        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        return Result.Success(await DescribeAsync(part, scope, cancellationToken));
    }

    public async Task<Result<IssuedParts>> IssueAsync(
        IReadOnlyList<PartIssue> issues,
        RooftopId rooftopId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(issues);

        if (issues.Count == 0)
        {
            return Result.Success(new IssuedParts(0m, "USD", []));
        }

        var method = await MethodAsync(cancellationToken);
        var wanted = issues.Select(i => i.PartId).Distinct().ToList();

        // Tracked, because this consumes the layers and the caller's SaveChanges
        // is what commits it.
        var layers = await _db.StockReceipts
            .Where(r => wanted.Contains(r.PartId) && r.RooftopId == rooftopId)
            .ToListAsync(cancellationToken);

        var parts = await _db.Parts
            .AsNoTracking()
            .Where(p => wanted.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.PartNumber, cancellationToken);

        var issued = new List<IssuedPart>();
        var total = 0m;
        var currency = "USD";

        foreach (var issue in issues)
        {
            var shelf = layers.Where(l => l.PartId == issue.PartId).ToList();

            var cost = PartsCosting.CostOf(method, shelf, issue.Quantity);
            if (cost is null)
            {
                var number = parts.TryGetValue(issue.PartId, out var known) ? known : "that part";
                var available = shelf.Sum(l => l.RemainingQuantity);

                return Result.Failure<IssuedParts>(Error.Conflict(
                    "parts.not_enough_stock",
                    $"There are {available} of {number} on this location's shelf and the job needs "
                        + $"{issue.Quantity}. Book the delivery in first."));
            }

            if (shelf.Count > 0)
            {
                currency = shelf[0].UnitCostCurrency;
            }

            // Layers are always consumed oldest-first, whatever method priced the
            // sale. Which physical box leaves the shelf is not a costing opinion.
            var outstanding = issue.Quantity;
            foreach (var layer in PartsCosting.Oldest(shelf))
            {
                if (outstanding <= 0)
                {
                    break;
                }

                outstanding -= layer.Take(outstanding);
            }

            total += cost.Value;
            issued.Add(new IssuedPart(
                issue.PartId,
                parts.TryGetValue(issue.PartId, out var name) ? name : string.Empty,
                issue.Quantity,
                cost.Value));
        }

        return Result.Success(new IssuedParts(total, currency, issued));
    }

    public async Task<Result<PartsCostingSetting>> GetCostingMethodAsync(CancellationToken cancellationToken)
    {
        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<PartsCostingSetting>(PartErrors.Forbidden);
        }

        return Result.Success(Describe(await MethodAsync(cancellationToken)));
    }

    public async Task<Result<PartsCostingSetting>> SetCostingMethodAsync(
        PartsCostingMethod method,
        CancellationToken cancellationToken)
    {
        // How the whole organization values its stock is not a decision one lot
        // makes, the same reasoning as the second-factor policy.
        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ManagePermission, cancellationToken);
        if (!scope.IsOrganizationWide)
        {
            await DenyAsync(ManagePermission, "Attempted to change the parts costing method.", null, cancellationToken);
            return Result.Failure<PartsCostingSetting>(PartErrors.CostingForbidden);
        }

        var settings = await _db.PartsSettings.SingleOrDefaultAsync(
            s => s.Id == PartsSettings.SingletonId, cancellationToken);

        var before = settings?.CostingMethod ?? PartsCostingMethod.MovingAverage;

        if (settings is null)
        {
            settings = new PartsSettings(method);
            _db.PartsSettings.Add(settings);
        }
        else
        {
            settings.Use(method);
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (before != method)
        {
            await _audit.RecordAsync(
                new AuditEntry(
                    _currentUser.Id, "Parts.CostingMethodChanged", AuditOutcome.Allowed,
                    "PartsSettings", PartsSettings.SingletonId.ToString(), null,
                    $"Parts costing changed from {before} to {method}. Applies to sales from now on; "
                        + "already-invoiced work keeps the cost it was sold at.",
                    null, null),
                cancellationToken);
        }

        return Result.Success(Describe(method));
    }

    // --- helpers -----------------------------------------------------------

    private async Task<PartsCostingMethod> MethodAsync(CancellationToken cancellationToken)
    {
        var settings = await _db.PartsSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == PartsSettings.SingletonId, cancellationToken);

        // Absent means nobody has chosen, which is the default rather than an
        // error — an organization should not have to configure parts before it
        // can use them.
        return settings?.CostingMethod ?? PartsCostingMethod.MovingAverage;
    }

    private static decimal UnitCost(PartsCostingMethod method, IReadOnlyList<StockReceipt> shelf) =>
        method switch
        {
            PartsCostingMethod.LastCost => PartsCosting.LastCostOf(shelf),
            // FIFO's next-one-out is the oldest layer's cost. Shown as the unit
            // cost because that is what selling one right now would record.
            PartsCostingMethod.Fifo => PartsCosting.Oldest(shelf).FirstOrDefault()?.UnitCostAmount
                ?? PartsCosting.LastCostOf(shelf),
            _ => PartsCosting.MovingAverageOf(shelf),
        };

    private async Task<PartDetail> DescribeAsync(
        Part part,
        AuthorizedScope scope,
        CancellationToken cancellationToken)
    {
        var method = await MethodAsync(cancellationToken);

        var layers = await Restrict(
                _db.StockReceipts.AsNoTracking().Where(r => r.PartId == part.Id), scope, rooftopId: null)
            .ToListAsync(cancellationToken);

        var stock = layers
            .GroupBy(l => l.RooftopId)
            .Select(group =>
            {
                var shelf = group.ToList();

                return new PartStockAtRooftop(
                    group.Key,
                    shelf.Sum(l => l.RemainingQuantity),
                    UnitCost(method, shelf),
                    shelf[0].UnitCostCurrency,
                    shelf
                        .OrderByDescending(l => l.ReceivedAt)
                        .Select(l => new StockLayerView(
                            l.Id, l.QuantityReceived, l.RemainingQuantity,
                            l.UnitCostAmount, l.ReceivedAt, l.Reference))
                        .ToList());
            })
            .ToList();

        return new PartDetail(part.Id, part.PartNumber, part.Description, method.ToString(), stock);
    }

    /// <summary>
    /// Applies the rooftop boundary to a layer query. Filtering the query rather
    /// than the results means another location's stock is never read at all.
    /// </summary>
    private static IQueryable<StockReceipt> Restrict(
        IQueryable<StockReceipt> layers,
        AuthorizedScope scope,
        RooftopId? rooftopId)
    {
        if (rooftopId is { } one)
        {
            return layers.Where(r => r.RooftopId == one);
        }

        if (scope.IsOrganizationWide)
        {
            return layers;
        }

        var allowed = scope.Rooftops.ToList();
        return layers.Where(r => allowed.Contains(r.RooftopId));
    }

    private static PartsCostingSetting Describe(PartsCostingMethod method) =>
        new(
            method.ToString(),
            [
                new PartsCostingOption(
                    nameof(PartsCostingMethod.MovingAverage),
                    "Moving average",
                    "The average cost of what is on the shelf, recalculated as deliveries arrive. "
                        + "Smooths supplier price changes, and what most dealer systems use."),
                new PartsCostingOption(
                    nameof(PartsCostingMethod.LastCost),
                    "Last cost paid",
                    "Whatever the most recent delivery cost. Simple to explain, and one unusual "
                        + "purchase price distorts every sale after it."),
                new PartsCostingOption(
                    nameof(PartsCostingMethod.Fifo),
                    "Oldest stock first (FIFO)",
                    "Costs each sale against the oldest delivery still on the shelf. The most "
                        + "faithful to what physically leaves."),
            ]);

    private Task DenyAsync(string permission, string what, RooftopId? rooftopId, CancellationToken cancellationToken) =>
        _audit.RecordAsync(
            AuditEntry.Denied(_currentUser.Id, permission, "Part", string.Empty, rooftopId?.Value, what),
            cancellationToken);
}

/// <summary>Stable error codes for the Parts capability (doc 06 §6).</summary>
internal static class PartErrors
{
    public static Error Forbidden { get; } = Error.Forbidden(
        "parts.forbidden",
        "You do not have access to parts at this location.");

    public static Error CatalogueForbidden { get; } = Error.Forbidden(
        "parts.catalogue_forbidden",
        "Adding a part to the catalogue needs organization-wide permission — the number means "
            + "the same thing at every location.");

    public static Error CostingForbidden { get; } = Error.Forbidden(
        "parts.costing_forbidden",
        "Changing how parts are costed needs organization-wide permission.");

    public static Error NotFound { get; } = Error.NotFound(
        "parts.not_found",
        "There is no part with that id.");

    public static Error NumberTaken { get; } = Error.Conflict(
        "parts.number_taken",
        "That part number is already in the catalogue.");
}
