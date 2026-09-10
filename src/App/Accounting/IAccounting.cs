// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   IAccounting — what other capabilities may call to reach the ledger.
//
// Usage:
//   Deals posts a delivered sale through this. Nothing writes journal rows
//   any other way.
//
// Coding Instructions:
//   There is deliberately no "create an entry from these lines" method on
//   the public contract. An entry is the consequence of something that
//   happened in the business, so the contract names the events — a delivery,
//   a reversal — rather than offering a general-purpose posting hole.

using DealerFOSS.Core;

namespace DealerFOSS.Accounting;

public interface IAccounting
{
    /// <summary>The chart of accounts, for a screen to label amounts with.</summary>
    Task<Result<IReadOnlyList<AccountView>>> ListAccountsAsync(CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<JournalEntrySummary>>> ListAsync(
        JournalQuery query,
        CancellationToken cancellationToken);

    Task<Result<JournalEntryDetail>> GetAsync(Guid entryId, CancellationToken cancellationToken);

    /// <summary>
    /// What every account adds up to over a period. This is the report that turns
    /// a pile of entries into something somebody can act on, and its own totals
    /// are the check that nothing was lost on the way in.
    /// </summary>
    Task<Result<TrialBalance>> TrialBalanceAsync(BalanceQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// What each department made over a period, and how many transactions made
    /// it. Same reads as a trial balance, arranged the way a dealer principal
    /// asks the question rather than the way an accountant checks it.
    /// </summary>
    /// <remarks>
    /// This lives here, and not in whatever screen wants it, because working out
    /// gross profit means knowing that 4000 is a vehicle sale and 5000 is what it
    /// cost. Accounting owns the chart of accounts; a second place that also knew
    /// the codes would be a second place to get them wrong.
    /// </remarks>
    Task<Result<LedgerPerformance>> PerformanceAsync(BalanceQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Records the accounting consequence of a car leaving the lot. Called by
    /// Deals when a deal is delivered; it is not something a person does.
    /// </summary>
    Task<Result<JournalEntryDetail>> PostDeliveryAsync(
        DeliveryPosting delivery,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records the accounting consequence of a repair order being invoiced.
    /// Called by RepairOrders; it is not something a person does.
    /// </summary>
    Task<Result<JournalEntryDetail>> PostServiceInvoiceAsync(
        ServiceInvoicePosting invoice,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records the accounting consequence of a car being taken into stock.
    /// Called by Inventory when a unit is received with a cost; it is not
    /// something a person does.
    /// </summary>
    /// <remarks>
    /// This is the entry that did not exist until 2026-09-10, and its absence was
    /// not subtle: delivery credited 1300 for every car sold and nothing ever
    /// debited it, so a dealership that had sold thirty cars showed vehicle
    /// inventory at minus $993,190 — an asset account nearly a million dollars
    /// negative. The ledger balanced throughout, because both sides of every
    /// delivery were present; it was the purchase that had no entry at all.
    ///
    /// Found by walking a day at the dealership rather than by a test, because
    /// every test posted deliveries against stock that a seeder had placed
    /// directly into the table. See <c>docs/implementation/DEALER-DAY.md</c>.
    /// </remarks>
    Task<Result<JournalEntryDetail>> PostStockPurchaseAsync(
        StockPurchasePosting purchase,
        CancellationToken cancellationToken);

    /// <summary>
    /// Undoes a posted entry by posting its opposite. The original is untouched —
    /// that is the whole point.
    /// </summary>
    Task<Result<JournalEntryDetail>> ReverseAsync(
        Guid entryId,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>The organization's months, newest first, with their state.</summary>
    Task<Result<IReadOnlyList<AccountingPeriodView>>> ListPeriodsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Starts a month. Nothing may post into a month that has not been opened —
    /// the books have a deliberate beginning rather than one inferred from the
    /// first thing anybody typed.
    /// </summary>
    Task<Result<AccountingPeriodView>> OpenPeriodAsync(
        int year,
        int month,
        string? note,
        CancellationToken cancellationToken);

    /// <summary>
    /// Locks a month at the end of the close. Called when the reconciling and
    /// adjusting is done, not on a date.
    /// </summary>
    Task<Result<AccountingPeriodView>> ClosePeriodAsync(
        int year,
        int month,
        string? note,
        CancellationToken cancellationToken);

    /// <summary>
    /// Unlocks a closed month. Its own permission and a written reason, because
    /// a figure somebody has already reported is about to be able to move.
    /// </summary>
    Task<Result<AccountingPeriodView>> ReopenPeriodAsync(
        int year,
        int month,
        string reason,
        CancellationToken cancellationToken);
}

public sealed record AccountingPeriodView(
    Guid Id,
    int Year,
    int Month,
    string State,
    DateOnly StartsOn,

    /// <summary>The cutoff — the 30th or 31st, whichever this month has.</summary>
    DateOnly EndsOn,
    DateTimeOffset? ClosedAt,
    Guid? ClosedByUserId,

    /// <summary>How many entries are dated into it. What a manager checks before closing.</summary>
    int Entries,
    IReadOnlyList<AccountingPeriodChangeView> History);

public sealed record AccountingPeriodChangeView(
    string? FromState,
    string ToState,
    DateTimeOffset OccurredAt,
    Guid? ChangedByUserId,
    string? Note);

/// <summary>
/// Everything the ledger needs to record a delivery, stated in business terms so
/// the caller does not need to know which accounts move.
/// </summary>
public sealed record DeliveryPosting(
    RooftopId RooftopId,
    string Reference,
    string Currency,
    decimal VehiclePrice,
    decimal Fees,
    decimal Discount,
    decimal TradeAllowance,
    decimal TradePayoff,
    decimal AmountDue,
    decimal VehicleCost,

    /// <summary>
    /// What the F&amp;I products sold for, and what they cost the dealership. Kept
    /// out of <c>Fees</c> deliberately: warranty and GAP income is a different
    /// business from the car, with its own gross, and merging them would make the
    /// one figure a dealer principal most wants unreadable.
    /// </summary>
    decimal ProductRevenue,
    decimal ProductCost,

    /// <summary>
    /// Sales tax taken from the customer. Credited to a LIABILITY, never to
    /// revenue: the dealership is holding it for the state, not earning it.
    /// Included in <c>AmountDue</c>, which is why leaving it out here makes the
    /// entry fail to balance by exactly the tax.
    /// </summary>
    decimal TaxCollected,
    string Memo);

/// <summary>
/// Everything the ledger needs to record a car being bought into stock: which
/// lot it landed on, which car it is, and what it cost.
/// </summary>
/// <remarks>
/// <para>
/// <b>The other side is Cash, and that is a decision rather than an obvious
/// truth.</b> Most dealerships floorplan their stock — a lender pays for the car
/// and is repaid when it sells — which would make the credit a liability rather
/// than a reduction in cash. There is no floorplan account in the chart and no
/// decision recorded about one, so this posts the way every other entry in this
/// ledger already does: the dealership paid for it.
/// </para>
/// <para>
/// That is honest and wrong in the same way the rest of the system is wrong, and
/// it is deliberately not fixed here. Floorplan is a register row, and when it
/// lands this credit becomes a choice made per unit rather than a constant.
/// </para>
/// <para>
/// <see cref="Cost"/> must be positive. A car received without a cost does not
/// post at all — the caller skips this entirely — because a zero-value entry
/// would assert that the car was free, which is a different claim from not
/// knowing yet.
/// </para>
/// </remarks>
public sealed record StockPurchasePosting(
    RooftopId RooftopId,

    /// <summary>The stock number. One non-reversal entry per unit is the rule.</summary>
    string Reference,
    string Currency,
    decimal Cost,
    string Memo);

/// <summary>
/// Everything the ledger needs to record a service invoice, stated in business
/// terms so the caller does not need to know which accounts move.
/// </summary>
/// <remarks>
/// <para>
/// <c>PartsCost</c> closed the gap this remark used to name. Until parts were
/// real stock there was no honest cost figure, so service revenue posted and
/// gross profit on service did not exist. It does now: the cost is worked out by
/// the organization's chosen costing method at the moment of invoicing, and
/// frozen on the line.
/// </para>
/// <para>
/// Zero is a legitimate value and means something specific — nothing on this job
/// came off a shelf. A workshop selling only labour genuinely has no parts cost,
/// which is not the same as having an unknown one.
/// </para>
/// </remarks>
/// <remarks>
/// <para>
/// <b>Two different splits of the same money, and they must agree.</b>
/// <see cref="Labour"/>, <see cref="Parts"/> and <see cref="Sublet"/> divide the
/// work by WHAT was sold, and are credited to revenue. <see cref="AmountDue"/>,
/// <see cref="Warranty"/> and <see cref="Internal"/> divide the same work by WHO
/// settles it, and are debited. The two sides balance because they are the same
/// total counted twice — if they ever disagree the entry is refused rather than
/// posted, because a ledger that does not balance is worse than a missing entry.
/// </para>
/// </remarks>
public sealed record ServiceInvoicePosting(
    RooftopId RooftopId,
    string Reference,
    string Currency,
    decimal Labour,
    decimal Parts,
    decimal Sublet,
    /// <summary>The customer's share, and only theirs.</summary>
    decimal AmountDue,
    /// <summary>Owed by the manufacturer. Lands in a receivable, never in cash.</summary>
    decimal Warranty,
    /// <summary>The dealership's own work, charged to itself.</summary>
    decimal Internal,

    /// <summary>
    /// The part of <see cref="Internal"/> spent on a car this rooftop owns, which
    /// belongs in that car's cost rather than in an expense account. The
    /// remainder — work on a demo, a courtesy car, anything not in stock — stays
    /// a charge. Never greater than <see cref="Internal"/>.
    /// </summary>
    decimal InternalCapitalised,
    decimal PartsCost,
    string Memo);

public sealed record AccountView(Guid Id, string Code, string Name, string Kind);

public sealed record JournalEntrySummary(
    Guid Id,
    RooftopId RooftopId,
    DateOnly EntryDate,
    string Source,
    string Reference,
    string Memo,
    decimal Total,
    string Currency,
    bool IsReversal);

public sealed record JournalEntryDetail(
    Guid Id,
    LegalEntityId LegalEntityId,
    RooftopId RooftopId,
    DateOnly EntryDate,
    string Source,
    string Reference,
    string Memo,
    string Currency,
    decimal TotalDebits,
    decimal TotalCredits,
    DateTimeOffset PostedAt,
    Guid? PostedByUserId,
    Guid? ReversesEntryId,
    IReadOnlyList<JournalLineView> Lines);

public sealed record JournalLineView(
    string AccountCode,
    string AccountName,
    decimal Debit,
    decimal Credit,
    string? Memo);

/// <summary>Which entries a balance covers.</summary>
public sealed record BalanceQuery(
    RooftopId? RooftopId = null,
    DateOnly? From = null,
    DateOnly? To = null);

/// <summary>
/// Every account that moved in the period, and the proof that the two sides
/// agree. A trial balance whose totals differ means something was lost, and
/// saying so plainly is the entire purpose of the report.
/// </summary>
public sealed record TrialBalance(
    DateOnly? From,
    DateOnly? To,
    string Currency,
    decimal TotalDebits,
    decimal TotalCredits,
    bool Balances,
    IReadOnlyList<AccountBalance> Accounts);

/// <summary>
/// One account's activity. <paramref name="Balance"/> is stated on the account's
/// normal side, so an asset with more debits than credits reads positive — which
/// is how an accountant expects to see it.
/// </summary>
public sealed record AccountBalance(
    string Code,
    string Name,
    string Kind,
    decimal Debits,
    decimal Credits,
    decimal Balance);

/// <summary>
/// A period's trading, by department. Every figure here is derived from the same
/// journal lines a trial balance totals, so the two can never disagree — which is
/// the point of deriving a dashboard from the ledger rather than from the deals.
/// </summary>
public sealed record LedgerPerformance(
    DateOnly? From,
    DateOnly? To,
    string Currency,
    IReadOnlyList<DepartmentResult> Departments,
    decimal TotalRevenue,
    decimal TotalCost,
    decimal TotalGross,

    /// <summary>
    /// Cars that left the lot, counted from the deliveries posted in the period.
    /// A delivery reversed in this period is subtracted, so the count moves with
    /// the money it produced instead of drifting away from it.
    /// </summary>
    int VehiclesDelivered,

    /// <summary>Repair orders invoiced in the period, counted the same way.</summary>
    int ServiceInvoices);

/// <summary>
/// One department's revenue, cost, and the difference. Named rather than keyed by
/// account code: what a manager reads is "the cars made this much", and the codes
/// that produced it are the ledger's business.
/// </summary>
public sealed record DepartmentResult(
    string Name,
    decimal Revenue,
    decimal Cost,
    decimal Gross,

    /// <summary>
    /// Gross as a share of revenue, 0 to 1. Null when nothing was sold — a
    /// department with no revenue has no margin, and printing 0% would say
    /// something false about a month that simply has not started.
    /// </summary>
    decimal? Margin);

/// <summary>
/// The departments a dashboard reports, in the order a dealer reads them. Public
/// so a screen can label a figure without inventing its own spelling.
/// </summary>
public static class Departments
{
    /// <summary>Front-end gross — the car itself, its fees, less what it cost.</summary>
    public const string Vehicles = "Vehicles";

    /// <summary>Back-end gross — warranties and cover, less what the provider charges.</summary>
    public const string FinanceAndInsurance = "Finance and insurance";

    /// <summary>Labour, parts, and sublet, less what the parts cost off the shelf.</summary>
    public const string Service = "Service";
}

public sealed record JournalQuery(
    RooftopId? RooftopId = null,
    string? Reference = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Limit = 50);
