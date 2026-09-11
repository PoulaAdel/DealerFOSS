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
    /// What customers owe the dealership. An asset: the work is done or the car
    /// is gone, and the money has not arrived yet.
    /// </summary>
    /// <remarks>
    /// Added 2026-09-10, and its absence was the reason nothing could be sold
    /// except for cash. Delivering a car and invoicing a repair order both
    /// debited 1000 Cash for the whole amount on the spot, so the books asserted
    /// that every customer paid in full the moment they were billed. A fleet
    /// customer on account, a deposit, a part-payment and a lender's settlement
    /// cheque were all unrepresentable, and the bank balance was wrong by
    /// everything anybody was still owed.
    ///
    /// Found by walking a day at the dealership: the service job reached
    /// Invoiced and there was nowhere to go. See
    /// <c>docs/implementation/DEALER-DAY.md</c>.
    /// </remarks>
    public const string AccountsReceivable = "1100";

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
    /// Stock financed by a lender rather than bought outright. A liability: the
    /// cars are on the lot and the money for them belongs to somebody else until
    /// each one sells.
    /// </summary>
    /// <remarks>
    /// Added 2026-09-11, and it exists because fixing one lie exposed another.
    /// Booking stock purchases (2026-09-10) made vehicle inventory a real asset
    /// for the first time and drove Cash to minus $2.5M, because the chart had no
    /// way for a dealership to pay for anything except out of a bank account that
    /// started at nothing. Most dealers floorplan: the lender pays the invoice and
    /// is repaid when the car sells.
    /// </remarks>
    public const string FloorplanPayable = "2000";

    /// <summary>
    /// What the owners put in. The other half of the same problem as
    /// <see cref="FloorplanPayable"/>: a business with no capital cannot buy
    /// anything, and a balance sheet with no equity section does not balance in
    /// any form a person would recognise.
    /// </summary>
    public const string OwnersCapital = "3000";

    // The expense accounts. Before 2026-09-11 there were NONE — not one — so the
    // system could record everything a dealership earned and nothing it spent,
    // and "what did the month make" could only ever be answered as gross. The
    // five below are the ones a dealer principal reads; a real installation will
    // want more, which is what makes replacing the chart a register row.

    /// <summary>Wages, salaries and commission.</summary>
    public const string Wages = "6000";

    /// <summary>Rent, rates and the cost of the premises.</summary>
    public const string Rent = "6100";

    /// <summary>Advertising and marketing.</summary>
    public const string Advertising = "6200";

    /// <summary>
    /// What the floorplan lender charges for carrying the stock. Its own line
    /// because it is the cost of a car standing still, and a used-car manager who
    /// cannot see it has no reason to move ageing stock.
    /// </summary>
    public const string FloorplanInterest = "6300";

    /// <summary>Everything else, until somebody needs it split.</summary>
    public const string OtherOperatingExpense = "6900";

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
        (AccountsReceivable, "Customer accounts receivable", AccountKind.Asset),
        (WarrantyReceivable, "Warranty claims receivable", AccountKind.Asset),
        (VehicleInventory, "Vehicle inventory", AccountKind.Asset),
        (TradeInventory, "Trade-in inventory", AccountKind.Asset),
        (PartsInventory, "Parts inventory", AccountKind.Asset),
        (FloorplanPayable, "Floorplan payable", AccountKind.Liability),
        (SalesTaxPayable, "Sales tax payable", AccountKind.Liability),
        (OwnersCapital, "Owners' capital", AccountKind.Equity),
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
        (Wages, "Wages and salaries", AccountKind.Expense),
        (Rent, "Rent and premises", AccountKind.Expense),
        (Advertising, "Advertising", AccountKind.Expense),
        (FloorplanInterest, "Floorplan interest", AccountKind.Expense),
        (OtherOperatingExpense, "Other operating expenses", AccountKind.Expense),
    ];

    /// <summary>
    /// The expense accounts already counted INSIDE a department's gross. A profit
    /// and loss must not take these off a second time below the line.
    /// </summary>
    /// <remarks>
    /// Subtracting them twice reads as a plausible net profit roughly a million
    /// dollars too low, which is exactly the kind of wrong number nobody
    /// questions.
    /// </remarks>
    public static IReadOnlyList<string> CostOfSales { get; } =
    [
        CostOfVehicleSales, CostOfPartsSales, CostOfFinanceProducts,
    ];

    /// <summary>
    /// Whether an expense account belongs below the gross line — what it costs to
    /// run the business, as opposed to what the things sold cost to buy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Asked as a question about the chart rather than answered by a fixed list,
    /// and that is the second version. The first named the five 6xxx accounts
    /// explicitly, which double-counted nothing and quietly LOST something else:
    /// 5400 Internal service charge is an expense, is not a department's cost of
    /// sales, and so appeared on no part of the profit and loss at all. The
    /// seeded dealership had $1,196 in it and the report simply did not mention
    /// it — money spent into an account that showed on no report.
    /// </para>
    /// <para>
    /// Found by adding up the two reports by hand and noticing they disagreed by
    /// $663.60. Phrased this way, a new expense account is on the report the day
    /// somebody adds it, and the only way to keep one off is to name it as a cost
    /// of sales deliberately.
    /// </para>
    /// </remarks>
    public static bool IsOperatingExpense(string code, AccountKind kind) =>
        kind == AccountKind.Expense && !CostOfSales.Contains(code);
}
