// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   DealProduct — one finance product actually sold on one deal.
//
// Usage:
//   Set with the rest of the terms, while the deal is still Draft.
//
// Coding Instructions:
//   The price AND the cost are copied onto this row at the point of sale
//   rather than read from the catalogue. That is the whole design.
//
//   F&I is negotiated: the same warranty goes out at different prices on
//   different deals, and the provider's cost changes when the term does. If
//   either figure were read live from the catalogue, next month's price list
//   would silently rewrite last month's gross — the same hazard as a part's
//   cost, and the same answer.
//
//   Gross is price minus cost, and it is the number an F&I manager is
//   measured on. It is computed here rather than stored, because two stored
//   figures and a stored difference is one figure too many.
//
//   CANCELLATION IS A SEPARATE EVENT, NOT AN EDIT. Price and Cost stay exactly
//   as sold — that is the audit trail, and the same reason the catalogue is
//   never re-read. A cancelled product records what came back to the customer
//   alongside what was originally agreed, rather than rewriting either figure
//   to zero.

using DealerFOSS.Core;

namespace DealerFOSS.Deals;

/// <summary>
/// A product sold with the car, at the price and cost agreed on this deal.
/// </summary>
public sealed class DealProduct
{
    public Guid Id { get; private set; }

    public Guid DealId { get; private set; }

    /// <summary>The catalogue entry. Kept so a screen can name the provider.</summary>
    public Guid FinanceProductId { get; private set; }

    /// <summary>
    /// Copied from the catalogue at the point of sale. A withdrawn or renamed
    /// product must not change what this deal says was sold.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>What the customer pays for it. Added to the amount due.</summary>
    public decimal Price { get; private set; }

    /// <summary>
    /// What the provider charges the dealership. Never shown to the customer, and
    /// the reason there is an F&amp;I gross figure at all.
    /// </summary>
    public decimal Cost { get; private set; }

    public int? TermMonths { get; private set; }

    public int? TermMiles { get; private set; }

    /// <summary>What the dealership made on it.</summary>
    public decimal Gross => Price - Cost;

    public bool IsCancelled { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>
    /// What was credited back to the customer. Zero is a real answer — a
    /// product cancelled inside a non-refundable window still needs recording.
    /// </summary>
    public decimal? RefundAmount { get; private set; }

    public string? CancellationReason { get; private set; }

    private DealProduct()
    {
    }

    internal DealProduct(
        Guid id,
        Guid dealId,
        Guid financeProductId,
        string name,
        decimal price,
        decimal cost,
        int? termMonths,
        int? termMiles)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A sold product needs a name.", nameof(name));
        }

        if (price < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "A price cannot be negative.");
        }

        if (cost < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(cost), "A cost cannot be negative.");
        }

        // Deliberately NOT refused when cost exceeds price. Selling a product at a
        // loss is a real decision — usually to hold a deal together — and a system
        // that forbids it just gets a wrong cost typed in instead.
        Id = id;
        DealId = dealId;
        FinanceProductId = financeProductId;
        Name = name.Trim();
        Price = price;
        Cost = cost;
        TermMonths = termMonths;
        TermMiles = termMiles;
    }

    /// <summary>
    /// Cancels a product already sold. Once only — a product cancelled twice is
    /// a mistake, not a bigger refund.
    /// </summary>
    internal void Cancel(DateTimeOffset cancelledAt, decimal refundAmount, string? reason)
    {
        if (IsCancelled)
        {
            throw new InvalidOperationException($"{Name} has already been cancelled.");
        }

        if (refundAmount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(refundAmount), "A refund cannot be negative.");
        }

        if (refundAmount > Price)
        {
            throw new ArgumentOutOfRangeException(
                nameof(refundAmount), $"That is more than the {Price} the customer paid for this.");
        }

        IsCancelled = true;
        CancelledAt = cancelledAt;
        RefundAmount = refundAmount;
        CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }
}
