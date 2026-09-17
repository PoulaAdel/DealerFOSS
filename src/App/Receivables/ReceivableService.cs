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

    /// <summary>
    /// Handing a credit back. Deliberately NOT PostPermission: taking money and
    /// giving it back are different acts, and only one of them is a way to get
    /// cash out of the business. See Permissions.AccountingRefund.
    /// </summary>
    private const string RefundPermission = Permissions.AccountingRefund;

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
        // A payment is capped at what is owed and the excess becomes a credit, so
        // outstanding is never negative and "not settled" is exactly "paid less
        // than billed" — which is what lets this be a comparison the database can
        // do rather than a subtraction it cannot.
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

    public async Task<Result<AgeingReport>> GetAgeingAsync(AgeingQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<AgeingReport>(ReceivableErrors.Forbidden);
        }

        if (query.RooftopId is { } requested && !scope.Covers(requested))
        {
            return Result.Failure<AgeingReport>(ReceivableErrors.Forbidden);
        }

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

        // Same shape as ListAsync's OutstandingOnly filter — a correlated sum so
        // the database drops settled bills before anything is loaded, rather
        // than after.
        rows = rows.Where(r => r.Payments.Sum(p => p.Amount) < r.Amount);

        // Mixing currencies in one column would produce a number that means
        // nothing — see AccountingService's own refusal for the same reason.
        // Ageing is a whole-book question a dealership asks rarely enough that
        // narrowing to one rooftop, which is usually one currency, is a
        // reasonable answer to be asked to give.
        var currencies = await rows.Select(r => r.Currency).Distinct().ToListAsync(cancellationToken);
        if (currencies.Count > 1)
        {
            return Result.Failure<AgeingReport>(ReceivableErrors.MixedCurrencies(currencies));
        }

        // The whole outstanding book, not a page of it — an ageing report is a
        // total across everybody, and there is no honest way to page a sum. A
        // real dealership's open receivables are hundreds of rows, not millions.
        var loaded = await rows.ToListAsync(cancellationToken);

        var names = await NamesAsync(loaded.Select(r => r.CustomerId), cancellationToken);
        var now = _clock.UtcNow;

        var customers = loaded
            .GroupBy(r => r.CustomerId)
            .Select(group =>
            {
                var bucket = Bucket(group, now);
                return new CustomerAgeing(
                    group.Key,
                    names.GetValueOrDefault(group.Key, string.Empty),
                    bucket);
            })
            // Oldest debt first: the row a manager needs to see is the one with
            // money in Over90, not the one with the biggest total.
            .OrderByDescending(c => c.Bucket.Over90)
            .ThenByDescending(c => c.Bucket.Total)
            .ToList();

        var totals = new AgeingBucket(
            customers.Sum(c => c.Bucket.Current),
            customers.Sum(c => c.Bucket.Days31To60),
            customers.Sum(c => c.Bucket.Days61To90),
            customers.Sum(c => c.Bucket.Over90),
            customers.Sum(c => c.Bucket.Total));

        return Result.Success(new AgeingReport(
            currencies.Count == 1 ? currencies[0] : "USD",
            customers,
            totals));
    }

    public async Task<Result<CustomerStatement>> GetStatementAsync(
        StatementQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.To < query.From)
        {
            return Result.Failure<CustomerStatement>(ReceivableErrors.StatementRangeBackwards);
        }

        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<CustomerStatement>(ReceivableErrors.Forbidden);
        }

        if (query.RooftopId is { } requested && !scope.Covers(requested))
        {
            return Result.Failure<CustomerStatement>(ReceivableErrors.Forbidden);
        }

        var rows = _db.Receivables
            .AsNoTracking()
            .Include(r => r.Payments)
            .Where(r => r.CustomerId == query.CustomerId)
            .AsQueryable();

        if (!scope.IsOrganizationWide)
        {
            var allowed = scope.Rooftops.ToList();
            rows = rows.Where(r => allowed.Contains(r.RooftopId));
        }

        if (query.RooftopId is { } only)
        {
            rows = rows.Where(r => r.RooftopId == only);
        }

        // The customer's WHOLE history, not just the period: the opening
        // balance is what came before it, and the running balance underneath
        // the period lines needs every bill and payment either side to be
        // right.
        var bills = await rows.OrderBy(r => r.BilledAt).ToListAsync(cancellationToken);

        var currencies = bills.Select(r => r.Currency).Distinct().ToList();
        if (currencies.Count > 1)
        {
            return Result.Failure<CustomerStatement>(ReceivableErrors.MixedCurrencies(currencies));
        }

        var names = await NamesAsync([query.CustomerId], cancellationToken);
        var currency = currencies.Count == 1 ? currencies[0] : "USD";

        var opening = bills
            .Where(r => r.BilledAt < query.From)
            .Sum(r => r.Amount - r.Payments.Where(p => p.ReceivedAt < query.From).Sum(p => p.Amount));

        var lines = new List<StatementLine>();

        foreach (var bill in bills.Where(r => r.BilledAt >= query.From && r.BilledAt <= query.To))
        {
            lines.Add(new StatementLine(bill.BilledAt, "Invoice", bill.Reference, bill.Amount, 0m));
        }

        foreach (var bill in bills)
        {
            foreach (var payment in bill.Payments.Where(p => p.ReceivedAt >= query.From && p.ReceivedAt <= query.To))
            {
                lines.Add(new StatementLine(payment.ReceivedAt, "Payment", bill.Reference, payment.Amount, 0m));
            }
        }

        lines = lines.OrderBy(l => l.Date).ToList();

        var running = opening;
        for (var i = 0; i < lines.Count; i++)
        {
            running += lines[i].Kind == "Invoice" ? lines[i].Amount : -lines[i].Amount;
            lines[i] = lines[i] with { Balance = running };
        }

        return Result.Success(new CustomerStatement(
            query.CustomerId,
            names.GetValueOrDefault(query.CustomerId, string.Empty),
            currency,
            query.From,
            query.To,
            opening,
            running,
            lines));
    }

    /// <summary>Buckets one customer's outstanding bills by how long each has been owed.</summary>
    private static AgeingBucket Bucket(IEnumerable<Receivable> bills, DateTimeOffset now)
    {
        decimal current = 0m, days31To60 = 0m, days61To90 = 0m, over90 = 0m;

        foreach (var bill in bills)
        {
            var outstanding = bill.Amount - bill.Payments.Sum(p => p.Amount);
            var days = Math.Max(0, (int)(now - bill.BilledAt).TotalDays);

            if (days <= 30)
            {
                current += outstanding;
            }
            else if (days <= 60)
            {
                days31To60 += outstanding;
            }
            else if (days <= 90)
            {
                days61To90 += outstanding;
            }
            else
            {
                over90 += outstanding;
            }
        }

        return new AgeingBucket(current, days31To60, days61To90, over90, current + days31To60 + days61To90 + over90);
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

        // THE CAP, WHEN THE CUSTOMER HAS ONE. Read with no permission check —
        // see the remarks on ICustomers.GetCreditLimitAsync — and skipped
        // rather than refused if the lookup itself fails, because a receivable
        // is not the place to discover a customer record has gone missing; the
        // caller already validated CustomerId when it built the deal or the
        // job.
        var limit = await _customers.GetCreditLimitAsync(receivable.CustomerId, cancellationToken);
        if (limit.IsSuccess && limit.Value is { } cap)
        {
            // Same-currency only, like every other total in this application —
            // see AccountingService's mixed-currency refusal. A customer who
            // owes in two currencies is rare enough that summing only the one
            // this new bill is in, rather than inventing an exchange rate, is
            // the honest answer.
            var billed = await _db.Receivables
                .Where(r => r.CustomerId == receivable.CustomerId && r.Currency == receivable.Currency)
                .SumAsync(r => r.Amount, cancellationToken);

            var paid = await _db.Receivables
                .Where(r => r.CustomerId == receivable.CustomerId && r.Currency == receivable.Currency)
                .SelectMany(r => r.Payments)
                .SumAsync(p => p.Amount, cancellationToken);

            var currentlyOwed = billed - paid;

            if (currentlyOwed + receivable.Amount > cap)
            {
                return Result.Failure<Receivable>(
                    ReceivableErrors.CreditLimitExceeded(currentlyOwed, cap, receivable.Currency));
            }
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

        // A SETTLED BILL IS STILL REFUSED, and this is not the same case as an
        // overpayment. A payment against a bill with nothing left on it is almost
        // always the same payment keyed twice, where no second money arrived at
        // all — absorbing it would invent both the cash and the liability, and
        // the books would balance perfectly around a transaction that never
        // happened. Overpaying a bill that IS still owed is different: the money
        // is real, it is on the counter, and the only question is where to put
        // the difference.
        if (row.IsSettled)
        {
            return Result.Failure<ReceivableDetail>(ReceivableErrors.AlreadySettled);
        }

        // A credit cannot be conjured by typing it into the payment box; it is
        // raised only by real money arriving. Applying one is its own operation
        // with its own entry, which posts no cash.
        if (method == PaymentMethod.CustomerCredit)
        {
            return Result.Failure<ReceivableDetail>(ReceivableErrors.CreditIsNotAPaymentMethod);
        }

        // THE SPLIT. What the bill can absorb settles it; the rest is the
        // customer's money and becomes something we owe back. Both halves come
        // out of one act at the counter, so both are written inside one
        // transaction and posted as one journal entry.
        var applied = Math.Min(payment.Amount, row.Outstanding);
        var excess = payment.Amount - applied;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        Payment taken;
        CustomerCredit? credit = null;
        try
        {
            taken = row.Take(
                Guid.NewGuid(),
                new Money(applied, row.Currency),
                method,
                _clock.UtcNow,
                payment.Note);

            if (excess > 0m)
            {
                credit = CustomerCredit.Raise(
                    Guid.NewGuid(),
                    row.RooftopId,
                    row.CustomerId,
                    new Money(excess, row.Currency),
                    row.Reference,
                    row.Id,
                    _clock.UtcNow);

                _db.CustomerCredits.Add(credit);
            }
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
                excess > 0m
                    ? $"Payment against {row.Reference}, {excess} overpaid"
                    : $"Payment against {row.Reference}",
                excess),
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
                credit is null
                    ? $"Payment {taken.Amount} against {row.Reference}"
                    : $"Payment {taken.Amount} against {row.Reference}, credit {credit.Amount} raised",
                null, null),
            cancellationToken);

        return Result.Success(await DescribeAsync(row, cancellationToken));
    }

    // --- credits the dealership is holding ----------------------------------

    public async Task<Result<Page<CreditSummary>>> ListCreditsAsync(
        CreditQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<Page<CreditSummary>>(ReceivableErrors.Forbidden);
        }

        if (query.RooftopId is { } requested && !scope.Covers(requested))
        {
            return Result.Failure<Page<CreditSummary>>(ReceivableErrors.Forbidden);
        }

        var take = Paging.Limit(query.Limit);
        var skip = Paging.Offset(query.Offset);
        var rows = _db.CustomerCredits.AsNoTracking().Include(c => c.Uses).AsQueryable();

        if (!scope.IsOrganizationWide)
        {
            var allowed = scope.Rooftops.ToList();
            rows = rows.Where(c => allowed.Contains(c.RooftopId));
        }

        if (query.RooftopId is { } only)
        {
            rows = rows.Where(c => c.RooftopId == only);
        }

        if (query.CustomerId is { } customer)
        {
            rows = rows.Where(c => c.CustomerId == customer);
        }

        // The same shape as the outstanding filter above, and for the same
        // reason: expressed as a correlated sum so the database applies it before
        // the page is taken, rather than in memory afterwards where it cannot be
        // paged. A use can never exceed the credit, so "not spent" is exactly
        // "used less than raised".
        if (query.OpenOnly)
        {
            rows = rows.Where(c => c.Uses.Sum(u => u.Amount) < c.Amount);
        }

        var total = await rows.CountAsync(cancellationToken);

        var loaded = await rows
            .OrderByDescending(c => c.RaisedAt)
            .ThenBy(c => c.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        var names = await NamesAsync(loaded.Select(c => c.CustomerId), cancellationToken);

        return Result.Success(new Page<CreditSummary>(
            loaded.Select(c => Summarize(c, names)).ToList(),
            total,
            skip,
            take));
    }

    public async Task<Result<CreditSummary>> ApplyCreditAsync(
        Guid creditId,
        ApplyCredit application,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);

        var credit = await _db.CustomerCredits
            .Include(c => c.Uses)
            .SingleOrDefaultAsync(c => c.Id == creditId, cancellationToken);

        if (credit is null)
        {
            return Result.Failure<CreditSummary>(ReceivableErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, PostPermission, credit.RooftopId, cancellationToken))
        {
            return Result.Failure<CreditSummary>(ReceivableErrors.Forbidden);
        }

        var bill = await _db.Receivables
            .Include(r => r.Payments)
            .SingleOrDefaultAsync(r => r.Id == application.ReceivableId, cancellationToken);

        if (bill is null)
        {
            return Result.Failure<CreditSummary>(ReceivableErrors.Forbidden);
        }

        // The bill is at a rooftop of its own, and the caller must be allowed to
        // post there too. Checking only the credit's rooftop would let somebody
        // who covers one lot settle a bill at another.
        if (!await _access.IsAuthorizedAsync(_currentUser.Id, PostPermission, bill.RooftopId, cancellationToken))
        {
            return Result.Failure<CreditSummary>(ReceivableErrors.Forbidden);
        }

        // ONE CUSTOMER'S MONEY DOES NOT PAY ANOTHER CUSTOMER'S BILL. This is the
        // check that matters most in this method: without it a credit becomes a
        // way to move money between accounts that never agreed to it, and the
        // ledger would balance the whole time.
        if (bill.CustomerId != credit.CustomerId)
        {
            return Result.Failure<CreditSummary>(ReceivableErrors.CreditBelongsToSomebodyElse);
        }

        if (bill.IsSettled)
        {
            return Result.Failure<CreditSummary>(ReceivableErrors.AlreadySettled);
        }

        if (application.Amount > bill.Outstanding)
        {
            return Result.Failure<CreditSummary>(
                ReceivableErrors.MoreThanIsOwed(bill.Outstanding, bill.Currency));
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            credit.Spend(
                Guid.NewGuid(),
                new Money(application.Amount, credit.Currency),
                CreditUseKind.AppliedToBill,
                bill.Id,
                _clock.UtcNow,
                application.Note);

            // The bill sees an ordinary payment, because from the bill's side it
            // was settled. What keeps it out of cash is the entry below.
            bill.Take(
                Guid.NewGuid(),
                new Money(application.Amount, bill.Currency),
                PaymentMethod.CustomerCredit,
                _clock.UtcNow,
                application.Note);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<CreditSummary>(Error.Validation("receivables.credit_invalid", ex.Message));
        }

        var posted = await _accounting.PostCreditApplicationAsync(
            new CreditApplicationPosting(
                bill.RooftopId,
                bill.Reference,
                bill.Currency,
                application.Amount,
                $"Credit from {credit.Reference} applied to {bill.Reference}"),
            cancellationToken);

        if (posted.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<CreditSummary>(posted.Error);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, PostPermission, AuditOutcome.Allowed,
                "CustomerCredit", credit.Id.ToString(), credit.RooftopId.Value,
                $"Credit {application.Amount} applied to {bill.Reference}", null, null),
            cancellationToken);

        return Result.Success(await DescribeAsync(credit, cancellationToken));
    }

    public async Task<Result<CreditSummary>> RefundCreditAsync(
        Guid creditId,
        RefundCredit refund,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refund);

        var credit = await _db.CustomerCredits
            .Include(c => c.Uses)
            .SingleOrDefaultAsync(c => c.Id == creditId, cancellationToken);

        if (credit is null)
        {
            return Result.Failure<CreditSummary>(ReceivableErrors.Forbidden);
        }

        // NOT PostPermission. Whoever takes money at a counter holds that; handing
        // money back is a different act and a different right. See
        // Permissions.AccountingRefund.
        if (!await _access.IsAuthorizedAsync(_currentUser.Id, RefundPermission, credit.RooftopId, cancellationToken))
        {
            return Result.Failure<CreditSummary>(ReceivableErrors.Forbidden);
        }

        if (!Enum.TryParse<PaymentMethod>(refund.Method, ignoreCase: true, out var method))
        {
            return Result.Failure<CreditSummary>(ReceivableErrors.UnknownMethod);
        }

        // Refunding a credit "by credit" is not a way of giving money back, it is
        // a way of writing a row that says nothing happened.
        if (method == PaymentMethod.CustomerCredit)
        {
            return Result.Failure<CreditSummary>(ReceivableErrors.CreditIsNotARefundMethod);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            credit.Spend(
                Guid.NewGuid(),
                new Money(refund.Amount, credit.Currency),
                CreditUseKind.Refunded,
                null,
                _clock.UtcNow,
                string.IsNullOrWhiteSpace(refund.Note) ? method.ToString() : refund.Note);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<CreditSummary>(Error.Validation("receivables.credit_invalid", ex.Message));
        }

        var posted = await _accounting.PostCreditRefundAsync(
            new CreditRefundPosting(
                credit.RooftopId,
                credit.Reference,
                credit.Currency,
                refund.Amount,
                $"Credit from {credit.Reference} refunded by {method}"),
            cancellationToken);

        if (posted.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<CreditSummary>(posted.Error);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, RefundPermission, AuditOutcome.Allowed,
                "CustomerCredit", credit.Id.ToString(), credit.RooftopId.Value,
                $"Credit {refund.Amount} refunded by {method}", null, null),
            cancellationToken);

        return Result.Success(await DescribeAsync(credit, cancellationToken));
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

        var credits = await _db.CustomerCredits
            .AsNoTracking()
            .Include(c => c.Uses)
            .Where(c => c.SourceReceivableId == row.Id)
            .OrderBy(c => c.RaisedAt)
            .ToListAsync(cancellationToken);

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
                .ToList(),
            credits.Select(c => Summarize(c, names)).ToList());
    }

    private async Task<CreditSummary> DescribeAsync(CustomerCredit credit, CancellationToken cancellationToken)
    {
        var names = await NamesAsync([credit.CustomerId], cancellationToken);

        return Summarize(credit, names);
    }

    private static CreditSummary Summarize(
        CustomerCredit credit,
        IReadOnlyDictionary<Guid, string> names) =>
        new(
            credit.Id,
            credit.RooftopId,
            credit.CustomerId,
            names.GetValueOrDefault(credit.CustomerId, string.Empty),
            credit.Amount,
            credit.Spent,
            credit.Remaining,
            credit.Currency,
            credit.Reference,
            credit.RaisedAt,
            credit.IsSpent,
            credit.Uses
                .OrderBy(u => u.UsedAt)
                .Select(u => new CreditUseView(
                    u.Id, u.Amount, u.Currency, u.Kind.ToString(), u.ReceivableId, u.UsedAt, u.Note))
                .ToList());

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

    /// <summary>
    /// The check that keeps a credit from becoming a way to move money between
    /// customers who never agreed to it.
    /// </summary>
    public static Error CreditBelongsToSomebodyElse { get; } =
        Error.Validation("receivables.credit_wrong_customer",
            "That credit belongs to a different customer.");

    public static Error CreditIsNotAPaymentMethod { get; } =
        Error.Validation("receivables.credit_not_a_payment",
            "A credit is put against a bill from the customer's credit, not typed in as a payment.");

    public static Error CreditIsNotARefundMethod { get; } =
        Error.Validation("receivables.credit_not_a_refund",
            "Say how the money was handed back — cash, card, transfer or cheque.");

    public static Error MoreThanIsOwed(decimal outstanding, string currency) =>
        Error.Validation("receivables.more_than_owed",
            $"Only {currency} {outstanding} is still owed on that bill.");

    public static Error CreditLimitExceeded(decimal currentlyOwed, decimal limit, string currency) =>
        Error.Validation("receivables.credit_limit_exceeded",
            $"This would put them at more than their {currency} {limit} credit limit "
            + $"— they already owe {currency} {currentlyOwed}.");

    public static Error StatementRangeBackwards { get; } = Error.Validation(
        "receivables.statement_range_backwards",
        "The statement's end date is before its start date.");

    public static Error MixedCurrencies(IEnumerable<string> currencies) => Error.Validation(
        "receivables.mixed_currencies",
        $"These bills are in more than one currency ({string.Join(", ", currencies)}). "
        + "Totalling them would produce a number that means nothing — narrow the rooftop.");
}
