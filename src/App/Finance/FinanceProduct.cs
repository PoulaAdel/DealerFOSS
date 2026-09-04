// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   FinanceProduct — something sold alongside the car: a warranty, GAP, a service
//   plan, paint protection.
//
// Usage:
//   The catalogue. What actually gets sold is a DealProduct on a deal, which
//   carries its own price and cost — see Deals.
//
// Coding Instructions:
//   The catalogue price and cost are DEFAULTS, not the deal. F&I is
//   negotiated: the same warranty goes out at different prices on different
//   deals, and the provider's cost changes when the term does. Copying both
//   onto the deal at the moment of sale is what lets last month's gross stay
//   correct after this month's price list arrives.
//
//   Cost is on the catalogue at all because a product sold without one has no
//   gross, and F&I gross is most of the point. It can be overridden per deal
//   and it must never be inferred.

using DealerFOSS.Core;

namespace DealerFOSS.Finance;

/// <summary>What kind of thing this is. Kept coarse — these are the groupings a
/// dealer principal actually reports on, not a provider's product taxonomy.</summary>
public enum FinanceProductKind
{
    /// <summary>An extended warranty or vehicle service contract.</summary>
    Warranty = 0,

    /// <summary>Covers the gap between what insurance pays out and what is owed.</summary>
    Gap = 1,

    /// <summary>Prepaid servicing.</summary>
    ServicePlan = 2,

    /// <summary>Paint, fabric, alloys, tyres — the physical add-ons.</summary>
    Protection = 3,

    /// <summary>Anything else, so an unusual product does not need a code change.</summary>
    Other = 4,
}

/// <summary>
/// A product the dealership can sell with a car. Organization-shared, like a part
/// number: the same warranty means the same thing at every lot, and what differs
/// between lots is what they sell it for.
/// </summary>
public sealed class FinanceProduct : AuditableEntity
{
    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public FinanceProductKind Kind { get; private set; }

    /// <summary>Whose product it is. Free text — a provider list is its own job.</summary>
    public string Provider { get; private set; } = string.Empty;

    /// <summary>What it usually goes out at. A starting point for the negotiation.</summary>
    public decimal DefaultPrice { get; private set; }

    /// <summary>What the provider usually charges the dealership for it.</summary>
    public decimal DefaultCost { get; private set; }

    public string Currency { get; private set; } = "USD";

    /// <summary>How long it runs, when that is a fixed part of the product.</summary>
    public int? TermMonths { get; private set; }

    public int? TermMiles { get; private set; }

    /// <summary>
    /// Withdrawn products stay in the catalogue rather than being deleted: deals
    /// already sold against them have to keep making sense.
    /// </summary>
    public bool IsAvailable { get; private set; } = true;

    private FinanceProduct()
    {
    }

    public FinanceProduct(
        Guid id,
        string name,
        FinanceProductKind kind,
        string provider,
        decimal defaultPrice,
        decimal defaultCost,
        string currency,
        int? termMonths,
        int? termMiles)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A product needs a name.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new ArgumentException(
                "A product needs a provider — 'who underwrites this' is the first question asked about it.",
                nameof(provider));
        }

        if (defaultPrice < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultPrice), "A price cannot be negative.");
        }

        if (defaultCost < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultCost), "A cost cannot be negative.");
        }

        Id = id;
        Name = name.Trim();
        Kind = kind;
        Provider = provider.Trim();
        DefaultPrice = defaultPrice;
        DefaultCost = defaultCost;
        Currency = Money.Zero(currency).Currency;
        TermMonths = termMonths;
        TermMiles = termMiles;
    }

    public void Withdraw() => IsAvailable = false;

    public void Restore() => IsAvailable = true;

    public void Reprice(decimal defaultPrice, decimal defaultCost)
    {
        if (defaultPrice < 0m || defaultCost < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultPrice), "Neither figure can be negative.");
        }

        // Only the defaults move. Deals already done copied their own figures at
        // the point of sale, so this cannot reach back and change last month's
        // gross — the same rule as a part's cost.
        DefaultPrice = defaultPrice;
        DefaultCost = defaultCost;
    }
}
