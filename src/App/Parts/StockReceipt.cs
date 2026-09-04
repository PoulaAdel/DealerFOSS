// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   StockReceipt — one delivery of one part onto one rooftop's shelf, at a cost.
//
// Usage:
//   Created by PartsService.ReceiveAsync; consumed by IssueAsync.
//
// Coding Instructions:
//   This is the single source of truth for both quantity and cost, and it is
//   kept that way ON PURPOSE even though only FIFO strictly needs layers.
//
//   The costing method is a setting the manager can change. If stock were
//   held as a running total plus an average cost, switching to FIFO later
//   would have no history to consume and would silently produce wrong costs
//   from the day it was switched. Keeping layers always means every method
//   reads the same data: moving average is value over quantity, last cost is
//   the newest layer, FIFO consumes the oldest. Switching is then safe at any
//   moment, and costs a little space nobody will notice.
//
//   RemainingQuantity only ever decreases. A layer at zero is kept, because
//   it is what a past sale's cost was calculated from.

using DealerFOSS.Core;

namespace DealerFOSS.Parts;

/// <summary>
/// A quantity of one part received onto one rooftop at a known unit cost. Layers
/// are consumed as parts are sold; a spent layer stays for the audit trail.
/// </summary>
public sealed class StockReceipt : AuditableEntity
{
    public Guid Id { get; private set; }

    public Guid PartId { get; private set; }

    public RooftopId RooftopId { get; private set; }

    public decimal QuantityReceived { get; private set; }

    /// <summary>What is left of this layer. Never negative, never increases.</summary>
    public decimal RemainingQuantity { get; private set; }

    public decimal UnitCostAmount { get; private set; }

    public string UnitCostCurrency { get; private set; } = "USD";

    public Money UnitCost => new(UnitCostAmount, UnitCostCurrency);

    public DateTimeOffset ReceivedAt { get; private set; }

    /// <summary>Free text: a supplier's delivery note number, typically.</summary>
    public string? Reference { get; private set; }

    private StockReceipt()
    {
    }

    public StockReceipt(
        Guid id,
        Guid partId,
        RooftopId rooftopId,
        decimal quantity,
        Money unitCost,
        DateTimeOffset receivedAt,
        string? reference)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity), "A receipt has to bring in more than nothing.");
        }

        if (unitCost.Amount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unitCost), "A part cannot cost less than nothing.");
        }

        Id = id;
        PartId = partId;
        RooftopId = rooftopId;
        QuantityReceived = quantity;
        RemainingQuantity = quantity;
        UnitCostAmount = unitCost.Amount;
        UnitCostCurrency = unitCost.Currency;
        ReceivedAt = receivedAt;
        Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
    }

    /// <summary>
    /// Takes as much as this layer can give, up to <paramref name="wanted"/>, and
    /// reports what it actually gave. The caller keeps asking the next layer
    /// until it has enough.
    /// </summary>
    public decimal Take(decimal wanted)
    {
        if (wanted <= 0)
        {
            return 0m;
        }

        var taken = Math.Min(wanted, RemainingQuantity);
        RemainingQuantity -= taken;
        return taken;
    }

    /// <summary>Puts stock back when a sale is undone. Never exceeds what came in.</summary>
    public void Return(decimal quantity)
    {
        if (quantity <= 0)
        {
            return;
        }

        RemainingQuantity = Math.Min(QuantityReceived, RemainingQuantity + quantity);
    }
}
