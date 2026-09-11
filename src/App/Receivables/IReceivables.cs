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
    Task<Result<IReadOnlyList<ReceivableSummary>>> ListAsync(
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
    /// Opens a debt. Called by Deals on delivery and RepairOrders on invoicing,
    /// inside their transaction; does not save.
    /// </summary>
    Task<Result<Receivable>> OpenAsync(NewReceivable receivable, CancellationToken cancellationToken);

    /// <summary>
    /// Records money arriving and posts it: cash up, receivable down. Owns its
    /// own transaction.
    /// </summary>
    Task<Result<ReceivableDetail>> RecordPaymentAsync(
        Guid receivableId,
        NewPayment payment,
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

/// <summary>How a caller narrows the list of what is owed.</summary>
public sealed record ReceivableQuery(
    RooftopId? RooftopId = null,
    Guid? CustomerId = null,

    /// <summary>
    /// Default true, because the question is almost always "who still owes us".
    /// Settled rows are history and are asked for deliberately.
    /// </summary>
    bool OutstandingOnly = true,
    int Limit = 50);

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
    IReadOnlyList<PaymentView> Payments);

public sealed record PaymentView(
    Guid Id,
    decimal Amount,
    string Currency,
    string Method,
    DateTimeOffset ReceivedAt,
    string? Note);
