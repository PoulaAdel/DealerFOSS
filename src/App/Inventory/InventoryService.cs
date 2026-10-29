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

        var recon = await ReconditioningForAsync(
            rows.Select(r => r.Unit.Id).ToList(), cancellationToken);

        var profit = await ProfitScopeAsync(cancellationToken);

        return Result.Success(new Page<InventoryUnitSummary>(
            rows.OrderBy(row => row.Unit.StockNumber, StringComparer.Ordinal)
                .Select(row => Summarize(
                    row.Unit, row.Vehicle, recon.GetValueOrDefault(row.Unit.Id), profit))
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

        return Result.Success(Describe(
            row.Unit, row.Vehicle, history, await ChargesForAsync(unitId, cancellationToken),
            await ProfitScopeAsync(cancellationToken),
            await ManageScopeAsync(cancellationToken)));
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

        var recon = await ReconditioningForAsync(
            rows.Select(r => r.Unit.Id).ToList(), cancellationToken);

        var profit = await ProfitScopeAsync(cancellationToken);

        return Result.Success<IReadOnlyList<InventoryUnitSummary>>(
            rows.Select(row => Summarize(
                row.Unit, row.Vehicle, recon.GetValueOrDefault(row.Unit.Id), profit)).ToList());
    }

    public async Task<Result<ImportOutcome>> ImportAsync(
        ImportedInventoryUnit unit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unit);

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, ManagePermission, unit.RooftopId, cancellationToken))
        {
            return Result.Failure<ImportOutcome>(InventoryErrors.Forbidden);
        }

        if (await _db.InventoryUnits.AsNoTracking().AnyAsync(u => u.Id == unit.Id, cancellationToken))
        {
            return Result.Success(ImportOutcome.AlreadyPresent);
        }

        if (!Enum.TryParse<InventoryStatus>(unit.Status, ignoreCase: true, out var status))
        {
            return Result.Failure<ImportOutcome>(InventoryErrors.UnknownStatus);
        }

        if (!await _db.Vehicles.AsNoTracking().AnyAsync(v => v.Id == unit.VehicleId, cancellationToken))
        {
            return Result.Failure<ImportOutcome>(InventoryErrors.VehicleNotFound);
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
            return Result.Failure<ImportOutcome>(Error.Validation("inventory.invalid", ex.Message));
        }

        // The receiving lot may already number a different car the same way.
        // Refused by name rather than renumbered: a stock number is printed on
        // paperwork and spoken aloud on the lot, and two cars quietly sharing
        // one is worse than one car that did not arrive.
        if (await _db.InventoryUnits
            .AsNoTracking()
            .AnyAsync(u => u.RooftopId == unit.RooftopId && u.StockNumber == stockNumber, cancellationToken))
        {
            return Result.Failure<ImportOutcome>(InventoryErrors.StockNumberTaken(stockNumber));
        }

        // No transaction and no posting. There is exactly one write here, and
        // the ledger is not part of it — see the note on IInventory.ImportAsync.
        _db.InventoryUnits.Add(InventoryUnit.Import(
            unit.Id,
            unit.VehicleId,
            unit.RooftopId,
            stockNumber,
            status,
            _clock.UtcNow,
            _currentUser.Id,
            cost,
            unit.AcquiredOn));

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _db.ForgetPendingWrites();
            return Result.Failure<ImportOutcome>(InventoryErrors.CouldNotBeWritten(ex));
        }

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, ManagePermission, AuditOutcome.Allowed,
                "InventoryUnit", unit.Id.ToString(), unit.RooftopId.Value,
                "Imported from a package", null, null),
            cancellationToken);

        return Result.Success(ImportOutcome.Created);
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

        // A car just taken in has absorbed nothing yet, which is a known amount.
        return Result.Success(Describe(
            received, vehicle, received.StatusHistory, [],
            await ProfitScopeAsync(cancellationToken),
            await ManageScopeAsync(cancellationToken)));
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

        // Sold is not a move a person makes. It is the consequence of delivering
        // a deal, which is also what posts the sale and relieves 1300 — so a car
        // marked Sold here would leave the lot with its entire carrying value
        // still on the balance sheet. See MarkSoldAsync, and the remark on
        // IInventory.ChangeStatusAsync for what this cost before 2026-10-19.
        //
        // Refused HERE rather than by removing Sold from InventoryStatusRules,
        // because the move itself is legitimate — Deals makes it on every
        // delivery. What is illegitimate is a person making it directly, and
        // that is a question about the caller, not about the life cycle.
        if (next == InventoryStatus.Sold)
        {
            return Result.Failure<InventoryUnitDetail>(InventoryErrors.SoldBelongsToADeal);
        }

        // And the same rule in the other direction, which is the less obvious
        // half. The life cycle allows Sold -> Available because "a deal falls
        // through and the car goes back on the lot" — but DealStatusRules has
        // `[Delivered] = []`, so a DELIVERED deal can never be cancelled, and
        // Deals only ever returns a car to stock from OnHold. Nothing in the
        // solution moves a car out of Sold. A person doing it by hand would put
        // the car back on the forecourt while the delivery posting stayed in the
        // ledger: 1300 already relieved, the sale still booked, the car for sale
        // again. The same failure as selling by hand, pointing the other way.
        //
        // Found on 2026-10-19 while computing availableMoves: the browser's old
        // movesFrom() returned nothing for a Sold car, so the screen was right
        // and — exactly as with Sold itself — the server had never heard of it.
        // If a delivery ever becomes reversible, that gets its own door the way
        // MarkSoldAsync did, not this one.
        if (unit.Status == InventoryStatus.Sold)
        {
            return Result.Failure<InventoryUnitDetail>(InventoryErrors.SoldIsUndoneByTheDeal);
        }

        return await MoveAsync(unit, next, change.Note, cancellationToken);
    }

    public async Task<Result<InventoryUnitDetail>> MarkSoldAsync(
        Guid unitId,
        Guid dealId,
        CancellationToken cancellationToken)
    {
        var unit = await _db.InventoryUnits.SingleOrDefaultAsync(u => u.Id == unitId, cancellationToken);
        if (unit is null)
        {
            return Result.Failure<InventoryUnitDetail>(InventoryErrors.Forbidden);
        }

        // The same right as any other move, checked the same way. Delivering a
        // deal already required Deals.Write at this rooftop; this is not a
        // second, weaker door into stock.
        if (!await _access.IsAuthorizedAsync(_currentUser.Id, ManagePermission, unit.RooftopId, cancellationToken))
        {
            return Result.Failure<InventoryUnitDetail>(InventoryErrors.Forbidden);
        }

        // The deal id goes in the note, so the car's own history says which sale
        // took it. Dealership data, not a translated string — the history is a
        // record, and this is the one line in it that points at another record.
        return await MoveAsync(
            unit, InventoryStatus.Sold, $"Sold on deal {dealId}.", cancellationToken);
    }

    /// <summary>
    /// The shared tail of every move: apply it, save it, audit it, and describe
    /// the car afterwards.
    /// </summary>
    /// <remarks>
    /// Extracted when <see cref="MarkSoldAsync"/> arrived rather than duplicated,
    /// because the half worth getting right is the same for both — the status
    /// change and its history line are one write, and the audit row names the
    /// move that actually happened.
    /// </remarks>
    private async Task<Result<InventoryUnitDetail>> MoveAsync(
        InventoryUnit unit,
        InventoryStatus next,
        string? note,
        CancellationToken cancellationToken)
    {
        var from = unit.Status;
        try
        {
            unit.ChangeStatus(next, _clock.UtcNow, _currentUser.Id, note);
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
            .Where(h => h.InventoryUnitId == unit.Id)
            .OrderBy(h => h.OccurredAt)
            .ThenBy(h => h.Sequence)
            .ToListAsync(cancellationToken);

        return Result.Success(Describe(
            unit, vehicle, history, await ChargesForAsync(unit.Id, cancellationToken),
            await ProfitScopeAsync(cancellationToken),
            await ManageScopeAsync(cancellationToken)));
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

    public async Task<Result> CapitaliseReconditioningAsync(
        Guid unitId,
        Money amount,
        Guid sourceRepairOrderId,
        CancellationToken cancellationToken)
    {
        var unit = await _db.InventoryUnits
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == unitId, cancellationToken);

        if (unit is null)
        {
            return Result.Failure(InventoryErrors.UnitNotFound);
        }

        // Manage, not Read. Changing what a car is carried at moves a number on
        // the balance sheet, so it is the same right as moving the car itself —
        // and the workshop caller already holds it for the rooftop it is
        // invoicing at.
        if (!await _access.IsAuthorizedAsync(
            _currentUser.Id, ManagePermission, unit.RooftopId, cancellationToken))
        {
            return Result.Failure(InventoryErrors.Forbidden);
        }

        // A car costed in one currency cannot absorb a charge in another without
        // somebody inventing a rate, and a rate invented at posting time is a
        // guess buried in the balance sheet. Refuse instead. A unit with no
        // recorded cost has no currency to disagree with, so it is allowed —
        // the charge is still attributed, and the book value stays null until a
        // purchase price is entered.
        if (unit.CostCurrency is { } costCurrency
            && !string.Equals(costCurrency, amount.Currency, StringComparison.Ordinal))
        {
            return Result.Failure(InventoryErrors.ReconditioningCurrencyMismatch);
        }

        var charge = ReconditioningCharge.Record(
            unitId, unit.RooftopId, amount, sourceRepairOrderId, _clock.UtcNow);

        if (charge.IsFailure)
        {
            return Result.Failure(charge.Error);
        }

        _db.Set<ReconditioningCharge>().Add(charge.Value);

        // Saved by the caller's transaction, not here: this runs inside the
        // invoicing transaction so a car cannot end up carrying a charge for a
        // posting that rolled back.
        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, ManagePermission, AuditOutcome.Allowed,
                "InventoryUnit", unitId.ToString(), unit.RooftopId.Value,
                $"Reconditioning of {amount} capitalised from repair order {sourceRepairOrderId}",
                null, null),
            cancellationToken);

        return Result.Success();
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

    /// <summary>
    /// Reconditioning per unit, for a page of units, in one query.
    ///
    /// <para>
    /// Summed rather than stored, like every other balance here — see
    /// <see cref="ReconditioningCharge"/> for why a running total on the unit
    /// would not be able to answer "where did this come from". Grouped in one
    /// round trip rather than per row, because the stock list is fifty cars and
    /// this would otherwise be fifty queries to draw one table.
    /// </para>
    /// </summary>
    /// <summary>Every charge on one unit, oldest first.</summary>
    private async Task<IReadOnlyList<ReconditioningCharge>> ChargesForAsync(
        Guid unitId,
        CancellationToken cancellationToken) =>
        await _db.Set<ReconditioningCharge>()
            .AsNoTracking()
            .Where(c => c.InventoryUnitId == unitId)
            .OrderBy(c => c.OccurredAt)
            .ToListAsync(cancellationToken);

    private async Task<Dictionary<Guid, decimal>> ReconditioningForAsync(
        List<Guid> unitIds,
        CancellationToken cancellationToken)
    {
        if (unitIds.Count == 0)
        {
            return [];
        }

        var rows = await _db.Set<ReconditioningCharge>()
            .AsNoTracking()
            .Where(c => unitIds.Contains(c.InventoryUnitId))
            .GroupBy(c => c.InventoryUnitId)
            .Select(g => new { UnitId = g.Key, Total = g.Sum(c => c.Amount) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.UnitId, r => r.Total);
    }

    /// <summary>
    /// Acquisition plus reconditioning, or null when acquisition is unknown.
    ///
    /// <para>
    /// Null rather than treating an unrecorded cost as zero: a car whose
    /// purchase price nobody entered has an unknown book value, and answering
    /// "the recon so far" would be a smaller number wearing the confidence of a
    /// complete one. The ledger is relieved by what is known; a car with no
    /// recorded cost relieves nothing, exactly as before.
    /// </para>
    /// </summary>
    private static decimal? BookValue(decimal? cost, decimal reconditioning) =>
        cost is null ? null : cost.Value + reconditioning;

    /// <summary>
    /// What the lot paid and what the car is carried at, or nothing (ADR-029).
    ///
    /// Takes the whole <see cref="AuthorizedScope"/> rather than a bool because a
    /// list legitimately spans rooftops: a group manager who may see the numbers
    /// at one lot and not the next gets a page that is right row by row. A bool
    /// decided once for the page would be wrong for half of it.
    /// </summary>
    private async Task<AuthorizedScope> ProfitScopeAsync(CancellationToken cancellationToken) =>
        // Not IsAuthorizedAsync: that writes a Denied row per refusal, and a
        // salesperson scrolling the stock list is not an incident.
        await _access.GetAuthorizedScopeAsync(
            _currentUser.Id, Permissions.ProfitabilityRead, cancellationToken);

    /// <summary>
    /// Whether this caller may move stock, per lot, for deciding what to offer.
    /// </summary>
    /// <remarks>
    /// Same instrument and same reason as <see cref="ProfitScopeAsync"/>: not
    /// <c>IsAuthorizedAsync</c>, because this runs on every read of a car —
    /// including the ones nobody is about to touch — and that overload writes a
    /// Denied row per call. An advisor opening six cars to find the right one
    /// would otherwise file six refusals for acts they never attempted.
    /// </remarks>
    private async Task<AuthorizedScope> ManageScopeAsync(CancellationToken cancellationToken) =>
        await _access.GetAuthorizedScopeAsync(
            _currentUser.Id, ManagePermission, cancellationToken);

    /// <summary>
    /// Which moves to offer for this car: the life cycle's legal ones, minus
    /// Sold, or none at all when the caller may not move stock at this lot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>Covers</c>, not <c>Allows</c> (ADR-030).</b> An inventory unit names
    /// nobody — there is no salesperson or technician on a car the way there is
    /// on a deal or a job — and <c>Allows(rooftopId, null)</c> deliberately
    /// REFUSES a caller whose grant is narrowed to their own records. Using it
    /// here would silently hide every move button from anybody holding
    /// <c>OwnRecordsOnly</c>, for every car on the lot. ADR-030 names receiving
    /// stock as its own example of a question that has no owner to ask with, and
    /// `Summarize` above already uses `Covers` for the same reason.
    /// `An_own_work_only_salesperson_can_still_move_stock` is the test that
    /// fails if somebody "tidies" this into `Allows`.
    /// </para>
    /// <para>
    /// All-or-nothing rather than per-move, because every move needs the same
    /// <c>Inventory.Manage</c>. Nothing here splits write from approve the way
    /// Deals splits building a deal from signing it off, so there is no second
    /// scope to fetch.
    /// </para>
    /// <para>
    /// Sold is subtracted because a car is sold by delivering a deal, and a car
    /// that IS Sold offers nothing at all, because a delivery is undone by the
    /// deal too — and a delivered deal cannot be cancelled, so there is nothing
    /// to undo it with. Both halves of that rule used to live only in the
    /// browser, which is exactly why the endpoint accepted either direction from
    /// anybody until 2026-10-19 — see <see cref="ChangeStatusAsync"/>.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// Returns <see cref="List{T}"/> rather than <see cref="IReadOnlyList{T}"/>
    /// because CA1859 is an error here and the only caller is
    /// <see cref="Describe"/>, which widens it into the record field anyway.
    /// </remarks>
    private static List<string> AvailableMoves(InventoryUnit unit, AuthorizedScope manage) =>
        manage.Covers(unit.RooftopId) && unit.Status != InventoryStatus.Sold
            ? InventoryStatusRules.MovesFrom(unit.Status)
                .Where(move => move != InventoryStatus.Sold)
                .Select(move => move.ToString())
                .ToList()
            : [];

    private static InventoryUnitSummary Summarize(
        InventoryUnit u, Vehicle v, decimal recon, AuthorizedScope profit)
    {
        var maySee = profit.Covers(u.RooftopId);

        return new(u.Id, u.StockNumber, u.RooftopId, u.Status.ToString(), v.Id, v.Vin, v.DisplayName,
            maySee ? u.CostAmount : null,
            maySee ? u.CostCurrency : null,
            maySee ? recon : null,
            maySee ? BookValue(u.CostAmount, recon) : null);
    }

    private static InventoryUnitDetail Describe(
        InventoryUnit u,
        Vehicle v,
        IReadOnlyList<InventoryStatusChange> history,
        IReadOnlyList<ReconditioningCharge> reconditioning,
        AuthorizedScope profit,
        AuthorizedScope manage) =>
        new(u.Id,
            u.StockNumber,
            u.RooftopId,
            u.Status.ToString(),
            v.Id,
            v.Vin,
            v.DisplayName,
            profit.Covers(u.RooftopId) ? u.CostAmount : null,
            profit.Covers(u.RooftopId) ? u.CostCurrency : null,
            u.AcquiredOn,
            history
                .OrderBy(h => h.OccurredAt)
                .ThenBy(h => h.Sequence)
                .Select(h => new InventoryStatusEntry(
                    h.FromStatus?.ToString(), h.ToStatus.ToString(), h.OccurredAt, h.Note))
                .ToList(),
            profit.Covers(u.RooftopId) ? reconditioning.Sum(c => c.Amount) : null,
            profit.Covers(u.RooftopId)
                ? BookValue(u.CostAmount, reconditioning.Sum(c => c.Amount))
                : null,
            // The charges behind the figure go with the figure. A list of what
            // was spent is the same disclosure as the total of it.
            profit.Covers(u.RooftopId)
                ? reconditioning
                    .OrderBy(c => c.OccurredAt)
                    .Select(c => new ReconditioningEntry(
                        c.Amount, c.Currency, c.SourceRepairOrderId, c.OccurredAt))
                    .ToList()
                : [],
            AvailableMoves(u, manage));
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

    /// <summary>
    /// A person tried to sell a car by typing a status onto it. Conflict rather
    /// than Forbidden, and the distinction matters: nobody holds a permission
    /// that would make this allowed, so it is not about who is asking — the act
    /// belongs to a different part of the system, and the message says which.
    /// </summary>
    public static Error SoldBelongsToADeal { get; } = Error.Conflict(
        "inventory.sold_belongs_to_a_deal",
        "A car is sold by delivering its deal, which records the sale in the books. "
            + "Mark it On hold for a customer instead.");

    /// <summary>
    /// The other direction, and the reason it is refused rather than allowed: a
    /// car put back on the lot by hand would be for sale again while the ledger
    /// still held its delivery. See the comment in ChangeStatusAsync.
    /// </summary>
    public static Error SoldIsUndoneByTheDeal { get; } = Error.Conflict(
        "inventory.sold_is_undone_by_the_deal",
        "This car has been sold. Putting it back on the lot means reversing the deal, "
            + "which also takes the sale back out of the books.");

    public static Error VehicleNotFound { get; } = Error.NotFound(
        "inventory.vehicle_not_found",
        "Record the vehicle before taking it into stock.");

    public static Error ChangedElsewhere { get; } = Error.Conflict(
        "inventory.changed_elsewhere",
        "Somebody else changed this unit. Reload it and try again.");

    public static Error StockNumberTaken(string stockNumber) => Error.Conflict(
        "inventory.stock_number_taken",
        $"Stock number {stockNumber} is already in use at this rooftop.");

    public static Error UnitNotFound { get; } = Error.NotFound(
        "inventory.unit_not_found",
        "That car is not in stock here.");

    /// <summary>
    /// The database refused an arriving stock record for a reason nothing
    /// checked for. One record's problem is one line in an import report rather
    /// than a failed request.
    /// </summary>
    public static Error CouldNotBeWritten(Exception cause) => Error.Conflict(
        "inventory.could_not_be_written",
        $"That stock record could not be written: {cause?.InnerException?.Message ?? cause?.Message}");

    public static Error ReconditioningIsNothing { get; } = Error.Validation(
        "inventory.reconditioning_is_nothing",
        "Reconditioning of nothing is not a charge worth recording.");

    /// <summary>
    /// Refused rather than converted. A car costed in one currency carrying a
    /// charge in another has a book value that is not a number, and inventing a
    /// rate at the moment of posting would bury that in the balance sheet.
    /// </summary>
    public static Error ReconditioningCurrencyMismatch { get; } = Error.Validation(
        "inventory.reconditioning_currency_mismatch",
        "Reconditioning must be in the same currency as the car it goes onto.");
}
