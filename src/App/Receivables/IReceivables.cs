// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   IReceivables — what the rest of the application may call to reach the
//   customer sub-ledger: who owes what, and money arriving against it.
//
// Usage:
//   Inject IReceivables. Nothing outside this folder touches Receivable,
//   Payment, or the receivables schema (ADR-014).
//
// Coding Instructions:
//   OpenAsync is called INSIDE the caller's transaction and does NOT save, the
//   same contract IParts.IssueAsync uses. Delivering a car and invoicing a job
//   both post to the ledger and open a receivable, and those two facts must
//   land together or not at all — a bill in the ledger that nobody is recorded
//   as owing is exactly the kind of half-state this capability exists to end.
//
//   RecordPaymentAsync is the exception: it is a whole operation of its own, so
//   it owns its transaction and posts the ledger entry itself.

using DealerFOSS.Core;

namespace DealerFOSS.Receivables;

public interface IReceivables
{
    /// <summary>Who owes what, newest first, scoped to the caller's rooftops.</summary>
    Task<Result<Page<ReceivableSummary>>> ListAsync(
        ReceivableQuery query,
        CancellationToken cancellationToken);

    Task<Result<ReceivableDetail>> GetAsync(Guid receivableId, CancellationToken cancellationToken);

    /// <summary>
    /// The receivable for one billed thing, or null if there is none. Lets a deal
    /// desk or a job sheet show what is still owed without knowing an id.
    /// </summary>
    Task<Result<ReceivableDetail?>> FindAsync(
        ReceivableSource source,
        string reference,
        CancellationToken cancellationToken);

    /// <summary>
    /// Who owes what, split by how long it has been owed — current, then three
    /// overdue bands. Scoped to the caller's rooftops, like <see cref="ListAsync"/>.
    /// </summary>
    Task<Result<AgeingReport>> GetAgeingAsync(AgeingQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// One customer's bills and payments over a period, with the balance
    /// running through them — what a dealership hands somebody who asks
    /// "what do I owe you".
    /// </summary>
    Task<Result<CustomerStatement>> GetStatementAsync(
        StatementQuery query,
        CancellationToken cancellationToken);

    /// <summary>
    /// Opens a debt. Called by Deals on delivery and RepairOrders on invoicing,
    /// inside their transaction; does not save.
    /// </summary>
    Task<Result<Receivable>> OpenAsync(NewReceivable receivable, CancellationToken cancellationToken);

    /// <summary>
    /// Raises a credit for a reason other than an overpayment — today, a
    /// cancelled F&amp;I product. Called inside the caller's transaction; does
    /// not save. The same contract as <see cref="OpenAsync"/>: the caller has
    /// already authorized the event this credit comes from.
    /// </summary>
    Task<Result<CustomerCredit>> RaiseCreditAsync(NewCredit credit, CancellationToken cancellationToken);

    /// <summary>
    /// Records money arriving and posts it: cash up, receivable down. Owns its
    /// own transaction.
    /// </summary>
    /// <remarks>
    /// More than is owed is <b>absorbed, not refused</b>, since 2026-09-14. The
    /// bill's share settles it and the rest becomes a
    /// <see cref="CustomerCredit"/> the dealership owes back. A customer paying
    /// a $1,340.50 invoice with $1,400 in cash has not made a mistake.
    /// </remarks>
    Task<Result<ReceivableDetail>> RecordPaymentAsync(
        Guid receivableId,
        NewPayment payment,
        CancellationToken cancellationToken);

    /// <summary>
    /// Credits the dealership is holding, newest first, scoped to the caller's
    /// rooftops. Open ones only unless asked otherwise.
    /// </summary>
    Task<Result<Page<CreditSummary>>> ListCreditsAsync(
        CreditQuery query,
        CancellationToken cancellationToken);

    /// <summary>
    /// Puts a credit against another bill the same customer owes. Owns its own
    /// transaction; posts 2200 down, 1100 down, and no cash moves.
    /// </summary>
    Task<Result<CreditSummary>> ApplyCreditAsync(
        Guid creditId,
        ApplyCredit application,
        CancellationToken cancellationToken);

    /// <summary>
    /// Hands a credit back to the customer. Owns its own transaction; posts 2200
    /// down and 1000 down.
    /// </summary>
    /// <remarks>
    /// Needs <c>Accounting.Refund</c>, which taking a payment does not — see
    /// the permission's own documentation for why they are separate.
    /// </remarks>
    Task<Result<CreditSummary>> RefundCreditAsync(
        Guid creditId,
        RefundCredit refund,
        CancellationToken cancellationToken);
}

/// <summary>What a caller supplies to open a debt.</summary>
public sealed record NewReceivable(
    RooftopId RooftopId,
    Guid CustomerId,
    ReceivableSource Source,
    string Reference,
    decimal Amount,
    string Currency,
    DateTimeOffset BilledAt);

/// <summary>What a caller supplies to record money arriving.</summary>
public sealed record NewPayment(
    decimal Amount,
    string Method,
    string? Note = null);

/// <summary>What a caller supplies to raise a credit that did not come from an overpayment.</summary>
public sealed record NewCredit(
    RooftopId RooftopId,
    Guid CustomerId,
    decimal Amount,
    string Currency,

    /// <summary>What this credit came from, so a customer asking can be told.</summary>
    string Reference,
    DateTimeOffset RaisedAt);

/// <summary>How a caller narrows the list of what is owed.</summary>
public sealed record ReceivableQuery(
    RooftopId? RooftopId = null,
    Guid? CustomerId = null,

    /// <summary>
    /// Default true, because the question is almost always "who still owes us".
    /// Settled rows are history and are asked for deliberately.
    /// </summary>
    bool OutstandingOnly = true,
    int Limit = 50,
    int Offset = 0);

public sealed record ReceivableSummary(
    Guid Id,
    RooftopId RooftopId,
    Guid CustomerId,
    string CustomerName,
    string Source,
    string Reference,
    decimal Amount,
    decimal Paid,
    decimal Outstanding,
    string Currency,
    DateTimeOffset BilledAt,

    /// <summary>How long the money has been owed. What an ageing report is built on.</summary>
    int DaysOutstanding,
    bool IsSettled);

public sealed record ReceivableDetail(
    Guid Id,
    RooftopId RooftopId,
    Guid CustomerId,
    string CustomerName,
    string Source,
    string Reference,
    decimal Amount,
    decimal Paid,
    decimal Outstanding,
    string Currency,
    DateTimeOffset BilledAt,
    int DaysOutstanding,
    bool IsSettled,
    IReadOnlyList<PaymentView> Payments,

    /// <summary>
    /// Credits this bill produced, because somebody paid more than it asked for.
    /// Almost always empty.
    /// </summary>
    /// <remarks>
    /// Carried on the bill rather than looked up separately so the counter can
    /// say "that is settled, and $59.50 is on their account" in the same breath
    /// as taking the money. A credit found ten minutes later on another screen
    /// is a credit the customer has already left without.
    /// </remarks>
    IReadOnlyList<CreditSummary> CreditsRaised);

public sealed record PaymentView(
    Guid Id,
    decimal Amount,
    string Currency,
    string Method,
    DateTimeOffset ReceivedAt,
    string? Note);

/// <summary>How a caller narrows the list of credits the dealership is holding.</summary>
public sealed record CreditQuery(
    RooftopId? RooftopId = null,
    Guid? CustomerId = null,

    /// <summary>
    /// Default true. "What do we still owe people" is the question; a credit
    /// fully spent is history and is asked for deliberately.
    /// </summary>
    bool OpenOnly = true,
    int Limit = 50,
    int Offset = 0);

/// <summary>What a caller supplies to put a credit against a bill.</summary>
public sealed record ApplyCredit(Guid ReceivableId, decimal Amount, string? Note = null);

/// <summary>What a caller supplies to hand a credit back.</summary>
public sealed record RefundCredit(decimal Amount, string Method, string? Note = null);

public sealed record CreditSummary(
    Guid Id,
    RooftopId RooftopId,
    Guid CustomerId,
    string CustomerName,

    /// <summary>What was overpaid.</summary>
    decimal Amount,

    /// <summary>What has since been applied or handed back.</summary>
    decimal Spent,

    /// <summary>What the dealership still owes out of this credit.</summary>
    decimal Remaining,
    string Currency,

    /// <summary>The bill that was overpaid.</summary>
    string Reference,
    DateTimeOffset RaisedAt,
    bool IsSpent,
    IReadOnlyList<CreditUseView> Uses);

public sealed record CreditUseView(
    Guid Id,
    decimal Amount,
    string Currency,
    string Kind,
    Guid? ReceivableId,
    DateTimeOffset UsedAt,
    string? Note);

/// <summary>How a caller narrows the ageing report.</summary>
public sealed record AgeingQuery(RooftopId? RooftopId = null);

/// <summary>
/// What every customer with an outstanding bill owes, split by age, oldest
/// debt first.
/// </summary>
public sealed record AgeingReport(
    string Currency,
    IReadOnlyList<CustomerAgeing> Customers,
    AgeingBucket Totals);

public sealed record CustomerAgeing(
    Guid CustomerId,
    string CustomerName,
    AgeingBucket Bucket);

/// <summary>
/// What is owed, split by how long it has been owed. The bands match what a
/// dealership actually chases differently: current is not yet a concern,
/// 31-60 gets a call, 61-90 gets a harder one, and over 90 is what a manager
/// asks about by name.
/// </summary>
public sealed record AgeingBucket(
    decimal Current,
    decimal Days31To60,
    decimal Days61To90,
    decimal Over90,
    decimal Total);

/// <summary>What a caller supplies to ask for one customer's statement.</summary>
public sealed record StatementQuery(
    Guid CustomerId,
    DateTimeOffset From,
    DateTimeOffset To,
    RooftopId? RooftopId = null);

/// <summary>
/// One customer's account over a period: what they owed coming in, every bill
/// and payment across it, and what they owe going out.
/// </summary>
public sealed record CustomerStatement(
    Guid CustomerId,
    string CustomerName,
    string Currency,
    DateTimeOffset From,
    DateTimeOffset To,
    decimal OpeningBalance,
    decimal ClosingBalance,
    IReadOnlyList<StatementLine> Lines);

/// <summary>
/// One bill or one payment, in date order, with the balance after it. Amount is
/// signed by <see cref="Kind"/> rather than carrying separate debit/credit
/// columns — a statement has one number moving, not two.
/// </summary>
/// <param name="Kind">"Invoice" (raises the balance) or "Payment" (lowers it).</param>
public sealed record StatementLine(
    DateTimeOffset Date,
    string Kind,
    string Reference,
    decimal Amount,
    decimal Balance);
