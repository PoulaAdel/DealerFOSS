// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ReceivableService — the customer sub-ledger, and the rooftop scope on it.
//
// Usage:
//   Through IReceivables.
//
// Coding Instructions:
//   THE SUB-LEDGER AND THE LEDGER MUST AGREE. Every row opened here has a
//   matching debit to 1100 posted by whoever opened it, and every payment
//   recorded here posts its own entry moving 1100 to 1000. If a change here
//   ever stops posting, the trial balance and "who owes us" drift apart
//   silently, and the first person to notice is an auditor. LedgerTests asserts
//   the arithmetic; keep it that way.
//
//   OpenAsync does NOT save. It is called inside the caller's transaction, the
//   same contract IParts.IssueAsync uses, so the bill and the debt commit
//   together.
//
//   Rooftop scope is enforced here and not in the endpoints, so a background
//   caller gets the same check.

using Microsoft.EntityFrameworkCore;
using DealerFOSS.Accounting;
using DealerFOSS.Core;
using DealerFOSS.Customers;
using DealerFOSS.Data;
using DealerFOSS.Identity;

namespace DealerFOSS.Receivables;

public sealed class ReceivableService(
    TenantDb db,
    IAccessDirectory access,
    ICurrentUser currentUser,
    ICustomers customers,
    IAccounting accounting,
    IAuditSink audit,
    IClock clock)
    : IReceivables
{
    private const string ReadPermission = Permissions.AccountingRead;

    /// <summary>
    /// Taking money is posting: it moves 1100 to 1000. Whoever may finish a sale
    /// or invoice a job already holds this, which is the point — the person who
    /// hands over the keys is the person who takes the cheque.
    /// </summary>
    private const string PostPermission = Permissions.AccountingPost;

    private const int MaxResults = 200;

    private readonly TenantDb _db = db;
    private readonly IAccessDirectory _access = access;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICustomers _customers = customers;
    private readonly IAccounting _accounting = accounting;
    private readonly IAuditSink _audit = audit;
    private readonly IClock _clock = clock;

    public async Task<Result<Page<ReceivableSummary>>> ListAsync(
        ReceivableQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<Page<ReceivableSummary>>(ReceivableErrors.Forbidden);
        }

        if (query.RooftopId is { } requested && !scope.Covers(requested))
        {
            return Result.Failure<Page<ReceivableSummary>>(ReceivableErrors.Forbidden);
        }

        var take = Paging.Limit(query.Limit);
        var skip = Paging.Offset(query.Offset);
        var rows = _db.Receivables.AsNoTracking().Include(r => r.Payments).AsQueryable();

        if (!scope.IsOrganizationWide)
        {
            var allowed = scope.Rooftops.ToList();
            rows = rows.Where(r => allowed.Contains(r.RooftopId));
        }

        if (query.RooftopId is { } only)
        {
            rows = rows.Where(r => r.RooftopId == only);
        }

        if (query.CustomerId is { } customer)
        {
            rows = rows.Where(r => r.CustomerId == customer);
        }

        // OUTSTANDING IS NOW A WHERE, AND IT HAS TO BE. It is still derived from
        // the payments rather than stored — nobody may write a balance — but it
        // is expressed as a correlated sum so the database applies it. This used
        // to filter in memory: take MaxResults rows, drop the settled ones, then
        // take the page. That cannot be paged at all. Skipping 50 rows means
        // skipping 50 rows the filter has not seen yet, so page two started in
        // the wrong place and a total counted over the unfiltered set would have
        // promised rows that do not exist.
        //
        // Overpayment is refused when a payment is taken, so outstanding is
        // never negative and "not settled" is exactly "paid less than billed".
        if (query.OutstandingOnly)
        {
            rows = rows.Where(r => r.Payments.Sum(p => p.Amount) < r.Amount);
        }

        var total = await rows.CountAsync(cancellationToken);

        // Id breaks ties. Several invoices raised in the same second is ordinary
        // — a delivery bills the car, the warranty and the plates together — and
        // an order that is not total lets the database return one of them on two
        // pages and another on none.
        var loaded = await rows
            .OrderByDescending(r => r.BilledAt)
            .ThenBy(r => r.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        var names = await NamesAsync(loaded.Select(r => r.CustomerId), cancellationToken);

        return Result.Success(new Page<ReceivableSummary>(
            loaded.Select(r => Summarize(r, names)).ToList(),
            total,
            skip,
            take));
    }

    public async Task<Result<ReceivableDetail>> GetAsync(Guid receivableId, CancellationToken cancellationToken)
    {
        var row = await _db.Receivables
            .AsNoTracking()
            .Include(r => r.Payments)
            .SingleOrDefaultAsync(r => r.Id == receivableId, cancellationToken);

        // Unknown and unauthorized answer identically, so a caller cannot probe
        // for another rooftop's debts by id.
        if (row is null)
        {
            return Result.Failure<ReceivableDetail>(ReceivableErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, ReadPermission, row.RooftopId, cancellationToken))
        {
            return Result.Failure<ReceivableDetail>(ReceivableErrors.Forbidden);
        }

        return Result.Success(await DescribeAsync(row, cancellationToken));
    }

    public async Task<Result<ReceivableDetail?>> FindAsync(
        ReceivableSource source,
        string reference,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);

        var trimmed = reference.Trim();

        var row = await _db.Receivables
            .AsNoTracking()
            .Include(r => r.Payments)
            .SingleOrDefaultAsync(
                r => r.Source == source && r.Reference == trimmed, cancellationToken);

        if (row is null)
        {
            return Result.Success<ReceivableDetail?>(null);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, ReadPermission, row.RooftopId, cancellationToken))
        {
            return Result.Failure<ReceivableDetail?>(ReceivableErrors.Forbidden);
        }

        return Result.Success<ReceivableDetail?>(await DescribeAsync(row, cancellationToken));
    }

    public async Task<Result<Receivable>> OpenAsync(
        NewReceivable receivable,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(receivable);

        // No permission check and no save: this runs inside a caller that has
        // already authorized the operation the debt comes from. Delivering a car
        // is the permission; the receivable is a consequence of it.
        var existing = await _db.Receivables
            .AsNoTracking()
            .AnyAsync(
                r => r.Source == receivable.Source && r.Reference == receivable.Reference,
                cancellationToken);

        if (existing)
        {
            return Result.Failure<Receivable>(ReceivableErrors.AlreadyBilled(receivable.Reference));
        }

        Receivable opened;
        try
        {
            opened = Receivable.Open(
                Guid.NewGuid(),
                receivable.RooftopId,
                receivable.CustomerId,
                receivable.Source,
                receivable.Reference,
                new Money(receivable.Amount, receivable.Currency),
                receivable.BilledAt);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return Result.Failure<Receivable>(Error.Validation("receivables.invalid", ex.Message));
        }

        _db.Receivables.Add(opened);

        return Result.Success(opened);
    }

    public async Task<Result<ReceivableDetail>> RecordPaymentAsync(
        Guid receivableId,
        NewPayment payment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payment);

        var row = await _db.Receivables
            .Include(r => r.Payments)
            .SingleOrDefaultAsync(r => r.Id == receivableId, cancellationToken);

        if (row is null)
        {
            return Result.Failure<ReceivableDetail>(ReceivableErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, PostPermission, row.RooftopId, cancellationToken))
        {
            return Result.Failure<ReceivableDetail>(ReceivableErrors.Forbidden);
        }

        if (!Enum.TryParse<PaymentMethod>(payment.Method, ignoreCase: true, out var method))
        {
            return Result.Failure<ReceivableDetail>(ReceivableErrors.UnknownMethod);
        }

        // Said plainly before the entity says it, because "that is more than is
        // owed" is the message somebody actually needs, and a settled row hit by
        // a second payment is the common way to arrive here.
        if (row.IsSettled)
        {
            return Result.Failure<ReceivableDetail>(ReceivableErrors.AlreadySettled);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        Payment taken;
        try
        {
            taken = row.Take(
                Guid.NewGuid(),
                new Money(payment.Amount, row.Currency),
                method,
                _clock.UtcNow,
                payment.Note);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<ReceivableDetail>(Error.Validation("receivables.payment_invalid", ex.Message));
        }

        // The money moves in the ledger too, or it does not move at all. A
        // payment recorded here without an entry there is the drift this whole
        // capability exists to prevent.
        // Filed under the bill's own reference, deliberately, so the debit and
        // every payment against it read together in the journal. Unlike a
        // delivery there is no "already posted" guard on it: several payments
        // against one bill is the ordinary case, not a mistake.
        var posted = await _accounting.PostPaymentAsync(
            new PaymentPosting(
                row.RooftopId,
                row.Reference,
                row.Currency,
                taken.Amount,
                $"Payment against {row.Reference}"),
            cancellationToken);

        if (posted.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<ReceivableDetail>(posted.Error);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, PostPermission, AuditOutcome.Allowed,
                "Receivable", row.Id.ToString(), row.RooftopId.Value,
                $"Payment {taken.Amount} against {row.Reference}", null, null),
            cancellationToken);

        return Result.Success(await DescribeAsync(row, cancellationToken));
    }

    // --- helpers -----------------------------------------------------------

    private async Task<Dictionary<Guid, string>> NamesAsync(
        IEnumerable<Guid> customerIds,
        CancellationToken cancellationToken)
    {
        var wanted = customerIds.Distinct().ToList();
        if (wanted.Count == 0)
        {
            return [];
        }

        // One query for the page rather than one per row, and never a stale copy
        // of the name kept on the receivable itself.
        var found = await _customers.GetManyAsync(wanted, cancellationToken);

        return found.IsSuccess
            ? found.Value.ToDictionary(c => c.Id, c => c.DisplayName)
            : [];
    }

    private async Task<ReceivableDetail> DescribeAsync(Receivable row, CancellationToken cancellationToken)
    {
        var names = await NamesAsync([row.CustomerId], cancellationToken);

        return new ReceivableDetail(
            row.Id,
            row.RooftopId,
            row.CustomerId,
            names.GetValueOrDefault(row.CustomerId, string.Empty),
            row.Source.ToString(),
            row.Reference,
            row.Amount,
            row.Paid,
            row.Outstanding,
            row.Currency,
            row.BilledAt,
            DaysOutstanding(row),
            row.IsSettled,
            row.Payments
                .OrderBy(p => p.ReceivedAt)
                .Select(p => new PaymentView(
                    p.Id, p.Amount, p.Currency, p.Method.ToString(), p.ReceivedAt, p.Note))
                .ToList());
    }

    private ReceivableSummary Summarize(Receivable row, IReadOnlyDictionary<Guid, string> names) =>
        new(
            row.Id,
            row.RooftopId,
            row.CustomerId,
            names.GetValueOrDefault(row.CustomerId, string.Empty),
            row.Source.ToString(),
            row.Reference,
            row.Amount,
            row.Paid,
            row.Outstanding,
            row.Currency,
            row.BilledAt,
            DaysOutstanding(row),
            row.IsSettled);

    /// <summary>
    /// How long the money has been owed, counted from the day it was billed and
    /// never negative. A settled debt reports zero rather than the age it reached
    /// — "still owed for 40 days" is a claim about the present.
    /// </summary>
    private int DaysOutstanding(Receivable row) =>
        row.IsSettled ? 0 : Math.Max(0, (int)(_clock.UtcNow - row.BilledAt).TotalDays);
}

internal static class ReceivableErrors
{
    public static Error Forbidden { get; } =
        Error.Forbidden("receivables.forbidden", "You do not have access to this account.");

    public static Error AlreadySettled { get; } =
        Error.Conflict("receivables.already_settled", "This has been paid in full already.");

    public static Error UnknownMethod { get; } =
        Error.Validation("receivables.unknown_method", "That is not a way of paying this system knows.");

    public static Error AlreadyBilled(string reference) =>
        Error.Conflict("receivables.already_billed", $"{reference} has already been billed.");
}
