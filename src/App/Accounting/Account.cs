// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Account — one line of the chart of accounts.
//
// Usage:
//   Looked up by code when posting. The chart is seeded, not created by
//   users, until account administration lands.
//
// Coding Instructions:
//   The account kind decides which side increases it, and every report ever
//   written depends on that being right. Do not add a kind without deciding
//   its normal balance.

using DealerFOSS.Core;

namespace DealerFOSS.Accounting;

public sealed class Account : AuditableEntity
{
    public Guid Id { get; private set; }

    /// <summary>Stable numeric code — "1000". What accountants actually use.</summary>
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public AccountKind Kind { get; private set; }

    public bool IsActive { get; private set; } = true;

    /// <summary>Which side increases this account. Assets and expenses are debits.</summary>
    public bool IncreasesOnDebit =>
        Kind is AccountKind.Asset or AccountKind.Expense;

    private Account()
    {
    }

    public Account(Guid id, string code, string name, AccountKind kind)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("An account needs a code.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("An account needs a name.", nameof(name));
        }

        Id = id;
        Code = code.Trim();
        Name = name.Trim();
        Kind = kind;
    }

    public void Deactivate() => IsActive = false;
}

public enum AccountKind
{
    Asset = 0,
    Liability = 1,
    Equity = 2,
    Revenue = 3,
    Expense = 4,
}

/// <summary>
/// The accounts this slice posts to. Codes are conventional retail-dealer
/// numbering; a real installation will replace the chart, which is why nothing
/// outside this file hard-codes anything but these constants.
/// </summary>
public static class AccountCodes
{
    public const string Cash = "1000";
    public const string VehicleInventory = "1300";
    public const string TradeInventory = "1310";
    public const string VehicleSalesRevenue = "4000";
    public const string FeeRevenue = "4100";

    /// <summary>Time sold in the workshop.</summary>
    public const string LabourRevenue = "4200";

    /// <summary>Parts sold on a repair order.</summary>
    public const string PartsRevenue = "4300";

    /// <summary>Work sent out to another business and billed on.</summary>
    public const string SubletRevenue = "4400";

    public const string SalesDiscounts = "4900";
    public const string CostOfVehicleSales = "5000";

    /// <summary>
    /// What the parts sold on a repair order cost. Paired with
    /// <see cref="PartsInventory"/>: selling a part moves its value off the shelf
    /// and into cost of sales, which is what turns service revenue into a service
    /// profit figure.
    /// </summary>
    public const string CostOfPartsSales = "5300";

    /// <summary>
    /// The value of parts sitting on the shelf. An asset, like vehicle stock —
    /// the dealership owns it until it is sold.
    /// </summary>
    public const string PartsInventory = "1400";

    /// <summary>
    /// Warranties, GAP, service plans sold with a car. Its own revenue line
    /// because F&amp;I is reported as a separate business from the vehicle.
    /// </summary>
    public const string FinanceProductRevenue = "4500";

    /// <summary>What those products cost the dealership — what the provider charges.</summary>
    public const string CostOfFinanceProducts = "5500";

    /// <summary>
    /// Warranty work done and not yet paid for by the manufacturer. An asset: the
    /// work is finished and the money is owed, but a claim has to be submitted and
    /// accepted before any of it arrives — and some of it never will.
    ///
    /// Keeping it out of Cash is the whole point. A workshop that books warranty
    /// as cash on the day the car leaves shows money it has not got, and nobody
    /// notices the claims nobody submitted.
    /// </summary>
    public const string WarrantyReceivable = "1200";

    /// <summary>
    /// Work the dealership did for itself — reconditioning its own stock, demos,
    /// company vehicles. The workshop is still credited with the sale, so its
    /// people are measured on the work they actually did; this account carries the
    /// matching charge, so the two net to nothing at the dealership level.
    ///
    /// <para>
    /// <b>Deliberately not capitalised onto the vehicle.</b> Reconditioning a used
    /// car properly belongs in that car's cost, which is what stops used-vehicle
    /// gross from flattering itself. Doing that means the workshop reaching into
    /// stock to find the unit, and it is a decision the maintainer has not made
    /// (doc 11, D4). This account is the honest interim: the charge lands
    /// somewhere real and visible rather than on the customer.
    /// </para>
    /// </summary>
    public const string InternalServiceCharge = "5400";

    /// <summary>
    /// Sales tax taken from a customer and owed to the state. A LIABILITY, and
    /// getting that wrong is not a presentation detail: the dealership never owns
    /// this money, it collects it on somebody else's behalf and remits it. Booking
    /// it as revenue would inflate the top line by the tax on every car and make
    /// the return that eventually falls due look like a loss.
    /// </summary>
    /// <remarks>
    /// Added 2026-09-09 with the first account of its kind in this chart. The
    /// delivery posting debited the full amount due — tax included, since tax
    /// landed on the deal that morning — and credited nothing against it, so a
    /// deal carrying tax could not be delivered at all. Found by seeding a
    /// dealership's worth of data and watching every delivery refuse.
    /// </remarks>
    public const string SalesTaxPayable = "2100";

    /// <summary>
    /// The chart every dealership starts with, in one place.
    ///
    /// It used to be written out twice — once in the development seeder and once
    /// in tenant provisioning — and the two drifted the moment an account was
    /// added: warranty and internal service went into the development copy, and
    /// the next real dealership provisioned without them could not invoice a
    /// repair order at all. The failure surfaced as "the chart of accounts is
    /// missing 1200, 5400" on somebody's first day.
    ///
    /// The roles catalogue learned this lesson already and is shared for exactly
    /// the same reason: the version a paying dealership receives must be the
    /// version the tests exercise.
    /// </summary>
    public static IReadOnlyList<(string Code, string Name, AccountKind Kind)> Standard { get; } =
    [
        (Cash, "Cash", AccountKind.Asset),
        (WarrantyReceivable, "Warranty claims receivable", AccountKind.Asset),
        (VehicleInventory, "Vehicle inventory", AccountKind.Asset),
        (TradeInventory, "Trade-in inventory", AccountKind.Asset),
        (PartsInventory, "Parts inventory", AccountKind.Asset),
        (SalesTaxPayable, "Sales tax payable", AccountKind.Liability),
        (VehicleSalesRevenue, "Vehicle sales", AccountKind.Revenue),
        (FeeRevenue, "Fee income", AccountKind.Revenue),
        (LabourRevenue, "Labour sales", AccountKind.Revenue),
        (PartsRevenue, "Parts sales", AccountKind.Revenue),
        (SubletRevenue, "Sublet sales", AccountKind.Revenue),
        (FinanceProductRevenue, "Finance product sales", AccountKind.Revenue),
        (SalesDiscounts, "Sales discounts", AccountKind.Revenue),
        (CostOfVehicleSales, "Cost of vehicle sales", AccountKind.Expense),
        (CostOfPartsSales, "Cost of parts sales", AccountKind.Expense),
        (InternalServiceCharge, "Internal service charge", AccountKind.Expense),
        (CostOfFinanceProducts, "Cost of finance products", AccountKind.Expense),
    ];
}
