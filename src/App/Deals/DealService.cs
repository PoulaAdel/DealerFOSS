// DealService — working a deal, and the two controls that make it safe.
//
// Use:  through IDeals.
// Edit: three things here are load-bearing.
//
//       The rooftop scope, as everywhere else: every read is filtered in the
//       query and every write authorizes the rooftop first.
//
//       Writing a deal and approving one are separate permissions. A salesperson
//       builds the numbers; somebody holding Deals.Approve signs them off. That
//       split is the point of the capability, not decoration.
//
//       Starting a deal HOLDS the car and cancelling releases it, both through
//       IInventory. Because the deal and the hold must never disagree, they are
//       committed in one transaction — the same TenantDb instance is shared by
//       both services within a request, so a single transaction covers both.

using Microsoft.EntityFrameworkCore;
using OpenDealer360.Accounting;
using OpenDealer360.Core;
using OpenDealer360.Customers;
using OpenDealer360.Data;
using OpenDealer360.Identity;
using OpenDealer360.Inventory;

namespace OpenDealer360.Deals;

public sealed class DealService(
    TenantDb db,
    IAccessDirectory access,
    ICustomers customers,
    IInventory inventory,
    IAccounting accounting,
    ICurrentUser currentUser,
    IAuditSink audit,
    IClock clock)
    : IDeals
{
    private const string ReadPermission = Permissions.DealsRead;
    private const string WritePermission = Permissions.DealsWrite;
    private const string ApprovePermission = Permissions.DealsApprove;

    /// <summary>The only status a car can be in when a deal starts on it.</summary>
    private const string SellableStatus = "Available";

    private const int MaxResults = 200;

    private readonly TenantDb _db = db;
    private readonly IAccessDirectory _access = access;
    private readonly ICustomers _customers = customers;
    private readonly IInventory _inventory = inventory;
    private readonly IAccounting _accounting = accounting;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditSink _audit = audit;
    private readonly IClock _clock = clock;

    public async Task<Result<IReadOnlyList<DealSummary>>> ListAsync(
        DealQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<IReadOnlyList<DealSummary>>(DealErrors.Forbidden);
        }

        if (query.RooftopId is { } requested && !scope.Covers(requested))
        {
            return Result.Failure<IReadOnlyList<DealSummary>>(DealErrors.Forbidden);
        }

        DealStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse(query.Status, ignoreCase: true, out DealStatus parsed))
            {
                return Result.Failure<IReadOnlyList<DealSummary>>(DealErrors.UnknownStatus);
            }

            status = parsed;
        }

        var take = Math.Clamp(query.Limit <= 0 ? 50 : query.Limit, 1, MaxResults);
        var deals = _db.Deals.AsNoTracking();

        if (!scope.IsOrganizationWide)
        {
            var allowed = scope.Rooftops.ToList();
            deals = deals.Where(d => allowed.Contains(d.RooftopId));
        }

        if (query.RooftopId is { } only)
        {
            deals = deals.Where(d => d.RooftopId == only);
        }

        if (status is { } wanted)
        {
            deals = deals.Where(d => d.Status == wanted);
        }

        if (query.CustomerId is { } customer)
        {
            deals = deals.Where(d => d.CustomerId == customer);
        }

        if (query.SalespersonUserId is { } salesperson)
        {
            deals = deals.Where(d => d.SalespersonUserId == salesperson);
        }

        if (query.OpenOnly)
        {
            deals = deals.Where(d => d.Status != DealStatus.Delivered && d.Status != DealStatus.Cancelled);
        }

        var rows = await deals
            .Include(d => d.Charges)
            .OrderByDescending(d => d.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        var context = await LookupAsync(rows, cancellationToken);
        if (context.IsFailure)
        {
            return Result.Failure<IReadOnlyList<DealSummary>>(context.Error);
        }

        return Result.Success<IReadOnlyList<DealSummary>>(
            rows.Select(d =>
            {
                var unit = context.Value.Unit(d.InventoryUnitId);
                return new DealSummary(
                    d.Id,
                    d.RooftopId,
                    d.Status.ToString(),
                    d.CustomerId,
                    context.Value.CustomerName(d.CustomerId),
                    d.InventoryUnitId,
                    unit is null ? "(not on file)" : unit.StockNumber,
                    unit is null ? "(not on file)" : unit.VehicleDisplayName,
                    d.AmountDue.Amount,
                    d.Currency,
                    d.SalespersonUserId,
                    d.ApprovedByUserId is not null);
            }).ToList());
    }

    public async Task<Result<DealDetail>> GetAsync(Guid dealId, CancellationToken cancellationToken)
    {
        var deal = await LoadAsync(dealId, tracked: false, cancellationToken);

        // Unknown and unauthorized answer identically, so a caller cannot probe
        // for deals belonging to a rooftop they may not see.
        if (deal is null)
        {
            return Result.Failure<DealDetail>(DealErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, ReadPermission, deal.RooftopId, cancellationToken))
        {
            return Result.Failure<DealDetail>(DealErrors.Forbidden);
        }

        return await DescribeAsync(deal, cancellationToken);
    }

    public async Task<Result<DealDetail>> StartAsync(NewDeal deal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deal);

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, WritePermission, deal.RooftopId, cancellationToken))
        {
            return Result.Failure<DealDetail>(DealErrors.Forbidden);
        }

        var customer = await _customers.GetAsync(deal.CustomerId, cancellationToken);
        if (customer.IsFailure)
        {
            return Result.Failure<DealDetail>(
                customer.Error.Type == ErrorType.NotFound ? DealErrors.CustomerNotFound : customer.Error);
        }

        var unit = await _inventory.GetAsync(deal.InventoryUnitId, cancellationToken);
        if (unit.IsFailure)
        {
            return Result.Failure<DealDetail>(DealErrors.UnitNotAvailable);
        }

        if (unit.Value.RooftopId != deal.RooftopId)
        {
            return Result.Failure<DealDetail>(DealErrors.UnitAtAnotherRooftop);
        }

        // The car must be on the lot and unspoken for. This is what stops the same
        // car being sold twice — the second deal finds it OnHold and is refused.
        if (!string.Equals(unit.Value.Status, SellableStatus, StringComparison.Ordinal))
        {
            return Result.Failure<DealDetail>(DealErrors.UnitNotAvailable);
        }

        Deal started;
        try
        {
            started = Deal.Start(
                Guid.NewGuid(),
                deal.RooftopId,
                deal.CustomerId,
                deal.InventoryUnitId,
                deal.Currency,
                _clock.UtcNow,
                deal.SalespersonUserId ?? _currentUser.Id,
                deal.LeadId);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<DealDetail>(Error.Validation("deals.invalid", ex.Message));
        }

        // The deal and the hold on the car must never disagree, so they commit
        // together or not at all.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var held = await _inventory.ChangeStatusAsync(
            deal.InventoryUnitId,
            new StatusChangeRequest("OnHold", $"Held for a deal at {DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime):yyyy-MM-dd}."),
            cancellationToken);

        if (held.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<DealDetail>(held.Error);
        }

        _db.Deals.Add(started);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, WritePermission, AuditOutcome.Allowed,
                "Deal", started.Id.ToString(), deal.RooftopId.Value, "Started", null, null),
            cancellationToken);

        return await DescribeAsync(started, cancellationToken);
    }

    public async Task<Result<DealDetail>> SetTermsAsync(
        Guid dealId,
        DealTerms terms,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(terms);

        var deal = await LoadAsync(dealId, tracked: true, cancellationToken);
        if (deal is null)
        {
            return Result.Failure<DealDetail>(DealErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, WritePermission, deal.RooftopId, cancellationToken))
        {
            return Result.Failure<DealDetail>(DealErrors.Forbidden);
        }

        var charges = new List<(ChargeKind, string, decimal)>();
        foreach (var charge in terms.Charges ?? [])
        {
            if (!Enum.TryParse<ChargeKind>(charge.Kind, ignoreCase: true, out var kind))
            {
                return Result.Failure<DealDetail>(DealErrors.UnknownChargeKind);
            }

            charges.Add((kind, charge.Description, charge.Amount));
        }

        try
        {
            TradeIn? trade = null;
            if (terms.TradeIn is { } t)
            {
                trade = TradeIn.Create(t.Description, t.Allowance, t.Payoff);
            }

            deal.SetTerms(charges, trade);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<DealDetail>(Error.Validation("deals.invalid_terms", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            // Editing a frozen deal is an ordinary refusal with a readable reason.
            return Result.Failure<DealDetail>(Error.Conflict("deals.terms_frozen", ex.Message));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await DescribeAsync(deal, cancellationToken);
    }

    public async Task<Result<DealDetail>> ChangeStatusAsync(
        Guid dealId,
        DealStatusChangeRequest change,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);

        var deal = await LoadAsync(dealId, tracked: true, cancellationToken);
        if (deal is null)
        {
            return Result.Failure<DealDetail>(DealErrors.Forbidden);
        }

        if (!Enum.TryParse(change.Status, ignoreCase: true, out DealStatus next))
        {
            return Result.Failure<DealDetail>(DealErrors.UnknownStatus);
        }

        // Signing a deal off is a different right from building one. A salesperson
        // who could approve their own numbers is not a control at all.
        var required = next == DealStatus.Approved ? ApprovePermission : WritePermission;
        if (!await _access.IsAuthorizedAsync(_currentUser.Id, required, deal.RooftopId, cancellationToken))
        {
            return Result.Failure<DealDetail>(
                next == DealStatus.Approved ? DealErrors.ApprovalForbidden : DealErrors.Forbidden);
        }

        var from = deal.Status;

        // Read the cost before the car moves — a delivered unit still has to
        // report what it cost, and the ledger needs it to show gross profit.
        decimal vehicleCost = 0m;
        if (next == DealStatus.Delivered)
        {
            var unit = await _inventory.GetAsync(deal.InventoryUnitId, cancellationToken);
            if (unit.IsFailure)
            {
                return Result.Failure<DealDetail>(unit.Error);
            }

            vehicleCost = unit.Value.CostAmount ?? 0m;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            deal.ChangeStatus(next, _clock.UtcNow, _currentUser.Id, change.Note);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<DealDetail>(Error.Conflict("deals.status_not_allowed", ex.Message));
        }

        // The car follows the deal: delivered means sold, cancelled means it goes
        // back on the lot for somebody else.
        var carMove = next switch
        {
            DealStatus.Delivered => "Sold",
            DealStatus.Cancelled => "Available",
            _ => null,
        };

        if (carMove is not null)
        {
            var moved = await _inventory.ChangeStatusAsync(
                deal.InventoryUnitId,
                new StatusChangeRequest(carMove, $"Deal {next.ToString().ToLowerInvariant()}."),
                cancellationToken);

            if (moved.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<DealDetail>(moved.Error);
            }
        }

        // A car leaving the lot is an accounting event. It posts inside the same
        // transaction as the delivery, so the ledger and the deal can never
        // disagree about whether the sale happened.
        if (next == DealStatus.Delivered)
        {
            var posted = await _accounting.PostDeliveryAsync(
                BuildPosting(deal, vehicleCost), cancellationToken);

            if (posted.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<DealDetail>(posted.Error);
            }
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<DealDetail>(DealErrors.ChangedElsewhere);
        }

        await transaction.CommitAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, required, AuditOutcome.Allowed,
                "Deal", deal.Id.ToString(), deal.RooftopId.Value, $"{from} to {next}", null, null),
            cancellationToken);

        return await DescribeAsync(deal, cancellationToken);
    }

    /// <summary>
    /// Restates the deal in the terms the ledger needs, so Accounting never has
    /// to know what a charge kind is and Deals never has to know what an account
    /// is.
    /// </summary>
    private static DeliveryPosting BuildPosting(Deal deal, decimal vehicleCost) =>
        new(
            deal.RooftopId,
            deal.Id.ToString(),
            deal.Currency,
            VehiclePrice: deal.Charges.Where(c => c.Kind == ChargeKind.VehiclePrice).Sum(c => c.Amount),
            Fees: deal.Charges
                .Where(c => c.Kind is ChargeKind.Fee or ChargeKind.Accessory)
                .Sum(c => c.Amount),
            Discount: deal.Charges.Where(c => c.Kind == ChargeKind.Discount).Sum(c => c.Amount),
            TradeAllowance: deal.Trade?.Allowance ?? 0m,
            TradePayoff: deal.Trade?.Payoff ?? 0m,
            AmountDue: deal.AmountDue.Amount,
            VehicleCost: vehicleCost,
            Memo: $"Delivered deal {deal.Id}");

    private async Task<Deal?> LoadAsync(Guid dealId, bool tracked, CancellationToken cancellationToken)
    {
        var query = _db.Deals.Include(d => d.Charges).AsQueryable();
        if (!tracked)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(d => d.Id == dealId, cancellationToken);
    }

    /// <summary>
    /// The customer names and stock numbers for a page of deals, in two queries
    /// rather than two per row, and always through the published contracts.
    /// </summary>
    private async Task<Result<DealLookup>> LookupAsync(
        List<Deal> deals,
        CancellationToken cancellationToken)
    {
        if (deals.Count == 0)
        {
            return Result.Success(new DealLookup([], []));
        }

        var names = await _customers.GetManyAsync(
            deals.Select(d => d.CustomerId).Distinct().ToList(), cancellationToken);

        if (names.IsFailure)
        {
            return Result.Failure<DealLookup>(names.Error);
        }

        var units = await _inventory.GetManyAsync(
            deals.Select(d => d.InventoryUnitId).Distinct().ToList(), cancellationToken);

        if (units.IsFailure)
        {
            return Result.Failure<DealLookup>(units.Error);
        }

        return Result.Success(new DealLookup(names.Value, units.Value));
    }

    private sealed record DealLookup(
        IReadOnlyList<CustomerSummary> Customers,
        IReadOnlyList<InventoryUnitSummary> Units)
    {
        public string CustomerName(Guid id)
        {
            var match = Customers.FirstOrDefault(c => c.Id == id);
            return match is null ? "(customer no longer on file)" : match.DisplayName;
        }

        public InventoryUnitSummary? Unit(Guid id) => Units.FirstOrDefault(u => u.Id == id);
    }

    private async Task<Result<DealDetail>> DescribeAsync(Deal deal, CancellationToken cancellationToken)
    {
        var context = await LookupAsync([deal], cancellationToken);
        if (context.IsFailure)
        {
            return Result.Failure<DealDetail>(context.Error);
        }


        var unit = context.Value.Unit(deal.InventoryUnitId);

        var history = await _db.DealHistory
            .AsNoTracking()
            .Where(h => h.DealId == deal.Id)
            .OrderBy(h => h.OccurredAt)
            .ToListAsync(cancellationToken);

        // A deal that has only just been started has its first history row in
        // memory rather than in a separate query's results.
        if (history.Count == 0)
        {
            history = deal.History.ToList();
        }

        return Result.Success(new DealDetail(
            deal.Id,
            deal.RooftopId,
            deal.Status.ToString(),
            deal.CustomerId,
            context.Value.CustomerName(deal.CustomerId),
            deal.InventoryUnitId,
            unit is null ? "(not on file)" : unit.StockNumber,
            unit is null ? "(not on file)" : unit.VehicleDisplayName,
            deal.LeadId,
            deal.Currency,
            deal.Subtotal.Amount,
            deal.AmountDue.Amount,
            deal.Trade is null
                ? null
                : new TradeInView(
                    deal.Trade.Description, deal.Trade.Allowance, deal.Trade.Payoff,
                    deal.Trade.Equity, deal.Trade.IsNegativeEquity),
            deal.Charges
                .Select(c => new ChargeView(c.Kind.ToString(), c.Description, c.Amount))
                .ToList(),
            deal.SalespersonUserId,
            deal.ApprovedByUserId,
            deal.ApprovedAt,
            deal.TermsAreOpen,
            history
                .OrderBy(h => h.OccurredAt)
                .Select(h => new DealHistoryEntry(
                    h.FromStatus?.ToString(), h.ToStatus.ToString(), h.OccurredAt,
                    h.ChangedByUserId, h.Note, h.AmountAtChange))
                .ToList()));
    }
}

/// <summary>Stable error codes for the Deals capability (doc 06 §6).</summary>
internal static class DealErrors
{
    public static Error Forbidden { get; } = Error.Forbidden(
        "deals.forbidden",
        "You do not have access to this rooftop's deals.");

    public static Error ApprovalForbidden { get; } = Error.Forbidden(
        "deals.approval_forbidden",
        "Approving a deal needs a manager. Ask somebody who holds Deals.Approve.");

    public static Error UnknownStatus { get; } = Error.Validation(
        "deals.unknown_status",
        "That is not a deal status.");

    public static Error UnknownChargeKind { get; } = Error.Validation(
        "deals.unknown_charge_kind",
        "That is not a kind of charge.");

    public static Error CustomerNotFound { get; } = Error.NotFound(
        "deals.customer_not_found",
        "Record the customer before starting their deal.");

    public static Error UnitNotAvailable { get; } = Error.Conflict(
        "deals.unit_not_available",
        "That car is not available. It may already be on another deal.");

    public static Error UnitAtAnotherRooftop { get; } = Error.Conflict(
        "deals.unit_at_another_rooftop",
        "That car is on another location's lot. Transfer it first.");

    public static Error ChangedElsewhere { get; } = Error.Conflict(
        "deals.changed_elsewhere",
        "Somebody else changed this deal. Reload it and try again.");
}
