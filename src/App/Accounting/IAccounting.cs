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
//   THE CONTRACT NAMES EVENTS, NOT ENTRIES. A delivery, an invoice, a stock
//   purchase, a payment: each is something that happened in the business, and
//   the caller says what happened rather than which accounts to move. Keep it
//   that way — a capability that knows account codes is a second place to get
//   them wrong, and Accounting owns the chart.
//
//   PostManualAsync is the one exception, added 2026-09-11, and this note used
//   to say there would never be one. The reason it changed: a dealership has
//   overheads. Wages, rent, advertising and floorplan interest are not the
//   consequence of anything this system models, and until there was a way to
//   record them the chart held no expense account at all and "what did the
//   month make" could only be answered as gross. The same method is how a new
//   installation states what it already owned on day one.
//
//   It is fenced rather than open: its own permission (Accounting.ManualEntry,
//   which a salesperson does not hold even though they hold Accounting.Post),
//   the period check every other posting gets, and JournalEntry.Post still
//   refusing anything that does not balance. Do not add a second general-purpose
//   hole; if a new business event needs posting, name the event.

using DealerFOSS.Core;

namespace DealerFOSS.Accounting;

public interface IAccounting
{
    /// <summary>The chart of accounts, for a screen to label amounts with.</summary>
    Task<Result<IReadOnlyList<AccountView>>> ListAccountsAsync(CancellationToken cancellationToken);

    Task<Result<Page<JournalEntrySummary>>> ListAsync(
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
    /// Records money arriving against something already billed: cash up,
    /// receivable down. Called by Receivables; it is not something a person does
    /// directly.
    /// </summary>
    /// <remarks>
    /// Unlike a delivery or an invoice, this has NO already-posted guard. Several
    /// payments against one bill is the ordinary case — a deposit and a balance,
    /// or a customer paying in instalments — so the reference is deliberately not
    /// unique. What stops a payment being taken twice is the receivable itself,
    /// which refuses more than is outstanding.
    /// </remarks>
    Task<Result<JournalEntryDetail>> PostPaymentAsync(
        PaymentPosting payment,
        CancellationToken cancellationToken);

    /// <summary>
    /// Puts a credit the dealership is holding against a bill: 2200 down,
    /// 1100 down. Called inside the sub-ledger's transaction; does not save.
    /// </summary>
    Task<Result<JournalEntryDetail>> PostCreditApplicationAsync(
        CreditApplicationPosting application,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gives a credit back: 2200 down, 1000 down. Called inside the sub-ledger's
    /// transaction; does not save.
    /// </summary>
    /// <remarks>
    /// Guarded by <see cref="Permissions.AccountingRefund"/> rather than
    /// <see cref="Permissions.AccountingPost"/>. Everything else in this
    /// interface records money the business earned or owes; this one takes money
    /// out of the till for a customer, which is the classic way a retail
    /// business is quietly stolen from.
    /// </remarks>
    Task<Result<JournalEntryDetail>> PostCreditRefundAsync(
        CreditRefundPosting refund,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records an F&amp;I product sold on a delivered deal being cancelled:
    /// reverses the revenue already recognized and raises what is owed back to
    /// the customer as a liability (2200), the same account an overpayment
    /// uses. Called inside the deal's transaction; does not save.
    /// </summary>
    Task<Result<JournalEntryDetail>> PostProductCancellationAsync(
        ProductCancellationPosting cancellation,
        CancellationToken cancellationToken);

    /// <summary>
    /// What the business made over a period: revenue and cost of sales by
    /// department, then what it costs to run the place, then the difference.
    /// </summary>
    /// <remarks>
    /// The report a dealer principal actually reads, and the one this system
    /// could not produce until 2026-09-11 because the chart contained no expense
    /// account of any kind. A trial balance is a bookkeeping instrument; this is
    /// a management report, and they are not the same thing.
    /// </remarks>
    Task<Result<ProfitAndLoss>> ProfitAndLossAsync(BalanceQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// What the business owns and owes, as at a date. Assets on one side;
    /// liabilities, what the owners put in, and what has been earned since, on the
    /// other.
    /// </summary>
    /// <remarks>
    /// Always cumulative from the first entry ever posted, whatever
    /// <see cref="BalanceQuery.From"/> says — a balance sheet is a position, not a
    /// period, and one built from a single month's entries would be nonsense.
    /// Only <see cref="BalanceQuery.To"/> is honoured, as the date it is "as at".
    /// </remarks>
    Task<Result<BalanceSheet>> BalanceSheetAsync(BalanceQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Writes an entry by hand — an expense, an opening balance, a correction.
    /// Its own permission, because choosing the accounts and the amounts is the
    /// most powerful thing anybody can do to a set of books.
    /// </summary>
    Task<Result<JournalEntryDetail>> PostManualAsync(
        ManualPosting entry,
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
/// <b>What is credited is a choice, made per car.</b> Most dealerships floorplan
/// their stock — a lender pays for the car and is repaid when it sells — and
/// some cars are bought outright. Both are ordinary, so the person taking the car
/// in says which, and <see cref="Floorplanned"/> decides between 2000 Floorplan
/// payable and 1000 Cash.
/// </para>
/// <para>
/// Until 2026-09-11 it was always Cash, because there was no floorplan account.
/// That produced a dealership with $3.7M of stock and a bank balance of minus
/// $2.5M — arithmetically correct and a description of nothing real.
/// </para>
/// <para>
/// <see cref="Cost"/> must be positive. A car received without a cost does not
/// post at all — the caller skips this entirely — because a zero-value entry
/// would assert that the car was free, which is a different claim from not
/// knowing yet.
/// </para>
/// </remarks>
/// <summary>
/// Everything the ledger needs to record money arriving against a bill: which
/// lot took it, what it was against, and how much.
/// </summary>
/// <remarks>
/// It says nothing about HOW the money arrived. Cash, card, transfer and a
/// lender's settlement all debit 1000 here, because this ledger has one bank
/// account and no merchant settlement. The method is recorded on the payment in
/// the sub-ledger, where it is a fact about the transaction rather than a
/// routing instruction — nothing in this system talks to a card terminal.
/// </remarks>
public sealed record PaymentPosting(
    RooftopId RooftopId,

    /// <summary>The bill this settles, so the debit and its payments read together.</summary>
    string Reference,
    string Currency,
    decimal Amount,
    string Memo,

    /// <summary>
    /// The part of the money handed over that was MORE than the bill, and is now
    /// owed back to the customer. Credits 2200 rather than 1100.
    /// </summary>
    /// <remarks>
    /// <b><see cref="Amount"/> keeps its original meaning</b> — what came off
    /// what they owed — and the cash debit is the two added together. It is
    /// written this way round on purpose: every existing caller means "this much
    /// came off the bill", and quietly redefining that to "this much was handed
    /// over" would have changed what four callers post without changing a line
    /// of their code.
    ///
    /// One entry, not two, because the customer performed one act. A person
    /// reading the journal sees $1,400 arrive, $1,340.50 clear the invoice and
    /// $59.50 become a liability, in one balanced entry with one date.
    /// </remarks>
    decimal CreditRaised = 0m);

/// <summary>
/// A credit the dealership holds, put against a bill the same customer owes.
/// </summary>
/// <remarks>
/// No cash line, and that is the whole character of it: the money arrived when
/// the overpayment was taken. This entry only moves it from "we owe this back"
/// to "this paid for something".
/// </remarks>
public sealed record CreditApplicationPosting(
    RooftopId RooftopId,

    /// <summary>The bill being settled, so this reads beside its other payments.</summary>
    string Reference,
    string Currency,
    decimal Amount,
    string Memo);

/// <summary>A credit handed back to the customer: the liability goes, the cash goes.</summary>
public sealed record CreditRefundPosting(
    RooftopId RooftopId,
    string Reference,
    string Currency,
    decimal Amount,
    string Memo);

public sealed record ProductCancellationPosting(
    RooftopId RooftopId,

    /// <summary>The deal's own id, so this reads beside the delivery it partly undoes.</summary>
    string Reference,
    string Currency,
    decimal RefundAmount,
    string Memo);

public sealed record StockPurchasePosting(
    RooftopId RooftopId,

    /// <summary>The stock number. One non-reversal entry per unit is the rule.</summary>
    string Reference,
    string Currency,
    decimal Cost,
    string Memo,

    /// <summary>
    /// True when a lender paid for the car and will be repaid when it sells.
    /// False when the dealership bought it outright.
    /// </summary>
    bool Floorplanned = false);

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
    int Limit = 50,
    int Offset = 0);

/// <summary>
/// What a person supplies to write an entry by hand. The lines are theirs to
/// choose, which is what makes this the one operation with its own permission.
/// </summary>
/// <remarks>
/// <see cref="JournalEntry.Post"/> still refuses anything that does not balance,
/// so the worst a mistake can do is be wrong rather than be impossible to read.
/// </remarks>
public sealed record ManualPosting(
    RooftopId RooftopId,
    DateOnly EntryDate,
    string Memo,
    string Currency,
    IReadOnlyList<ManualLine> Lines);

/// <summary>One line of a hand-written entry: an account code, and one side.</summary>
public sealed record ManualLine(string AccountCode, decimal Debit, decimal Credit, string? Memo);

/// <summary>
/// A profit and loss. Departmental gross comes first because that is how a
/// dealership is run; overheads and net profit follow.
/// </summary>
public sealed record ProfitAndLoss(
    DateOnly? From,
    DateOnly? To,
    string Currency,
    IReadOnlyList<DepartmentResult> Departments,
    decimal TotalRevenue,
    decimal TotalCost,

    /// <summary>Revenue less what the things sold cost. The dealer's daily number.</summary>
    decimal GrossProfit,

    IReadOnlyList<ExpenseLine> Expenses,
    decimal TotalExpenses,

    /// <summary>
    /// Gross less overheads. The figure this system could not produce at all
    /// before 2026-09-11, because it had nowhere to record an overhead.
    /// </summary>
    decimal NetProfit);

/// <summary>One overhead account and what it came to over the period.</summary>
public sealed record ExpenseLine(string Code, string Name, decimal Amount);

/// <summary>
/// What the business owns and owes, as at a date.
/// </summary>
/// <remarks>
/// <see cref="Balances"/> is not decoration. Assets must equal liabilities plus
/// equity plus what has been earned; if they ever do not, something has been
/// posted that this report does not know how to classify, and saying so is more
/// useful than printing a plausible page with a hole in it.
/// </remarks>
public sealed record BalanceSheet(
    DateOnly? AsAt,
    string Currency,
    IReadOnlyList<AccountBalance> Assets,
    IReadOnlyList<AccountBalance> Liabilities,
    IReadOnlyList<AccountBalance> Equity,
    decimal TotalAssets,
    decimal TotalLiabilities,

    /// <summary>What the owners put in, from the equity accounts alone.</summary>
    decimal TotalEquity,

    /// <summary>
    /// Revenue less every expense, for all time. Shown as its own line rather
    /// than folded into equity, because there is no year-end close in this system
    /// yet and pretending earnings had been transferred to capital would be a
    /// claim about a process nobody has run.
    /// </summary>
    decimal EarningsToDate,

    bool Balances);
