// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   InventoryUnit — one vehicle standing on one rooftop's lot, with a stock
//   number, a status, and what it cost.
//
// Usage:
//   InventoryUnit.Receive(...) to take a vehicle into stock, then
//   ChangeStatus to move it along. Every move writes a history line.
//
// Coding Instructions:
//   This is the rooftop-scoped half of the module (doc 04 §1, §3). RooftopId
//   is a permission boundary, unlike Customer.HomeRooftopId — a user assigned
//   to one location must never see or move another location's stock. The
//   scope check lives in InventoryService; removing it fails
//   InventoryScopeTests.

using DealerFOSS.Core;

namespace DealerFOSS.Inventory;

public sealed class InventoryUnit : AuditableEntity
{
    private readonly List<InventoryStatusChange> _statusHistory = [];

    public Guid Id { get; private set; }

    public Guid VehicleId { get; private set; }

    /// <summary>The rooftop that owns this unit. A permission boundary.</summary>
    public RooftopId RooftopId { get; private set; }

    /// <summary>The number written on the windscreen. Unique within its rooftop.</summary>
    public string StockNumber { get; private set; } = string.Empty;

    public InventoryStatus Status { get; private set; }

    /// <summary>What the dealership paid. Null while it is not yet known.</summary>
    public decimal? CostAmount { get; private set; }

    /// <summary>ISO 4217 code; always present when an amount is.</summary>
    public string? CostCurrency { get; private set; }

    /// <summary>Cost as money, so no caller has to pair the two columns itself.</summary>
    public Money? Cost => CostAmount is null || CostCurrency is null
        ? null
        : new Money(CostAmount.Value, CostCurrency);

    /// <summary>The day it became the dealership's, in the rooftop's own terms.</summary>
    public DateOnly? AcquiredOn { get; private set; }

    public IReadOnlyList<InventoryStatusChange> StatusHistory => _statusHistory;

    private InventoryUnit()
    {
    }

    /// <summary>
    /// Takes a vehicle into a rooftop's stock. A unit starts as
    /// <see cref="InventoryStatus.Incoming"/> even when it is already standing on
    /// the lot — the first status change to Available is what a manager
    /// recognises as "it is ready to sell", and that moment is worth recording.
    /// </summary>
    public static InventoryUnit Receive(
        Guid id,
        Guid vehicleId,
        RooftopId rooftopId,
        string stockNumber,
        DateTimeOffset receivedAt,
        Guid? receivedByUserId = null,
        Money? cost = null,
        DateOnly? acquiredOn = null,
        string? note = null)
    {
        var normalizedStock = NormalizeStockNumber(stockNumber);

        var unit = new InventoryUnit
        {
            Id = id,
            VehicleId = vehicleId,
            RooftopId = rooftopId,
            StockNumber = normalizedStock,
            Status = InventoryStatus.Incoming,
            CostAmount = cost?.Amount,
            CostCurrency = cost?.Currency,
            AcquiredOn = acquiredOn,
        };

        unit._statusHistory.Add(new InventoryStatusChange(
            Guid.NewGuid(), id, null, InventoryStatus.Incoming, receivedAt, receivedByUserId, note));

        return unit;
    }

    /// <summary>
    /// A unit arriving from another DealerFOSS installation, in the state it was
    /// already in, keeping its id.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Receive"/> and not built on it, because
    /// replaying Incoming → Reconditioning → Available would write a status
    /// history that never happened, dated today, attributed to whoever ran the
    /// import. One entry saying the record arrived is the truth; a fabricated
    /// trail that looks like the real thing is worse than a short one.
    ///
    /// The status is taken as given rather than checked against the life-cycle
    /// rules on purpose: those rules govern moves, and this is not a move. A
    /// Sold car has to be able to arrive Sold.
    /// </remarks>
    public static InventoryUnit Import(
        Guid id,
        Guid vehicleId,
        RooftopId rooftopId,
        string stockNumber,
        InventoryStatus status,
        DateTimeOffset importedAt,
        Guid? importedByUserId = null,
        Money? cost = null,
        DateOnly? acquiredOn = null)
    {
        var unit = new InventoryUnit
        {
            Id = id,
            VehicleId = vehicleId,
            RooftopId = rooftopId,
            StockNumber = NormalizeStockNumber(stockNumber),
            Status = status,
            CostAmount = cost?.Amount,
            CostCurrency = cost?.Currency,
            AcquiredOn = acquiredOn,
        };

        unit._statusHistory.Add(new InventoryStatusChange(
            Guid.NewGuid(), id, null, status, importedAt, importedByUserId,
            "Arrived in a records package from another installation."));

        return unit;
    }

    /// <summary>
    /// Moves the unit to another status and records the move. Refuses a move the
    /// life cycle does not allow, so a unit cannot leave a terminal state or
    /// change to the status it is already in.
    /// </summary>
    public void ChangeStatus(
        InventoryStatus next,
        DateTimeOffset occurredAt,
        Guid? changedByUserId = null,
        string? note = null)
    {
        if (!InventoryStatusRules.CanMove(Status, next))
        {
            var options = InventoryStatusRules.MovesFrom(Status);
            throw new InvalidOperationException(
                options.Count == 0
                    ? $"A {Status} unit cannot change status. Receive it again to put it back in stock."
                    : $"A {Status} unit cannot become {next}. It can become: {string.Join(", ", options)}.");
        }

        _statusHistory.Add(new InventoryStatusChange(
            Guid.NewGuid(), Id, Status, next, occurredAt, changedByUserId, note));

        Status = next;
    }

    public void SetCost(Money? cost)
    {
        CostAmount = cost?.Amount;
        CostCurrency = cost?.Currency;
    }

    /// <summary>
    /// Stock numbers are written by hand and read aloud, so they are stored
    /// upper-cased and without spaces: "a 1234" and "A1234" are the same car.
    /// </summary>
    public static string NormalizeStockNumber(string? stockNumber)
    {
        var trimmed = (stockNumber ?? string.Empty).Trim().ToUpperInvariant();
        var normalized = new string(trimmed.Where(c => c is not (' ' or '-')).ToArray());

        if (normalized.Length == 0)
        {
            throw new ArgumentException("A unit needs a stock number.", nameof(stockNumber));
        }

        return normalized;
    }
}
