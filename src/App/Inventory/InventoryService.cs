// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   InventoryService — what is on the lot, and the rooftop scope applied to it.
//
// Usage:
//   Through IInventory.
//
// Coding Instructions:
//   This is where the rooftop boundary is actually enforced. Every read is
//   filtered to the caller's authorized rooftops, and every write authorizes
//   the specific rooftop first. An unauthorized unit and an unknown one
//   return the same failure, so a response cannot be used to discover what
//   another location has in stock. Removing either check fails
//   InventoryScopeTests.

using Microsoft.EntityFrameworkCore;
using DealerFOSS.Accounting;
using DealerFOSS.Core;
using DealerFOSS.Data;
using DealerFOSS.Identity;
using DealerFOSS.Vehicles;

namespace DealerFOSS.Inventory;

public sealed class InventoryService(
    TenantDb db,
    IAccessDirectory access,
    ICurrentUser currentUser,
    IAuditSink audit,
    IClock clock,
    IAccounting accounting)
    : IInventory
{
    private const string ReadPermission = "Inventory.Read";
    private const string ManagePermission = "Inventory.Manage";

    /// <summary>Caps how many rows a single list can return, however it is called.</summary>
    private const int MaxResults = 200;

    private readonly TenantDb _db = db;
    private readonly IAccessDirectory _access = access;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditSink _audit = audit;
    private readonly IClock _clock = clock;
    private readonly IAccounting _accounting = accounting;

    public async Task<Result<Page<InventoryUnitSummary>>> ListAsync(
        InventoryQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<Page<InventoryUnitSummary>>(InventoryErrors.Forbidden);
        }

        // Asking for one rooftop is answered with the same refusal whether the
        // caller may not see it or it does not exist.
        if (query.RooftopId is { } requested && !scope.Covers(requested))
        {
            return Result.Failure<Page<InventoryUnitSummary>>(InventoryErrors.Forbidden);
        }

        InventoryStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse(query.Status, ignoreCase: true, out InventoryStatus parsed))
            {
                return Result.Failure<Page<InventoryUnitSummary>>(InventoryErrors.UnknownStatus);
            }

            status = parsed;
        }

        var take = Paging.Limit(query.Limit);
        var skip = Paging.Offset(query.Offset);
        var units = _db.InventoryUnits.AsNoTracking();

        // The scope filter is applied to the query itself, not to the results:
        // another rooftop's rows must never be read, let alone returned.
        if (!scope.IsOrganizationWide)
        {
            var allowed = scope.Rooftops.ToList();
            units = units.Where(u => allowed.Contains(u.RooftopId));
        }

        if (query.RooftopId is { } only)
        {
            units = units.Where(u => u.RooftopId == only);
        }

        if (status is { } wanted)
        {
            units = units.Where(u => u.Status == wanted);
        }

        // "Anything somebody could still be interested in" is a real question a
        // single status cannot express. An enquiry screen wants the car being
        // reconditioned and the one on hold for somebody else — an enquiry is not
        // a claim on the car, and a second person's interest is worth recording —
        // but never one that has already gone.
        //
        // Added 2026-09-15: the enquiry form asked for everything and offered
        // SOLD cars, under a comment saying it did not. Filtering to Available
        // would have been the easy fix and the wrong one, because it drops the
        // two states the comment was protecting.
        if (query.StillGettable)
        {
            units = units.Where(u =>
                u.Status != InventoryStatus.Sold && u.Status != InventoryStatus.Removed);
        }

        if (!string.IsNullOrWhiteSpace(query.StockNumber))
        {
            var stock = InventoryUnit.NormalizeStockNumber(query.StockNumber);
            units = units.Where(u => u.StockNumber == stock);
        }

        var search = (query.Search ?? string.Empty).Trim();
        if (search.Length > 0)
        {
            // Matched against the vehicle, but expressed as a condition on the
            // unit: the join happens once, at the end, so the ordering and the
            // row limit stay server-side.
            var vin = Vin.Normalize(search);

            units = units.Where(u => _db.Vehicles.Any(v =>
                v.Id == u.VehicleId
                && (EF.Functions.Like(v.Vin, $"%{vin}%")
                    || EF.Functions.Like(v.Make, $"%{search}%")
                    || EF.Functions.Like(v.Model, $"%{search}%"))));
        }

        // Counted over the same filters as the page, and before it is taken.
        var total = await units.CountAsync(cancellationToken);

        var rows = await Join(units.OrderBy(u => u.StockNumber).Skip(skip).Take(take))
            .ToListAsync(cancellationToken);

        return Result.Success(new Page<InventoryUnitSummary>(
            rows.OrderBy(row => row.Unit.StockNumber, StringComparer.Ordinal)
                .Select(row => Summarize(row.Unit, row.Vehicle))
                .ToList(),
            total,
            skip,
            take));
    }

    public async Task<Result<InventoryUnitDetail>> GetAsync(Guid unitId, CancellationToken cancellationToken)
    {
        var row = await Join(_db.InventoryUnits.AsNoTracking().Where(u => u.Id == unitId))
            .SingleOrDefaultAsync(cancellationToken);

        // Unknown and unauthorized answer identically, so a caller cannot probe
        // for units belonging to a rooftop they may not see.
        if (row is null)
        {
            return Result.Failure<InventoryUnitDetail>(InventoryErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, ReadPermission, row.Unit.RooftopId, cancellationToken))
        {
            return Result.Failure<InventoryUnitDetail>(InventoryErrors.Forbidden);
        }

        var history = await _db.Set<InventoryStatusChange>()
            .AsNoTracking()
            .Where(h => h.InventoryUnitId == unitId)
            .OrderBy(h => h.OccurredAt)
            .ThenBy(h => h.Sequence)
            .ToListAsync(cancellationToken);

        return Result.Success(Describe(row.Unit, row.Vehicle, history));
    }

    public async Task<Result<IReadOnlyList<InventoryUnitSummary>>> GetManyAsync(
        IReadOnlyCollection<Guid> unitIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unitIds);

        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<IReadOnlyList<InventoryUnitSummary>>(InventoryErrors.Forbidden);
        }

        if (unitIds.Count == 0)
        {
            return Result.Success<IReadOnlyList<InventoryUnitSummary>>([]);
        }

        var wanted = unitIds.Distinct().Take(MaxResults).ToList();
        var units = _db.InventoryUnits.AsNoTracking().Where(u => wanted.Contains(u.Id));

        // The scope filter applies here exactly as it does to a list: asking by id
        // must not be a way around it.
        if (!scope.IsOrganizationWide)
        {
            var allowed = scope.Rooftops.ToList();
            units = units.Where(u => allowed.Contains(u.RooftopId));
        }

        var rows = await Join(units).ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<InventoryUnitSummary>>(
            rows.Select(row => Summarize(row.Unit, row.Vehicle)).ToList());
    }

    public async Task<Result<InventoryUnitDetail>> ReceiveAsync(
        NewInventoryUnit unit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unit);

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, ManagePermission, unit.RooftopId, cancellationToken))
        {
            return Result.Failure<InventoryUnitDetail>(InventoryErrors.Forbidden);
        }

        var vehicle = await _db.Vehicles
            .AsNoTracking()
            .SingleOrDefaultAsync(v => v.Id == unit.VehicleId, cancellationToken);

        if (vehicle is null)
        {
            return Result.Failure<InventoryUnitDetail>(InventoryErrors.VehicleNotFound);
        }

        Money? cost;
        string stockNumber;
        try
        {
            cost = unit.CostAmount is null
                ? null
                : new Money(unit.CostAmount.Value, unit.CostCurrency ?? string.Empty);
            stockNumber = InventoryUnit.NormalizeStockNumber(unit.StockNumber);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<InventoryUnitDetail>(Error.Validation("inventory.invalid", ex.Message));
        }

        // Checked before saving so the caller gets a sentence rather than a
        // constraint violation; the unique index is still what guarantees it.
        var taken = await _db.InventoryUnits
            .AsNoTracking()
            .AnyAsync(u => u.RooftopId == unit.RooftopId && u.StockNumber == stockNumber, cancellationToken);

        if (taken)
        {
            return Result.Failure<InventoryUnitDetail>(InventoryErrors.StockNumberTaken(stockNumber));
        }

        var received = InventoryUnit.Receive(
            Guid.NewGuid(),
            unit.VehicleId,
            unit.RooftopId,
            stockNumber,
            _clock.UtcNow,
            _currentUser.Id,
            cost,
            unit.AcquiredOn,
            unit.Note);

        // The unit and its ledger entry land together or not at all. A car in the
        // stock list that the books have never heard of is exactly the state this
        // whole change exists to end, so it must not be creatable by a posting
        // that failed halfway.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        _db.InventoryUnits.Add(received);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two people numbering a car at the same moment; the index caught it.
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<InventoryUnitDetail>(InventoryErrors.StockNumberTaken(stockNumber));
        }

        // The car is on the lot; now it is on the balance sheet. Until 2026-09-10
        // this posting did not exist at all, and vehicle inventory only ever went
        // down — delivery relieved 1300 for every car sold and nothing ever put
        // one there, leaving the account at minus $993,190 on a dealership that
        // had sold thirty cars.
        //
        // A cost is what makes it postable. A car received without one is a real
        // and ordinary thing — a part-exchange still being appraised, stock
        // arriving before the invoice does — so it is recorded and not posted,
        // rather than refused or posted at zero. That unit is then a known gap
        // rather than a silent one: it has no cost on it to report.
        if (cost is { Amount: > 0m })
        {
            var posted = await _accounting.PostStockPurchaseAsync(
                new StockPurchasePosting(
                    unit.RooftopId,
                    stockNumber,
                    cost.Value.Currency,
                    cost.Value.Amount,
                    $"Stock {stockNumber} — {vehicle.DisplayName}",
                    unit.Floorplanned),
                cancellationToken);

            if (posted.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<InventoryUnitDetail>(posted.Error);
            }
        }

        await transaction.CommitAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, ManagePermission, AuditOutcome.Allowed,
                "InventoryUnit", received.Id.ToString(), unit.RooftopId.Value, "Received", null, null),
            cancellationToken);

        return Result.Success(Describe(received, vehicle, received.StatusHistory));
    }

    public async Task<Result<InventoryUnitDetail>> ChangeStatusAsync(
        Guid unitId,
        StatusChangeRequest change,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);

        var unit = await _db.InventoryUnits.SingleOrDefaultAsync(u => u.Id == unitId, cancellationToken);
        if (unit is null)
        {
            return Result.Failure<InventoryUnitDetail>(InventoryErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, ManagePermission, unit.RooftopId, cancellationToken))
        {
            return Result.Failure<InventoryUnitDetail>(InventoryErrors.Forbidden);
        }

        if (!Enum.TryParse(change.Status, ignoreCase: true, out InventoryStatus next))
        {
            return Result.Failure<InventoryUnitDetail>(InventoryErrors.UnknownStatus);
        }

        var from = unit.Status;
        try
        {
            unit.ChangeStatus(next, _clock.UtcNow, _currentUser.Id, change.Note);
        }
        catch (InvalidOperationException ex)
        {
            // A refused move is an ordinary business outcome, not a bug: the
            // message names the moves that are available instead.
            return Result.Failure<InventoryUnitDetail>(
                Error.Conflict("inventory.status_not_allowed", ex.Message));
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<InventoryUnitDetail>(InventoryErrors.ChangedElsewhere);
        }

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, ManagePermission, AuditOutcome.Allowed,
                "InventoryUnit", unit.Id.ToString(), unit.RooftopId.Value,
                $"{from} to {next}", null, null),
            cancellationToken);

        var vehicle = await _db.Vehicles
            .AsNoTracking()
            .SingleAsync(v => v.Id == unit.VehicleId, cancellationToken);

        var history = await _db.Set<InventoryStatusChange>()
            .AsNoTracking()
            .Where(h => h.InventoryUnitId == unitId)
            .OrderBy(h => h.OccurredAt)
            .ThenBy(h => h.Sequence)
            .ToListAsync(cancellationToken);

        return Result.Success(Describe(unit, vehicle, history));
    }

    public async Task<Result<Guid?>> FindOwnedAsync(
        Guid vehicleId,
        RooftopId rooftopId,
        CancellationToken cancellationToken)
    {
        if (!await _access.IsAuthorizedAsync(_currentUser.Id, ReadPermission, rooftopId, cancellationToken))
        {
            return Result.Failure<Guid?>(InventoryErrors.Forbidden);
        }

        // Ordered so the answer is the same every time a job is re-read. A
        // vehicle should not be in stock twice at one rooftop, but if a bad
        // import ever puts it there, silently picking a different unit on
        // different days would be worse than picking a predictable one.
        var unitId = await _db.InventoryUnits
            .AsNoTracking()
            .Where(u => u.VehicleId == vehicleId
                && u.RooftopId == rooftopId
                && u.Status != InventoryStatus.Sold
                && u.Status != InventoryStatus.Removed)
            .OrderBy(u => u.StockNumber)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return Result.Success(unitId);
    }

    public async Task<Result<StockAging>> AgingAsync(
        StockAgingQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<StockAging>(InventoryErrors.Forbidden);
        }

        if (query.RooftopId is { } requested && !scope.Covers(requested))
        {
            return Result.Failure<StockAging>(InventoryErrors.Forbidden);
        }

        var asOf = query.AsOf ?? DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);

        var units = _db.InventoryUnits.AsNoTracking().Where(u => Unsold.Contains(u.Status));

        if (!scope.IsOrganizationWide)
        {
            var allowed = scope.Rooftops.ToList();
            units = units.Where(u => allowed.Contains(u.RooftopId));
        }

        if (query.RooftopId is { } only)
        {
            units = units.Where(u => u.RooftopId == only);
        }

        // No Take: this is a count of everything standing on the lot, and a lot
        // with more cars than a page limit is exactly the one whose aging matters.
        // The rows are small and the answer is wrong if any are left out.
        var rows = await Join(units).ToListAsync(cancellationToken);

        var aged = rows
            .Select(row => Age(row.Unit, row.Vehicle, asOf))
            .OrderByDescending(unit => unit.DaysInStock)
            .ThenBy(unit => unit.StockNumber, StringComparer.Ordinal)
            .ToList();

        var bands = AgeBands
            .Select(band => new StockAgeBand(
                band.Name,
                band.FromDay,
                band.ToDay,
                aged.Count(unit =>
                    unit.DaysInStock >= band.FromDay
                    && (band.ToDay is null || unit.DaysInStock <= band.ToDay))))
            .ToList();

        return Result.Success(new StockAging(
            asOf,
            aged.Count,
            bands,
            aged.Take(OldestShown).ToList()));
    }

    /// <summary>
    /// The statuses that still represent money tied up. Incoming counts: it is
    /// bought and paid for, and a car that never arrives is precisely the kind of
    /// aging nobody notices.
    /// </summary>
    private static readonly InventoryStatus[] Unsold =
    [
        InventoryStatus.Incoming,
        InventoryStatus.Reconditioning,
        InventoryStatus.Available,
        InventoryStatus.OnHold,
    ];

    /// <summary>
    /// Thirty-day bands, which is how floor-plan interest is charged and therefore
    /// how a dealer already thinks about stock. The last one is open-ended.
    /// </summary>
    private static readonly (string Name, int FromDay, int? ToDay)[] AgeBands =
    [
        ("0 to 30 days", 0, 30),
        ("31 to 60 days", 31, 60),
        ("61 to 90 days", 61, 90),
        ("Over 90 days", 91, null),
    ];

    private const int OldestShown = 5;

    private static AgingUnit Age(InventoryUnit unit, Vehicle vehicle, DateOnly asOf)
    {
        // Falls back to the day the unit was entered when nobody recorded an
        // acquisition date. Treating a missing date as age zero would hide the
        // oldest cars in the newest band, which is the one mistake this report
        // cannot afford — so the estimate is used and then declared.
        var estimated = unit.AcquiredOn is null;
        var from = unit.AcquiredOn ?? DateOnly.FromDateTime(unit.CreatedAt.UtcDateTime);

        // A car entered with tomorrow's acquisition date is not minus one day old.
        var days = Math.Max(0, asOf.DayNumber - from.DayNumber);

        return new AgingUnit(
            unit.Id, unit.StockNumber, vehicle.DisplayName, unit.Status.ToString(), days, estimated);
    }

    /// <summary>
    /// A unit is only meaningful next to its vehicle, so every read pairs the two
    /// in one query rather than fetching vehicles row by row.
    /// </summary>
    private IQueryable<UnitRow> Join(IQueryable<InventoryUnit> units) =>
        from unit in units
        join vehicle in _db.Vehicles.AsNoTracking() on unit.VehicleId equals vehicle.Id
        select new UnitRow(unit, vehicle);

    private sealed record UnitRow(InventoryUnit Unit, Vehicle Vehicle);

    private static InventoryUnitSummary Summarize(InventoryUnit u, Vehicle v) =>
        new(u.Id, u.StockNumber, u.RooftopId, u.Status.ToString(), v.Id, v.Vin, v.DisplayName);

    private static InventoryUnitDetail Describe(
        InventoryUnit u,
        Vehicle v,
        IReadOnlyList<InventoryStatusChange> history) =>
        new(u.Id,
            u.StockNumber,
            u.RooftopId,
            u.Status.ToString(),
            v.Id,
            v.Vin,
            v.DisplayName,
            u.CostAmount,
            u.CostCurrency,
            u.AcquiredOn,
            history
                .OrderBy(h => h.OccurredAt)
                .ThenBy(h => h.Sequence)
                .Select(h => new InventoryStatusEntry(
                    h.FromStatus?.ToString(), h.ToStatus.ToString(), h.OccurredAt, h.Note))
                .ToList());
}

/// <summary>Stable error codes for the Inventory capability (doc 06 §6).</summary>
internal static class InventoryErrors
{
    public static Error Forbidden { get; } = Error.Forbidden(
        "inventory.forbidden",
        "You do not have access to this rooftop's inventory.");

    public static Error UnknownStatus { get; } = Error.Validation(
        "inventory.unknown_status",
        "That is not an inventory status.");

    public static Error VehicleNotFound { get; } = Error.NotFound(
        "inventory.vehicle_not_found",
        "Record the vehicle before taking it into stock.");

    public static Error ChangedElsewhere { get; } = Error.Conflict(
        "inventory.changed_elsewhere",
        "Somebody else changed this unit. Reload it and try again.");

    public static Error StockNumberTaken(string stockNumber) => Error.Conflict(
        "inventory.stock_number_taken",
        $"Stock number {stockNumber} is already in use at this rooftop.");
}
