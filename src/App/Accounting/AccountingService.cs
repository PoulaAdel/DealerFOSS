// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   AccountingService — reading the ledger, and the two ways something gets into
//   it.
//
// Usage:
//   Through IAccounting.
//
// Coding Instructions:
//   The posting rules live in JournalEntry, not here, so they hold for any
//   caller. This class does three jobs: scope the reads by rooftop, translate
//   a business event into the accounts it moves, and refuse to reverse
//   something twice.
//
//   The account map below is the one place that decides which accounts a
//   sale touches. When a real chart of accounts arrives it becomes
//   configuration; until then it is deliberately in one readable method
//   rather than scattered.

using Microsoft.EntityFrameworkCore;
using DealerFOSS.Core;
using DealerFOSS.Data;
using DealerFOSS.Identity;
using DealerFOSS.Organization;

namespace DealerFOSS.Accounting;

public sealed class AccountingService(
    TenantDb db,
    IAccessDirectory access,
    IOrganization organization,
    ICurrentUser currentUser,
    IAuditSink audit,
    IClock clock)
    : IAccounting
{
    private const string ReadPermission = Permissions.AccountingRead;
    private const string PostPermission = Permissions.AccountingPost;

    /// <summary>
    /// Reversing is its own right. Posting is the consequence of finishing a sale
    /// or a job and belongs to whoever finishes them; reversing is the one ledger
    /// operation that can make a mistake disappear, so it belongs to whoever
    /// answers for the numbers.
    /// </summary>
    private const string ReversePermission = Permissions.AccountingReverse;

    /// <summary>
    /// Giving a customer their money back. Its own permission, because it is the
    /// only posting here that takes cash out for something the business did not
    /// sell. See Permissions.AccountingRefund.
    /// </summary>
    private const string RefundPermission = Permissions.AccountingRefund;

    /// <summary>
    /// Writing an entry by hand. Its own right, and NOT implied by
    /// <see cref="PostPermission"/> - a salesperson holds that because delivering
    /// a car posts the sale, and choosing the accounts is a different act.
    /// </summary>
    private const string ManualEntryPermission = Permissions.AccountingManualEntry;

    private const string ClosePeriodPermission = Permissions.AccountingClosePeriod;

    /// <summary>
    /// Its own right, and not the one that closes. Closing is routine month-end
    /// work; reopening lets a reported figure move.
    /// </summary>
    private const string ReopenPeriodPermission = Permissions.AccountingReopenPeriod;

    private const int MaxResults = 200;

    private readonly TenantDb _db = db;
    private readonly IAccessDirectory _access = access;
    private readonly IOrganization _organization = organization;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditSink _audit = audit;
    private readonly IClock _clock = clock;

    public async Task<Result<IReadOnlyList<AccountView>>> ListAccountsAsync(CancellationToken cancellationToken)
    {
        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<IReadOnlyList<AccountView>>(LedgerErrors.Forbidden);
        }

        var accounts = await _db.Accounts
            .AsNoTracking()
            .Where(a => a.IsActive)
            .OrderBy(a => a.Code)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<AccountView>>(
            accounts.Select(a => new AccountView(a.Id, a.Code, a.Name, a.Kind.ToString())).ToList());
    }

    public async Task<Result<Page<JournalEntrySummary>>> ListAsync(
        JournalQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<Page<JournalEntrySummary>>(LedgerErrors.Forbidden);
        }

        if (query.RooftopId is { } requested && !scope.Covers(requested))
        {
            return Result.Failure<Page<JournalEntrySummary>>(LedgerErrors.Forbidden);
        }

        var take = Paging.Limit(query.Limit);
        var skip = Paging.Offset(query.Offset);
        var entries = _db.JournalEntries.AsNoTracking().Include(e => e.Lines).AsQueryable();

        if (!scope.IsOrganizationWide)
        {
            var allowed = scope.Rooftops.ToList();
            entries = entries.Where(e => allowed.Contains(e.RooftopId));
        }

        if (query.RooftopId is { } only)
        {
            entries = entries.Where(e => e.RooftopId == only);
        }

        if (!string.IsNullOrWhiteSpace(query.Reference))
        {
            var reference = query.Reference.Trim();
            entries = entries.Where(e => e.Reference == reference);
        }

        if (query.From is { } from)
        {
            entries = entries.Where(e => e.EntryDate >= from);
        }

        if (query.To is { } to)
        {
            entries = entries.Where(e => e.EntryDate <= to);
        }

        // Counted over the same filters as the page, and before it is taken.
        var total = await entries.CountAsync(cancellationToken);

        var rows = await entries
            .OrderByDescending(e => e.PostedAt)
            .ThenBy(e => e.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return Result.Success(new Page<JournalEntrySummary>(
            rows.Select(e => new JournalEntrySummary(
                e.Id,
                e.RooftopId,
                e.EntryDate,
                e.Source.ToString(),
                e.Reference,
                e.Memo,
                e.TotalDebits.Amount,
                e.Currency,
                e.ReversesEntryId is not null)).ToList(),
            total,
            skip,
            take));
    }

    public async Task<Result<JournalEntryDetail>> GetAsync(Guid entryId, CancellationToken cancellationToken)
    {
        var entry = await _db.JournalEntries
            .AsNoTracking()
            .Include(e => e.Lines)
            .SingleOrDefaultAsync(e => e.Id == entryId, cancellationToken);

        if (entry is null)
        {
            return Result.Failure<JournalEntryDetail>(LedgerErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, ReadPermission, entry.RooftopId, cancellationToken))
        {
            return Result.Failure<JournalEntryDetail>(LedgerErrors.Forbidden);
        }

        return Result.Success(await DescribeAsync(entry, cancellationToken));
    }

    public async Task<Result<TrialBalance>> TrialBalanceAsync(
        BalanceQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var covered = await CoveredEntriesAsync(query, cancellationToken);
        if (covered.IsFailure)
        {
            return Result.Failure<TrialBalance>(covered.Error);
        }

        var (entries, currency) = covered.Value;

        var totals = await (
            from line in _db.JournalLines.AsNoTracking()
            join entry in entries on line.EntryId equals entry.Id
            group line by line.AccountId into byAccount
            select new
            {
                AccountId = byAccount.Key,
                Debits = byAccount.Sum(l => l.Debit),
                Credits = byAccount.Sum(l => l.Credit),
            }).ToListAsync(cancellationToken);

        var accounts = await _db.Accounts.AsNoTracking().ToListAsync(cancellationToken);

        var balances = totals
            .Select(total =>
            {
                var account = accounts.Find(a => a.Id == total.AccountId);
                var debits = total.Debits;
                var credits = total.Credits;

                // Stated on the account's normal side, so a healthy account reads
                // positive whichever kind it is.
                var balance = account?.IncreasesOnDebit == true
                    ? debits - credits
                    : credits - debits;

                return new AccountBalance(
                    account?.Code ?? "?",
                    account?.Name ?? "(account removed)",
                    account?.Kind.ToString() ?? "Unknown",
                    debits,
                    credits,
                    balance);
            })
            .OrderBy(a => a.Code, StringComparer.Ordinal)
            .ToList();

        var totalDebits = balances.Sum(a => a.Debits);
        var totalCredits = balances.Sum(a => a.Credits);

        return Result.Success(new TrialBalance(
            query.From,
            query.To,
            currency,
            totalDebits,
            totalCredits,
            totalDebits == totalCredits,
            balances));
    }

    public async Task<Result<LedgerPerformance>> PerformanceAsync(
        BalanceQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Deliberately the same scoping, the same filters, and the same
        // mixed-currency refusal as the trial balance — one method, so a figure
        // on a dashboard cannot cover a rooftop the trial balance would not.
        var covered = await CoveredEntriesAsync(query, cancellationToken);
        if (covered.IsFailure)
        {
            return Result.Failure<LedgerPerformance>(covered.Error);
        }

        var (entries, currency) = covered.Value;

        var totals = await (
            from line in _db.JournalLines.AsNoTracking()
            join entry in entries on line.EntryId equals entry.Id
            group line by line.AccountCode into byCode
            select new
            {
                Code = byCode.Key,
                Debits = byCode.Sum(l => l.Debit),
                Credits = byCode.Sum(l => l.Credit),
            }).ToDictionaryAsync(t => t.Code, cancellationToken);

        // On the account's normal side. Revenue net of what was debited to it,
        // which is how the discount account subtracts itself: 4900 is a revenue
        // account that only ever takes debits, so Earned() returns it negative
        // and a discount reduces the sale it belongs to.
        decimal Earned(string code) =>
            totals.TryGetValue(code, out var t) ? t.Credits - t.Debits : 0m;

        decimal Spent(string code) =>
            totals.TryGetValue(code, out var t) ? t.Debits - t.Credits : 0m;

        var departments = new List<DepartmentResult>
        {
            Department(
                Departments.Vehicles,
                Earned(AccountCodes.VehicleSalesRevenue)
                    + Earned(AccountCodes.FeeRevenue)
                    + Earned(AccountCodes.SalesDiscounts),
                Spent(AccountCodes.CostOfVehicleSales)),
            Department(
                Departments.FinanceAndInsurance,
                Earned(AccountCodes.FinanceProductRevenue),
                Spent(AccountCodes.CostOfFinanceProducts)),
            Department(
                Departments.Service,
                Earned(AccountCodes.LabourRevenue)
                    + Earned(AccountCodes.PartsRevenue)
                    + Earned(AccountCodes.SubletRevenue),
                Spent(AccountCodes.CostOfPartsSales)),
        };

        var deliveries = await CountAsync(entries, JournalSource.DealDelivery, cancellationToken);
        var invoices = await CountAsync(entries, JournalSource.ServiceInvoice, cancellationToken);

        return Result.Success(new LedgerPerformance(
            query.From,
            query.To,
            currency,
            departments,
            departments.Sum(d => d.Revenue),
            departments.Sum(d => d.Cost),
            departments.Sum(d => d.Gross),
            deliveries,
            invoices));
    }

    public Task<Result<ProfitAndLoss>> ProfitAndLossAsync(
        BalanceQuery query,
        CancellationToken cancellationToken) =>
        ProfitAndLossAsync(query, includePriorYear: true, cancellationToken);

    private async Task<Result<ProfitAndLoss>> ProfitAndLossAsync(
        BalanceQuery query,
        bool includePriorYear,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // The departmental half is PerformanceAsync's, called rather than
        // reimplemented. Two methods computing gross from the same accounts would
        // be two places to disagree, and the dashboard already reads one of them.
        var performance = await PerformanceAsync(query, cancellationToken);
        if (performance.IsFailure)
        {
            return Result.Failure<ProfitAndLoss>(performance.Error);
        }

        var covered = await CoveredEntriesAsync(query, cancellationToken);
        if (covered.IsFailure)
        {
            return Result.Failure<ProfitAndLoss>(covered.Error);
        }

        var (entries, _) = covered.Value;

        var totals = await (
            from line in _db.JournalLines.AsNoTracking()
            join entry in entries on line.EntryId equals entry.Id
            group line by line.AccountCode into byCode
            select new
            {
                Code = byCode.Key,
                Spent = byCode.Sum(l => l.Debit) - byCode.Sum(l => l.Credit),
            }).ToListAsync(cancellationToken);

        // Read from the CHART rather than from a list in code, so an expense
        // account somebody adds is on the report the day they add it. The first
        // version named five accounts explicitly and silently omitted 5400
        // Internal service charge, which is an expense and is not a cost of
        // sales — money spent into an account that appeared on no report.
        var overheads = await _db.Accounts
            .AsNoTracking()
            .Where(a => a.Kind == AccountKind.Expense)
            .OrderBy(a => a.Code)
            .ToListAsync(cancellationToken);

        // Every operating expense account is listed, including the ones at zero.
        // A dealership that has recorded no advertising this month should see
        // that, rather than a report that quietly omits the line and leaves
        // somebody to wonder whether the figure is missing or the spending is.
        var expenses = overheads
            .Where(a => AccountCodes.IsOperatingExpense(a.Code, a.Kind))
            .Select(a => new ExpenseLine(
                a.Code,
                a.Name,
                totals.Find(t => t.Code == a.Code)?.Spent ?? 0m))
            .ToList();

        var totalExpenses = expenses.Sum(e => e.Amount);
        var gross = performance.Value.TotalGross;

        // Only a bounded period has an unambiguous "a year earlier" — an
        // open-ended report has no single date to shift back from.
        // includePriorYear: false on the recursive call, or this would try to
        // fetch the year before THAT one, all the way back through history.
        ProfitAndLoss? priorYear = null;
        if (includePriorYear && query.From is { } from && query.To is { } to)
        {
            var priorQuery = new BalanceQuery(query.RooftopId, from.AddYears(-1), to.AddYears(-1));
            var prior = await ProfitAndLossAsync(priorQuery, includePriorYear: false, cancellationToken);
            if (prior.IsSuccess)
            {
                priorYear = prior.Value;
            }
        }

        return Result.Success(new ProfitAndLoss(
            query.From,
            query.To,
            performance.Value.Currency,
            performance.Value.Departments,
            performance.Value.TotalRevenue,
            performance.Value.TotalCost,
            gross,
            expenses,
            totalExpenses,
            gross - totalExpenses,
            priorYear));
    }

    public async Task<Result<BalanceSheet>> BalanceSheetAsync(
        BalanceQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // A balance sheet is a POSITION, not a period: everything ever posted up
        // to the date, whatever From says. Honouring From would produce a page
        // that looks like a balance sheet and is arithmetic nonsense — assets as
        // at one month, with no opening position under them.
        var asAt = new BalanceQuery(query.RooftopId, From: null, To: query.To);

        var covered = await CoveredEntriesAsync(asAt, cancellationToken);
        if (covered.IsFailure)
        {
            return Result.Failure<BalanceSheet>(covered.Error);
        }

        var (entries, currency) = covered.Value;

        var totals = await (
            from line in _db.JournalLines.AsNoTracking()
            join entry in entries on line.EntryId equals entry.Id
            group line by line.AccountId into byAccount
            select new
            {
                AccountId = byAccount.Key,
                Debits = byAccount.Sum(l => l.Debit),
                Credits = byAccount.Sum(l => l.Credit),
            }).ToListAsync(cancellationToken);

        var accounts = await _db.Accounts.AsNoTracking().ToListAsync(cancellationToken);

        var balances = totals
            .Select(total =>
            {
                var account = accounts.Find(a => a.Id == total.AccountId);

                return new
                {
                    Account = account,
                    View = new AccountBalance(
                        account?.Code ?? "?",
                        account?.Name ?? "(unknown)",
                        (account?.Kind ?? AccountKind.Asset).ToString(),
                        total.Debits,
                        total.Credits,
                        account?.IncreasesOnDebit == true
                            ? total.Debits - total.Credits
                            : total.Credits - total.Debits),
                };
            })
            .Where(b => b.Account is not null)
            .OrderBy(b => b.View.Code, StringComparer.Ordinal)
            .ToList();

        List<AccountBalance> Of(AccountKind kind) =>
            balances.Where(b => b.Account!.Kind == kind).Select(b => b.View).ToList();

        var assets = Of(AccountKind.Asset);
        var liabilities = Of(AccountKind.Liability);
        var equity = Of(AccountKind.Equity);

        // Revenue and expenses do not appear on a balance sheet as accounts; what
        // they have added up to since the beginning does, as one line. There is no
        // year-end close in this system, so folding it into capital would assert a
        // process nobody has run.
        var earnings =
            balances.Where(b => b.Account!.Kind == AccountKind.Revenue).Sum(b => b.View.Balance)
            - balances.Where(b => b.Account!.Kind == AccountKind.Expense).Sum(b => b.View.Balance);

        var totalAssets = assets.Sum(a => a.Balance);
        var totalLiabilities = liabilities.Sum(l => l.Balance);
        var totalEquity = equity.Sum(e => e.Balance);

        return Result.Success(new BalanceSheet(
            query.To,
            currency,
            assets,
            liabilities,
            equity,
            totalAssets,
            totalLiabilities,
            totalEquity,
            earnings,
            totalAssets == totalLiabilities + totalEquity + earnings));
    }

    public async Task<Result<JournalEntryDetail>> PostManualAsync(
        ManualPosting entry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // ManualEntryPermission and not PostPermission. A salesperson holds the
        // latter because delivering a car posts the sale; choosing the accounts
        // and the amounts is a different act entirely.
        if (!await _access.IsAuthorizedAsync(
            _currentUser.Id, ManualEntryPermission, entry.RooftopId, cancellationToken))
        {
            return Result.Failure<JournalEntryDetail>(LedgerErrors.Forbidden);
        }

        if (entry.Lines.Count == 0)
        {
            return Result.Failure<JournalEntryDetail>(
                Error.Validation("accounting.no_lines", "An entry needs at least two lines."));
        }

        if (string.IsNullOrWhiteSpace(entry.Memo))
        {
            // A hand-written entry with no explanation is the one somebody will be
            // asked about in a year and nobody will be able to answer.
            return Result.Failure<JournalEntryDetail>(
                Error.Validation("accounting.memo_required", "Say what this entry is for."));
        }

        var rooftop = await _organization.GetRooftopAsync(entry.RooftopId, cancellationToken);
        if (rooftop.IsFailure)
        {
            return Result.Failure<JournalEntryDetail>(rooftop.Error);
        }

        // The date is the person's to choose — an expense is dated when it was
        // incurred — so the period check uses THAT date rather than today's.
        var refusal = await PeriodRefusalAsync(entry.EntryDate, cancellationToken);
        if (refusal is not null)
        {
            return Result.Failure<JournalEntryDetail>(refusal);
        }

        var chart = await _db.Accounts
            .AsNoTracking()
            .ToDictionaryAsync(a => a.Code, cancellationToken);

        var unknown = entry.Lines
            .Select(l => l.AccountCode)
            .Where(code => !chart.ContainsKey(code))
            .Distinct()
            .ToList();

        if (unknown.Count > 0)
        {
            return Result.Failure<JournalEntryDetail>(LedgerErrors.UnknownAccounts(unknown));
        }

        JournalEntry posted;
        try
        {
            posted = JournalEntry.Post(
                Guid.NewGuid(),
                rooftop.Value.LegalEntityId,
                entry.RooftopId,
                entry.EntryDate,
                JournalSource.Manual,
                // No business record to point at, so the reference is the person's
                // own words. An empty one would make the journal unsearchable.
                entry.Memo.Trim(),
                entry.Memo.Trim(),
                entry.Currency,
                entry.Lines.Select(l =>
                    (l.AccountCode, chart[l.AccountCode].Id, l.Debit, l.Credit, l.Memo)),
                _clock.UtcNow,
                _currentUser.Id);
        }
        catch (ArgumentException ex)
        {
            // Unlike the automatic postings, an imbalance here is a PERSON's
            // mistake rather than a defect in a mapping, so it comes back as
            // something they can act on.
            return Result.Failure<JournalEntryDetail>(
                Error.Validation("accounting.will_not_balance", ex.Message));
        }

        _db.JournalEntries.Add(posted);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, ManualEntryPermission, AuditOutcome.Allowed,
                "JournalEntry", posted.Id.ToString(), entry.RooftopId.Value,
                $"Manual entry: {entry.Memo.Trim()}", null, null),
            cancellationToken);

        return Result.Success(await DescribeAsync(posted, cancellationToken));
    }

    private static DepartmentResult Department(string name, decimal revenue, decimal cost)
    {
        var gross = revenue - cost;

        return new DepartmentResult(
            name,
            revenue,
            cost,
            gross,

            // No revenue is not a zero margin. A department that has not sold
            // anything has no margin to state, and 0% would read as "we sold
            // things and made nothing on them".
            revenue == 0m ? null : gross / revenue);
    }

    /// <summary>
    /// How many of one kind of event landed in the period, less the ones reversed
    /// within it.
    /// </summary>
    /// <remarks>
    /// A reversal posted in a later month is not subtracted here, and that is
    /// correct rather than a gap: the reversal's own lines are dated into the
    /// later month, so the revenue leaves that month too. The count and the money
    /// move together, which is the only property that makes them worth showing
    /// side by side.
    /// </remarks>
    private async Task<int> CountAsync(
        IQueryable<JournalEntry> entries,
        JournalSource source,
        CancellationToken cancellationToken)
    {
        var posted = await entries.CountAsync(e => e.Source == source, cancellationToken);

        var undone = await (
            from reversal in entries.Where(e => e.Source == JournalSource.Reversal)
            join original in _db.JournalEntries.AsNoTracking()
                on reversal.ReversesEntryId equals original.Id
            where original.Source == source
            select reversal.Id).CountAsync(cancellationToken);

        return posted - undone;
    }

    /// <summary>
    /// The entries this caller may total, narrowed by the query, and the one
    /// currency they are all in.
    /// </summary>
    /// <remarks>
    /// Shared by every report that adds journal lines up. The rooftop filter is
    /// applied to the query rather than to the results, so a location the caller
    /// may not see is never read — removing that is what LedgerScopeTests catches.
    /// </remarks>
    private async Task<Result<(IQueryable<JournalEntry> Entries, string Currency)>> CoveredEntriesAsync(
        BalanceQuery query,
        CancellationToken cancellationToken)
    {
        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<(IQueryable<JournalEntry>, string)>(LedgerErrors.Forbidden);
        }

        if (query.RooftopId is { } requested && !scope.Covers(requested))
        {
            return Result.Failure<(IQueryable<JournalEntry>, string)>(LedgerErrors.Forbidden);
        }

        var entries = _db.JournalEntries.AsNoTracking();

        if (!scope.IsOrganizationWide)
        {
            var allowed = scope.Rooftops.ToList();
            entries = entries.Where(e => allowed.Contains(e.RooftopId));
        }

        if (query.RooftopId is { } only)
        {
            entries = entries.Where(e => e.RooftopId == only);
        }

        if (query.From is { } from)
        {
            entries = entries.Where(e => e.EntryDate >= from);
        }

        if (query.To is { } to)
        {
            entries = entries.Where(e => e.EntryDate <= to);
        }

        // Mixing currencies in one column would produce a number that means
        // nothing. Better to refuse than to print it.
        var currencies = await entries.Select(e => e.Currency).Distinct().ToListAsync(cancellationToken);
        if (currencies.Count > 1)
        {
            return Result.Failure<(IQueryable<JournalEntry>, string)>(
                LedgerErrors.MixedCurrencies(currencies));
        }

        return Result.Success((entries, currencies.Count == 1 ? currencies[0] : string.Empty));
    }

    public async Task<Result<JournalEntryDetail>> PostDeliveryAsync(
        DeliveryPosting delivery,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, PostPermission, delivery.RooftopId, cancellationToken))
        {
            return Result.Failure<JournalEntryDetail>(LedgerErrors.Forbidden);
        }

        // Money belongs to a legal entity, so the rooftop's owner is resolved
        // through the Organization contract rather than guessed.
        var rooftop = await _organization.GetRooftopAsync(delivery.RooftopId, cancellationToken);
        if (rooftop.IsFailure)
        {
            return Result.Failure<JournalEntryDetail>(rooftop.Error);
        }

        // Posting the same delivery twice would double the revenue. The reference
        // is the deal, so one non-reversal entry per deal is the rule.
        var alreadyPosted = await _db.JournalEntries
            .AsNoTracking()
            .AnyAsync(
                e => e.Reference == delivery.Reference && e.Source == JournalSource.DealDelivery,
                cancellationToken);

        if (alreadyPosted)
        {
            return Result.Failure<JournalEntryDetail>(LedgerErrors.AlreadyPosted);
        }

        // The books have to be open for the month this lands in. Checked here
        // rather than in JournalEntry because the period is a fact about the
        // organization, not about the entry.
        var refusal = await PeriodRefusalAsync(
            DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime), cancellationToken);

        if (refusal is not null)
        {
            return Result.Failure<JournalEntryDetail>(refusal);
        }

        var accounts = await AccountMapAsync(cancellationToken);
        if (accounts.IsFailure)
        {
            return Result.Failure<JournalEntryDetail>(accounts.Error);
        }

        JournalEntry entry;
        try
        {
            entry = JournalEntry.Post(
                Guid.NewGuid(),
                rooftop.Value.LegalEntityId,
                delivery.RooftopId,
                DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime),
                JournalSource.DealDelivery,
                delivery.Reference,
                delivery.Memo,
                delivery.Currency,
                BuildDeliveryLines(delivery, accounts.Value),
                _clock.UtcNow,
                _currentUser.Id);
        }
        catch (ArgumentException ex)
        {
            // An unbalanced entry is a defect in the mapping above, not user
            // error — but it must never reach the ledger either way.
            return Result.Failure<JournalEntryDetail>(
                Error.Validation("accounting.will_not_balance", ex.Message));
        }

        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, PostPermission, AuditOutcome.Allowed,
                "JournalEntry", entry.Id.ToString(), delivery.RooftopId.Value,
                $"Delivery {delivery.Reference}", null, null),
            cancellationToken);

        return Result.Success(await DescribeAsync(entry, cancellationToken));
    }

    public async Task<Result<JournalEntryDetail>> PostPaymentAsync(
        PaymentPosting payment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payment);

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, PostPermission, payment.RooftopId, cancellationToken))
        {
            return Result.Failure<JournalEntryDetail>(LedgerErrors.Forbidden);
        }

        if (payment.Amount <= 0m)
        {
            return Result.Failure<JournalEntryDetail>(
                Error.Validation("accounting.payment_invalid", "A payment is for an amount above zero."));
        }

        var rooftop = await _organization.GetRooftopAsync(payment.RooftopId, cancellationToken);
        if (rooftop.IsFailure)
        {
            return Result.Failure<JournalEntryDetail>(rooftop.Error);
        }

        // No already-posted guard, and that is deliberate: a deposit followed by
        // a balance is two payments against one reference. The receivable is what
        // stops more than is owed being taken.
        var refusal = await PeriodRefusalAsync(
            DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime), cancellationToken);

        if (refusal is not null)
        {
            return Result.Failure<JournalEntryDetail>(refusal);
        }

        var accounts = await AccountMapAsync(cancellationToken);
        if (accounts.IsFailure)
        {
            return Result.Failure<JournalEntryDetail>(accounts.Error);
        }

        if (payment.CreditRaised > 0m && !accounts.Value.ContainsKey(AccountCodes.CustomerCredits))
        {
            return Result.Failure<JournalEntryDetail>(
                LedgerErrors.ChartIncomplete([AccountCodes.CustomerCredits]));
        }

        JournalEntry entry;
        try
        {
            entry = JournalEntry.Post(
                Guid.NewGuid(),
                rooftop.Value.LegalEntityId,
                payment.RooftopId,
                DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime),
                JournalSource.Payment,
                payment.Reference,
                payment.Memo,
                payment.Currency,
                BuildPaymentLines(payment, accounts.Value),
                _clock.UtcNow,
                _currentUser.Id);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<JournalEntryDetail>(
                Error.Validation("accounting.will_not_balance", ex.Message));
        }

        _db.JournalEntries.Add(entry);

        // Deliberately NOT saved here. This is called inside the receivable's
        // transaction, and its SaveChanges commits the payment row and this entry
        // together — the pair is the whole point.
        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, PostPermission, AuditOutcome.Allowed,
                "JournalEntry", entry.Id.ToString(), payment.RooftopId.Value,
                $"Payment against {payment.Reference}", null, null),
            cancellationToken);

        return Result.Success(await DescribeAsync(entry, cancellationToken));
    }

    public Task<Result<JournalEntryDetail>> PostCreditApplicationAsync(
        CreditApplicationPosting application,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);

        return PostCreditMovementAsync(
            application.RooftopId,
            application.Reference,
            application.Currency,
            application.Amount,
            application.Memo,
            JournalSource.CreditApplied,
            PostPermission,
            accounts => BuildCreditApplicationLines(application, accounts),
            cancellationToken);
    }

    public Task<Result<JournalEntryDetail>> PostCreditRefundAsync(
        CreditRefundPosting refund,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refund);

        return PostCreditMovementAsync(
            refund.RooftopId,
            refund.Reference,
            refund.Currency,
            refund.Amount,
            refund.Memo,
            JournalSource.CreditRefunded,
            RefundPermission,
            accounts => BuildCreditRefundLines(refund, accounts),
            cancellationToken);
    }

    public async Task<Result<JournalEntryDetail>> PostWarrantyClaimPaymentAsync(
        WarrantyClaimPaymentPosting payment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payment);

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, PostPermission, payment.RooftopId, cancellationToken))
        {
            return Result.Failure<JournalEntryDetail>(LedgerErrors.Forbidden);
        }

        if (payment.Amount <= 0m)
        {
            return Result.Failure<JournalEntryDetail>(
                Error.Validation("accounting.warranty_payment_invalid", "A warranty payment is for an amount above zero."));
        }

        var rooftop = await _organization.GetRooftopAsync(payment.RooftopId, cancellationToken);
        if (rooftop.IsFailure)
        {
            return Result.Failure<JournalEntryDetail>(rooftop.Error);
        }

        var refusal = await PeriodRefusalAsync(
            DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime), cancellationToken);

        if (refusal is not null)
        {
            return Result.Failure<JournalEntryDetail>(refusal);
        }

        var accounts = await AccountMapAsync(cancellationToken);
        if (accounts.IsFailure)
        {
            return Result.Failure<JournalEntryDetail>(accounts.Error);
        }

        var cash = accounts.Value[AccountCodes.Cash];
        var owed = accounts.Value[AccountCodes.WarrantyReceivable];

        JournalEntry entry;
        try
        {
            entry = JournalEntry.Post(
                Guid.NewGuid(),
                rooftop.Value.LegalEntityId,
                payment.RooftopId,
                DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime),
                JournalSource.WarrantyClaimPaid,
                payment.Reference,
                payment.Memo,
                payment.Currency,
                [
                    (cash.Code, cash.Id, payment.Amount, 0m, "Money in"),
                    (owed.Code, owed.Id, 0m, payment.Amount, "Off what the manufacturer owed"),
                ],
                _clock.UtcNow,
                _currentUser.Id);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<JournalEntryDetail>(
                Error.Validation("accounting.will_not_balance", ex.Message));
        }

        _db.JournalEntries.Add(entry);

        // Deliberately NOT saved here — called inside the claim's own
        // transaction, the same contract PostProductCancellationAsync uses.
        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, PostPermission, AuditOutcome.Allowed,
                "JournalEntry", entry.Id.ToString(), payment.RooftopId.Value,
                payment.Memo, null, null),
            cancellationToken);

        return Result.Success(await DescribeAsync(entry, cancellationToken));
    }

    /// <summary>
    /// PostPermission, not RefundPermission: no cash leaves the business here,
    /// only a liability is raised — the same as applying a credit. Cash only
    /// actually leaves when that liability is later refunded, through
    /// PostCreditRefundAsync, which does hold RefundPermission.
    /// </summary>
    public Task<Result<JournalEntryDetail>> PostProductCancellationAsync(
        ProductCancellationPosting cancellation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cancellation);

        return PostCreditMovementAsync(
            cancellation.RooftopId,
            cancellation.Reference,
            cancellation.Currency,
            cancellation.RefundAmount,
            cancellation.Memo,
            JournalSource.ProductCancelled,
            PostPermission,
            accounts => BuildProductCancellationLines(cancellation, accounts),
            cancellationToken);
    }

    /// <summary>
    /// The half of a credit movement that is the same whichever direction it
    /// goes: the permission, the open period, the account 2200 has to exist, and
    /// an entry that is added but NOT saved because the sub-ledger's transaction
    /// owns the save.
    /// </summary>
    /// <remarks>
    /// Written once rather than twice because applying and refunding differ in
    /// exactly three things — the permission, the journal source, and the second
    /// line — and two copies of the rest would be two places for the period
    /// check to be forgotten.
    /// </remarks>
    private async Task<Result<JournalEntryDetail>> PostCreditMovementAsync(
        RooftopId rooftopId,
        string reference,
        string currency,
        decimal amount,
        string memo,
        JournalSource source,
        string permission,
        Func<IReadOnlyDictionary<string, Account>, List<(string, Guid, decimal, decimal, string?)>> lines,
        CancellationToken cancellationToken)
    {
        if (!await _access.IsAuthorizedAsync(_currentUser.Id, permission, rooftopId, cancellationToken))
        {
            return Result.Failure<JournalEntryDetail>(LedgerErrors.Forbidden);
        }

        if (amount <= 0m)
        {
            return Result.Failure<JournalEntryDetail>(
                Error.Validation("accounting.credit_invalid", "A credit movement is for an amount above zero."));
        }

        var rooftop = await _organization.GetRooftopAsync(rooftopId, cancellationToken);
        if (rooftop.IsFailure)
        {
            return Result.Failure<JournalEntryDetail>(rooftop.Error);
        }

        var refusal = await PeriodRefusalAsync(
            DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime), cancellationToken);

        if (refusal is not null)
        {
            return Result.Failure<JournalEntryDetail>(refusal);
        }

        var accounts = await AccountMapAsync(cancellationToken);
        if (accounts.IsFailure)
        {
            return Result.Failure<JournalEntryDetail>(accounts.Error);
        }

        if (!accounts.Value.ContainsKey(AccountCodes.CustomerCredits))
        {
            return Result.Failure<JournalEntryDetail>(
                LedgerErrors.ChartIncomplete([AccountCodes.CustomerCredits]));
        }

        JournalEntry entry;
        try
        {
            entry = JournalEntry.Post(
                Guid.NewGuid(),
                rooftop.Value.LegalEntityId,
                rooftopId,
                DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime),
                source,
                reference,
                memo,
                currency,
                lines(accounts.Value),
                _clock.UtcNow,
                _currentUser.Id);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<JournalEntryDetail>(
                Error.Validation("accounting.will_not_balance", ex.Message));
        }

        _db.JournalEntries.Add(entry);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, permission, AuditOutcome.Allowed,
                "JournalEntry", entry.Id.ToString(), rooftopId.Value,
                memo, null, null),
            cancellationToken);

        return Result.Success(await DescribeAsync(entry, cancellationToken));
    }

    public async Task<Result<JournalEntryDetail>> PostStockPurchaseAsync(
        StockPurchasePosting purchase,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(purchase);

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, PostPermission, purchase.RooftopId, cancellationToken))
        {
            return Result.Failure<JournalEntryDetail>(LedgerErrors.Forbidden);
        }

        // A car that cost nothing was not bought. Refused rather than posted as a
        // pair of noughts, because an entry saying a car was free is a claim, and
        // a missing entry is only an absence.
        if (purchase.Cost <= 0m)
        {
            return Result.Failure<JournalEntryDetail>(
                Error.Validation("accounting.cost_required", "A car taken into stock needs a cost above zero to post."));
        }

        var rooftop = await _organization.GetRooftopAsync(purchase.RooftopId, cancellationToken);
        if (rooftop.IsFailure)
        {
            return Result.Failure<JournalEntryDetail>(rooftop.Error);
        }

        // The reference is the stock number, so buying the same unit twice is
        // refused the way delivering the same deal twice is — but SCOPED TO THE
        // ROOFTOP, because a stock number is only unique within one lot. Both
        // rooftops of a group may legitimately hold a car numbered A1001, and the
        // first draft of this refused the second one with "already posted".
        //
        // Caught by InventoryTests.The_same_stock_number_is_allowed_at_a_different_rooftop,
        // which existed already and was right to. It is the same mistake the
        // workshop's job numbering makes on screen — a per-rooftop identifier
        // treated as though it were unique everywhere.
        var alreadyPosted = await _db.JournalEntries
            .AsNoTracking()
            .AnyAsync(
                e => e.Reference == purchase.Reference
                    && e.RooftopId == purchase.RooftopId
                    && e.Source == JournalSource.StockPurchase,
                cancellationToken);

        if (alreadyPosted)
        {
            return Result.Failure<JournalEntryDetail>(LedgerErrors.AlreadyPosted);
        }

        var refusal = await PeriodRefusalAsync(
            DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime), cancellationToken);

        if (refusal is not null)
        {
            return Result.Failure<JournalEntryDetail>(refusal);
        }

        var accounts = await AccountMapAsync(cancellationToken);
        if (accounts.IsFailure)
        {
            return Result.Failure<JournalEntryDetail>(accounts.Error);
        }

        JournalEntry entry;
        try
        {
            entry = JournalEntry.Post(
                Guid.NewGuid(),
                rooftop.Value.LegalEntityId,
                purchase.RooftopId,
                DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime),
                JournalSource.StockPurchase,
                purchase.Reference,
                purchase.Memo,
                purchase.Currency,
                BuildStockPurchaseLines(purchase, accounts.Value),
                _clock.UtcNow,
                _currentUser.Id);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<JournalEntryDetail>(
                Error.Validation("accounting.will_not_balance", ex.Message));
        }

        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, PostPermission, AuditOutcome.Allowed,
                "JournalEntry", entry.Id.ToString(), purchase.RooftopId.Value,
                $"Stock purchase {purchase.Reference}", null, null),
            cancellationToken);

        return Result.Success(await DescribeAsync(entry, cancellationToken));
    }

    public async Task<Result<JournalEntryDetail>> PostServiceInvoiceAsync(
        ServiceInvoicePosting invoice,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, PostPermission, invoice.RooftopId, cancellationToken))
        {
            return Result.Failure<JournalEntryDetail>(LedgerErrors.Forbidden);
        }

        // The two splits of the same money have to agree before anything is
        // posted. JournalEntry.Post would catch the imbalance anyway, but it would
        // report it as "the entry will not balance", which sends whoever reads it
        // hunting through account mappings. The real fault is upstream — a caller
        // whose payer split does not add up to what it says it sold — and saying
        // so by name is the difference between a five-minute fix and an afternoon.
        var byKind = invoice.Labour + invoice.Parts + invoice.Sublet;
        var byPayer = invoice.AmountDue + invoice.Warranty + invoice.Internal;
        if (byKind != byPayer)
        {
            return Result.Failure<JournalEntryDetail>(LedgerErrors.PayerSplitDisagrees(byKind, byPayer));
        }

        var rooftop = await _organization.GetRooftopAsync(invoice.RooftopId, cancellationToken);
        if (rooftop.IsFailure)
        {
            return Result.Failure<JournalEntryDetail>(rooftop.Error);
        }

        // One non-reversal entry per repair order, for the same reason as a
        // delivery: invoicing twice would double the revenue.
        var alreadyPosted = await _db.JournalEntries
            .AsNoTracking()
            .AnyAsync(
                e => e.Reference == invoice.Reference && e.Source == JournalSource.ServiceInvoice,
                cancellationToken);

        if (alreadyPosted)
        {
            return Result.Failure<JournalEntryDetail>(LedgerErrors.AlreadyPosted);
        }

        // The books have to be open for the month this lands in. Checked here
        // rather than in JournalEntry because the period is a fact about the
        // organization, not about the entry.
        var refusal = await PeriodRefusalAsync(
            DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime), cancellationToken);

        if (refusal is not null)
        {
            return Result.Failure<JournalEntryDetail>(refusal);
        }

        var accounts = await AccountMapAsync(cancellationToken);
        if (accounts.IsFailure)
        {
            return Result.Failure<JournalEntryDetail>(accounts.Error);
        }

        JournalEntry entry;
        try
        {
            entry = JournalEntry.Post(
                Guid.NewGuid(),
                rooftop.Value.LegalEntityId,
                invoice.RooftopId,
                DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime),
                JournalSource.ServiceInvoice,
                invoice.Reference,
                invoice.Memo,
                invoice.Currency,
                BuildServiceInvoiceLines(invoice, accounts.Value),
                _clock.UtcNow,
                _currentUser.Id);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<JournalEntryDetail>(
                Error.Validation("accounting.will_not_balance", ex.Message));
        }

        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, PostPermission, AuditOutcome.Allowed,
                "JournalEntry", entry.Id.ToString(), invoice.RooftopId.Value,
                $"Service invoice {invoice.Reference}", null, null),
            cancellationToken);

        return Result.Success(await DescribeAsync(entry, cancellationToken));
    }

    public async Task<Result<JournalEntryDetail>> ReverseAsync(
        Guid entryId,
        string reason,
        CancellationToken cancellationToken)
    {
        var original = await _db.JournalEntries
            .AsNoTracking()
            .Include(e => e.Lines)
            .SingleOrDefaultAsync(e => e.Id == entryId, cancellationToken);

        if (original is null)
        {
            return Result.Failure<JournalEntryDetail>(LedgerErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, ReversePermission, original.RooftopId, cancellationToken))
        {
            return Result.Failure<JournalEntryDetail>(LedgerErrors.Forbidden);
        }

        if (original.Source == JournalSource.Reversal)
        {
            return Result.Failure<JournalEntryDetail>(LedgerErrors.CannotReverseAReversal);
        }

        var alreadyReversed = await _db.JournalEntries
            .AsNoTracking()
            .AnyAsync(e => e.ReversesEntryId == entryId, cancellationToken);

        if (alreadyReversed)
        {
            return Result.Failure<JournalEntryDetail>(LedgerErrors.AlreadyReversed);
        }

        // A reversal is dated today, so it lands in today's month — the closed
        // month it corrects is left exactly as it was reported, which is the
        // whole reason reversals exist.
        var refusal = await PeriodRefusalAsync(
            DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime), cancellationToken);

        if (refusal is not null)
        {
            return Result.Failure<JournalEntryDetail>(refusal);
        }

        JournalEntry reversal;
        try
        {
            reversal = original.BuildReversal(
                Guid.NewGuid(),
                DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime),
                _clock.UtcNow,
                _currentUser.Id,
                reason);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<JournalEntryDetail>(Error.Validation("accounting.invalid_reversal", ex.Message));
        }

        _db.JournalEntries.Add(reversal);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, ReversePermission, AuditOutcome.Allowed,
                "JournalEntry", reversal.Id.ToString(), original.RooftopId.Value,
                $"Reversed {entryId}", null, null),
            cancellationToken);

        return Result.Success(await DescribeAsync(reversal, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<AccountingPeriodView>>> ListPeriodsAsync(
        CancellationToken cancellationToken)
    {
        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<IReadOnlyList<AccountingPeriodView>>(LedgerErrors.Forbidden);
        }

        var periods = await _db.AccountingPeriods
            .AsNoTracking()
            .Include(p => p.History)
            .OrderByDescending(p => p.Year)
            .ThenByDescending(p => p.Month)
            .ToListAsync(cancellationToken);

        // How many entries each month holds. A manager about to close one wants
        // to know whether it is the month they think it is.
        var counts = await _db.JournalEntries
            .AsNoTracking()
            .GroupBy(e => new { e.EntryDate.Year, e.EntryDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<AccountingPeriodView>>(
            periods.Select(p => Describe(
                p,
                counts.FirstOrDefault(c => c.Year == p.Year && c.Month == p.Month)?.Count ?? 0)).ToList());
    }

    public Task<Result<AccountingPeriodView>> OpenPeriodAsync(
        int year,
        int month,
        string? note,
        CancellationToken cancellationToken) =>
        ChangePeriodAsync(year, month, ClosePeriodPermission, PeriodAction.Open, note, cancellationToken);

    public Task<Result<AccountingPeriodView>> ClosePeriodAsync(
        int year,
        int month,
        string? note,
        CancellationToken cancellationToken) =>
        ChangePeriodAsync(year, month, ClosePeriodPermission, PeriodAction.Close, note, cancellationToken);

    public Task<Result<AccountingPeriodView>> ReopenPeriodAsync(
        int year,
        int month,
        string reason,
        CancellationToken cancellationToken) =>
        ChangePeriodAsync(year, month, ReopenPeriodPermission, PeriodAction.Reopen, reason, cancellationToken);

    private enum PeriodAction
    {
        Open,
        Close,
        Reopen,
    }

    private async Task<Result<AccountingPeriodView>> ChangePeriodAsync(
        int year,
        int month,
        string permission,
        PeriodAction action,
        string? note,
        CancellationToken cancellationToken)
    {
        // Organization-wide, always. The books close as a whole; one lot does not
        // close the group's month, and one lot does not reopen it either.
        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, permission, cancellationToken);
        if (!scope.IsOrganizationWide)
        {
            await _audit.RecordAsync(
                AuditEntry.Denied(
                    _currentUser.Id, permission, "AccountingPeriod", $"{year}-{month:00}", null,
                    $"Attempted to {action.ToString().ToLowerInvariant()} an accounting period."),
                cancellationToken);

            return Result.Failure<AccountingPeriodView>(LedgerErrors.PeriodForbidden);
        }

        var period = await _db.AccountingPeriods
            .Include(p => p.History)
            .SingleOrDefaultAsync(p => p.Year == year && p.Month == month, cancellationToken);

        try
        {
            switch (action)
            {
                case PeriodAction.Open when period is not null:
                    return Result.Failure<AccountingPeriodView>(LedgerErrors.PeriodAlreadyExists);

                case PeriodAction.Open:
                    period = AccountingPeriod.Open(
                        Guid.NewGuid(), year, month, _clock.UtcNow, _currentUser.Id, note);
                    _db.AccountingPeriods.Add(period);
                    break;

                case PeriodAction.Close when period is null:
                case PeriodAction.Reopen when period is null:
                    return Result.Failure<AccountingPeriodView>(LedgerErrors.PeriodNotOpened);

                case PeriodAction.Close:
                    period.Close(_clock.UtcNow, _currentUser.Id, note);
                    break;

                case PeriodAction.Reopen:
                    period.Reopen(_clock.UtcNow, _currentUser.Id, note ?? string.Empty);
                    break;
            }
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<AccountingPeriodView>(Error.Validation("accounting.invalid_period", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<AccountingPeriodView>(Error.Conflict("accounting.period_state", ex.Message));
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(
                _currentUser.Id, permission, AuditOutcome.Allowed,
                "AccountingPeriod", period!.Id.ToString(), null,
                action switch
                {
                    PeriodAction.Open => $"Opened the books for {year}-{month:00}.",
                    PeriodAction.Close => $"Closed {year}-{month:00}.",
                    _ => $"Reopened {year}-{month:00}. Reason: {note}",
                },
                null, null),
            cancellationToken);

        var entries = await _db.JournalEntries
            .AsNoTracking()
            .CountAsync(e => e.EntryDate.Year == year && e.EntryDate.Month == month, cancellationToken);

        return Result.Success(Describe(period, entries));
    }

    /// <summary>
    /// Whether the books will take an entry dated here. Called before every
    /// posting, so a month nobody has opened and a month somebody has closed both
    /// refuse — with different messages, because they need different actions.
    /// </summary>
    private async Task<Error?> PeriodRefusalAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var period = await _db.AccountingPeriods
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Year == date.Year && p.Month == date.Month, cancellationToken);

        if (period is null)
        {
            return Error.Conflict(
                "accounting.period_not_opened",
                $"The books for {date.Year}-{date.Month:00} have not been opened, so nothing can be "
                    + "posted into that month yet.");
        }

        if (period.State == AccountingPeriodState.Closed)
        {
            return Error.Conflict(
                "accounting.period_closed",
                $"{date.Year}-{date.Month:00} is closed. Reopen it if something genuinely belongs in "
                    + "that month, or post this to an open one.");
        }

        // The year's own gate, independent of the month's. A reopened month
        // inside a still-closed year must still refuse, or the two gates would
        // not actually agree with each other — see FiscalYear.
        var fiscalYear = await _db.FiscalYears
            .AsNoTracking()
            .SingleOrDefaultAsync(y => y.Year == date.Year, cancellationToken);

        if (fiscalYear is { State: FiscalYearState.Closed })
        {
            return Error.Conflict(
                "accounting.year_closed",
                $"{date.Year} is closed. Reopen the year if something genuinely belongs in it, or post "
                    + "this to an open one.");
        }

        return null;
    }

    public async Task<Result<IReadOnlyList<FiscalYearView>>> ListFiscalYearsAsync(
        CancellationToken cancellationToken)
    {
        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<IReadOnlyList<FiscalYearView>>(LedgerErrors.Forbidden);
        }

        var years = await _db.FiscalYears
            .AsNoTracking()
            .Include(y => y.History)
            .OrderByDescending(y => y.Year)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<FiscalYearView>>(years.Select(DescribeYear).ToList());
    }

    public async Task<Result<FiscalYearView>> CloseYearAsync(
        int year,
        string? note,
        CancellationToken cancellationToken)
    {
        // Organization-wide, the same reason ChangePeriodAsync requires it: the
        // books close as a whole.
        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ClosePeriodPermission, cancellationToken);
        if (!scope.IsOrganizationWide)
        {
            await _audit.RecordAsync(
                AuditEntry.Denied(
                    _currentUser.Id, ClosePeriodPermission, "FiscalYear", $"{year}", null,
                    $"Attempted to close {year}."),
                cancellationToken);

            return Result.Failure<FiscalYearView>(LedgerErrors.PeriodForbidden);
        }

        var months = await _db.AccountingPeriods
            .AsNoTracking()
            .Where(p => p.Year == year)
            .ToListAsync(cancellationToken);

        var openMonths = Enumerable.Range(1, 12)
            .Where(month => months.Find(p => p.Month == month) is not { State: AccountingPeriodState.Closed })
            .ToList();

        if (openMonths.Count > 0)
        {
            return Result.Failure<FiscalYearView>(Error.Conflict(
                "accounting.months_not_closed",
                $"{year} still has open or never-opened months: "
                    + string.Join(", ", openMonths.Select(m => $"{year}-{m:00}"))
                    + ". Close every month before closing the year."));
        }

        var fiscalYear = await _db.FiscalYears
            .Include(y => y.History)
            .SingleOrDefaultAsync(y => y.Year == year, cancellationToken);

        if (fiscalYear is { State: FiscalYearState.Closed })
        {
            return Result.Failure<FiscalYearView>(
                Error.Conflict("accounting.year_already_closed", $"{year} is already closed."));
        }

        // Every revenue and expense account's balance for the calendar year,
        // computed the same way BalanceSheetAsync computes EarningsToDate — from
        // the account's own debit/credit totals, not from a running figure kept
        // anywhere else.
        var entries = _db.JournalEntries
            .AsNoTracking()
            .Where(e => e.EntryDate.Year == year);

        var totals = await (
            from line in _db.JournalLines.AsNoTracking()
            join entry in entries on line.EntryId equals entry.Id
            group line by line.AccountId into byAccount
            select new
            {
                AccountId = byAccount.Key,
                Debits = byAccount.Sum(l => l.Debit),
                Credits = byAccount.Sum(l => l.Credit),
            }).ToListAsync(cancellationToken);

        var accounts = await _db.Accounts.AsNoTracking().ToListAsync(cancellationToken);

        var retainedEarnings = accounts.Find(a => a.Code == AccountCodes.RetainedEarnings);
        if (retainedEarnings is null)
        {
            return Result.Failure<FiscalYearView>(LedgerErrors.ChartIncomplete([AccountCodes.RetainedEarnings]));
        }

        var lines = new List<(string AccountCode, Guid AccountId, decimal Debit, decimal Credit, string? Memo)>();

        foreach (var total in totals)
        {
            var account = accounts.Find(a => a.Id == total.AccountId);
            if (account is not { Kind: AccountKind.Revenue or AccountKind.Expense })
            {
                continue;
            }

            var balance = account.IncreasesOnDebit
                ? total.Debits - total.Credits
                : total.Credits - total.Debits;

            if (balance == 0m)
            {
                continue;
            }

            // Zeroing means posting the opposite of what the account normally
            // carries: a debit-normal account with a positive balance clears
            // with a credit for that amount, and a credit-normal account with a
            // positive balance clears with a debit. A negative balance — an
            // account that ended up the "wrong" way round, which is unusual but
            // not invalid — clears the same way with the sides swapped.
            var clearingDebit = account.IncreasesOnDebit ? Math.Max(-balance, 0m) : Math.Max(balance, 0m);
            var clearingCredit = account.IncreasesOnDebit ? Math.Max(balance, 0m) : Math.Max(-balance, 0m);

            lines.Add((account.Code, account.Id, clearingDebit, clearingCredit, $"Closed for {year}"));
        }

        if (lines.Count == 0)
        {
            return Result.Failure<FiscalYearView>(Error.Conflict(
                "accounting.nothing_to_close",
                $"{year} has no revenue or expense activity, so there is nothing for a closing entry to carry."));
        }

        var netProfit = lines.Sum(l => l.Debit) - lines.Sum(l => l.Credit);

        // What clears the revenue and expense accounts must land somewhere, and
        // that somewhere is RetainedEarnings — a profit credits it, a loss debits
        // it, the same rule every other equity account already follows.
        lines.Add(netProfit > 0m
            ? (retainedEarnings.Code, retainedEarnings.Id, 0m, netProfit, $"Net profit for {year}")
            : (retainedEarnings.Code, retainedEarnings.Id, -netProfit, 0m, $"Net loss for {year}"));

        // The closing entry needs a rooftop and a legal entity even though the
        // year itself is organization-wide — the first rooftop on record stands
        // in, the same arbitrary-but-stable choice a value that cannot be null
        // needs when nothing about the event is really about one location.
        // Through IOrganization rather than the Rooftop entity directly — a
        // sibling module's entity never crosses the boundary (ADR-008).
        var structure = await _organization.GetStructureAsync(cancellationToken);
        var rooftop = structure.IsSuccess
            ? structure.Value.LegalEntities.SelectMany(e => e.Rooftops).FirstOrDefault()
            : null;

        if (rooftop is null)
        {
            return Result.Failure<FiscalYearView>(Error.Conflict(
                "accounting.no_rooftop", "There is no rooftop yet to post the closing entry against."));
        }

        // Mixing currencies would produce a closing entry that means nothing,
        // the same reason CoveredEntriesAsync refuses to total across them.
        var currencies = await entries.Select(e => e.Currency).Distinct().ToListAsync(cancellationToken);
        if (currencies.Count > 1)
        {
            return Result.Failure<FiscalYearView>(LedgerErrors.MixedCurrencies(currencies));
        }

        var currency = currencies.Count == 1 ? currencies[0] : "USD";

        JournalEntry closingEntry;
        try
        {
            closingEntry = JournalEntry.Post(
                Guid.NewGuid(),
                rooftop.LegalEntityId,
                rooftop.Id,
                new DateOnly(year, 12, 31),
                JournalSource.YearEndClose,
                $"FY{year}",
                (note ?? $"Year-end close for {year}").Trim(),
                currency,
                lines,
                _clock.UtcNow,
                _currentUser.Id);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<FiscalYearView>(Error.Conflict("accounting.will_not_balance", ex.Message));
        }

        _db.JournalEntries.Add(closingEntry);

        if (fiscalYear is null)
        {
            fiscalYear = FiscalYear.Open(Guid.NewGuid(), year, _clock.UtcNow, _currentUser.Id, null);
            _db.FiscalYears.Add(fiscalYear);
        }

        fiscalYear.Close(closingEntry.Id, _clock.UtcNow, _currentUser.Id, note);

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(
                _currentUser.Id, ClosePeriodPermission, AuditOutcome.Allowed,
                "FiscalYear", fiscalYear.Id.ToString(), null,
                $"Closed {year}. Net {(netProfit >= 0m ? "profit" : "loss")} {Math.Abs(netProfit):0.00} "
                    + "carried to retained earnings.",
                null, null),
            cancellationToken);

        return Result.Success(DescribeYear(fiscalYear));
    }

    public async Task<Result<FiscalYearView>> ReopenYearAsync(
        int year,
        string reason,
        CancellationToken cancellationToken)
    {
        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReopenPeriodPermission, cancellationToken);
        if (!scope.IsOrganizationWide)
        {
            await _audit.RecordAsync(
                AuditEntry.Denied(
                    _currentUser.Id, ReopenPeriodPermission, "FiscalYear", $"{year}", null,
                    $"Attempted to reopen {year}."),
                cancellationToken);

            return Result.Failure<FiscalYearView>(LedgerErrors.PeriodForbidden);
        }

        var fiscalYear = await _db.FiscalYears
            .Include(y => y.History)
            .SingleOrDefaultAsync(y => y.Year == year, cancellationToken);

        if (fiscalYear is null)
        {
            return Result.Failure<FiscalYearView>(
                Error.NotFound("accounting.year_not_closed", $"{year} has never been closed."));
        }

        try
        {
            fiscalYear.Reopen(_clock.UtcNow, _currentUser.Id, reason);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<FiscalYearView>(Error.Conflict("accounting.year_state", ex.Message));
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<FiscalYearView>(Error.Validation("accounting.reason_required", ex.Message));
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(
                _currentUser.Id, ReopenPeriodPermission, AuditOutcome.Allowed,
                "FiscalYear", fiscalYear.Id.ToString(), null,
                $"Reopened {year}. Reason: {reason}",
                null, null),
            cancellationToken);

        return Result.Success(DescribeYear(fiscalYear));
    }

    private static FiscalYearView DescribeYear(FiscalYear fiscalYear) =>
        new(
            fiscalYear.Id,
            fiscalYear.Year,
            fiscalYear.State.ToString(),
            fiscalYear.StartsOn,
            fiscalYear.EndsOn,
            fiscalYear.ClosedAt,
            fiscalYear.ClosedByUserId,
            fiscalYear.ClosingEntryId,
            fiscalYear.History
                .OrderBy(h => h.OccurredAt)
                .ThenBy(h => h.Sequence)
                .Select(h => new FiscalYearChangeView(
                    h.FromState?.ToString(), h.ToState.ToString(), h.OccurredAt, h.ChangedByUserId, h.Note))
                .ToList());

    private static AccountingPeriodView Describe(AccountingPeriod period, int entries) =>
        new(
            period.Id,
            period.Year,
            period.Month,
            period.State.ToString(),
            period.StartsOn,
            period.EndsOn,
            period.ClosedAt,
            period.ClosedByUserId,
            entries,
            period.History
                .OrderBy(h => h.OccurredAt)
                .ThenBy(h => h.Sequence)
                .Select(h => new AccountingPeriodChangeView(
                    h.FromState?.ToString(), h.ToState.ToString(), h.OccurredAt, h.ChangedByUserId, h.Note))
                .ToList());

    /// <summary>
    /// The one place that decides which accounts a retail sale moves.
    ///
    /// Debits:  the cash taken, the trade car we now own, and any discount given.
    /// Credits: the price of the car, the fees, and the cash paid out to settle
    ///          what the customer still owed on their trade.
    /// Then the cost of the car sold is moved out of inventory and into cost of
    /// sales, which is what turns revenue into a gross profit anybody can check.
    /// </summary>
    /// <summary>
    /// Money arriving against a bill: the bank goes up and what the customer owes
    /// goes down. It touches no revenue account, because the sale was recognised
    /// when the car left or the job was invoiced — being paid is not a second
    /// sale, and treating it as one would double every figure a dealer reads.
    /// </summary>
    private static List<(string, Guid, decimal, decimal, string?)> BuildPaymentLines(
        PaymentPosting p,
        IReadOnlyDictionary<string, Account> accounts)
    {
        var cash = accounts[AccountCodes.Cash];
        var owed = accounts[AccountCodes.AccountsReceivable];

        // The ordinary case, and the one worth keeping to two lines.
        if (p.CreditRaised <= 0m)
        {
            return
            [
                (cash.Code, cash.Id, p.Amount, 0m, "Money in"),
                (owed.Code, owed.Id, 0m, p.Amount, "Off what they owed"),
            ];
        }

        // Somebody handed over more than the bill. ONE entry, because they
        // performed one act: the whole amount arrives as cash, the bill's share
        // clears the receivable, and the rest becomes money we owe them back.
        var credits = accounts[AccountCodes.CustomerCredits];

        return
        [
            (cash.Code, cash.Id, p.Amount + p.CreditRaised, 0m, "Money in"),
            (owed.Code, owed.Id, 0m, p.Amount, "Off what they owed"),
            (credits.Code, credits.Id, 0m, p.CreditRaised, "Overpaid, and owed back"),
        ];
    }

    /// <summary>
    /// A credit put against a bill. Two lines and no cash: the money arrived
    /// when the overpayment was taken, and this is where it stops being owed
    /// back and starts having paid for something.
    /// </summary>
    private static List<(string, Guid, decimal, decimal, string?)> BuildCreditApplicationLines(
        CreditApplicationPosting a,
        IReadOnlyDictionary<string, Account> accounts)
    {
        var credits = accounts[AccountCodes.CustomerCredits];
        var owed = accounts[AccountCodes.AccountsReceivable];

        return
        [
            (credits.Code, credits.Id, a.Amount, 0m, "Credit used"),
            (owed.Code, owed.Id, 0m, a.Amount, "Off what they owed"),
        ];
    }

    /// <summary>The credit handed back: the liability goes and so does the cash.</summary>
    private static List<(string, Guid, decimal, decimal, string?)> BuildCreditRefundLines(
        CreditRefundPosting r,
        IReadOnlyDictionary<string, Account> accounts)
    {
        var credits = accounts[AccountCodes.CustomerCredits];
        var cash = accounts[AccountCodes.Cash];

        return
        [
            (credits.Code, credits.Id, r.Amount, 0m, "Credit refunded"),
            (cash.Code, cash.Id, 0m, r.Amount, "Money out"),
        ];
    }

    /// <summary>
    /// An F&amp;I product cancelled: the revenue recognized at delivery is
    /// reversed, and the same amount becomes a liability owed back to the
    /// customer. Deliberately silent on the cost side — this system has no
    /// modelled way to know whether the provider actually refunds the
    /// dealership, so it states only the half it can stand behind rather than
    /// inventing a provider-refund entry nothing confirms happened.
    /// </summary>
    private static List<(string, Guid, decimal, decimal, string?)> BuildProductCancellationLines(
        ProductCancellationPosting c,
        IReadOnlyDictionary<string, Account> accounts)
    {
        var revenue = accounts[AccountCodes.FinanceProductRevenue];
        var credits = accounts[AccountCodes.CustomerCredits];

        return
        [
            (revenue.Code, revenue.Id, c.RefundAmount, 0m, "Product cancelled"),
            (credits.Code, credits.Id, 0m, c.RefundAmount, "Owed back to the customer"),
        ];
    }

    /// <summary>
    /// A car bought onto the lot: the value arrives as an asset, and whatever
    /// paid for it leaves. Two lines, and the smallest entry in this file.
    /// </summary>
    /// <remarks>
    /// The pair to <see cref="BuildDeliveryLines"/>, which credits
    /// <see cref="AccountCodes.VehicleInventory"/> at cost when the car goes out.
    /// Received and delivered at the same cost, the two net to nothing on 1300
    /// and leave the gross where it belongs — which is the arithmetic the
    /// regression test asserts.
    /// </remarks>
    private static List<(string, Guid, decimal, decimal, string?)> BuildStockPurchaseLines(
        StockPurchasePosting p,
        IReadOnlyDictionary<string, Account> accounts)
    {
        var inventory = accounts[AccountCodes.VehicleInventory];

        // Whoever actually paid. A floorplanned car is the lender's money until it
        // sells; an outright purchase is the dealership's own.
        var paidBy = p.Floorplanned
            ? accounts[AccountCodes.FloorplanPayable]
            : accounts[AccountCodes.Cash];

        return
        [
            (inventory.Code, inventory.Id, p.Cost, 0m, "Car onto the lot"),
            (paidBy.Code, paidBy.Id, 0m, p.Cost,
                p.Floorplanned ? "Financed by the floorplan lender" : "Paid for the car"),
        ];
    }

    private static List<(string, Guid, decimal, decimal, string?)> BuildDeliveryLines(
        DeliveryPosting d,
        IReadOnlyDictionary<string, Account> accounts)
    {
        var lines = new List<(string, Guid, decimal, decimal, string?)>();

        void Line(string code, decimal debit, decimal credit, string? memo)
        {
            if (debit == 0m && credit == 0m)
            {
                return;
            }

            var account = accounts[code];
            lines.Add((account.Code, account.Id, debit, credit, memo));
        }

        // The discount arrives negative on the deal; it is a debit here.
        var discount = Math.Abs(d.Discount);

        // Debited to what the customer OWES, not to cash. Until 2026-09-10 this
        // line debited 1000 and asserted that every customer paid in full the
        // moment they were billed — so a fleet account, a deposit, a part-payment
        // and a lender's cheque were all unrepresentable, and the bank balance was
        // wrong by everything anybody was still owed. Money arriving is a separate
        // entry now: PostPaymentAsync moves 1100 to 1000 when it actually turns up.
        Line(AccountCodes.AccountsReceivable, d.AmountDue, 0m, "Owed by the customer");
        Line(AccountCodes.TradeInventory, d.TradeAllowance, 0m, "Trade taken in");
        Line(AccountCodes.SalesDiscounts, discount, 0m, "Discount given");
        Line(AccountCodes.VehicleSalesRevenue, 0m, d.VehiclePrice, "Sale of vehicle");
        Line(AccountCodes.FeeRevenue, 0m, d.Fees, "Fees");
        Line(AccountCodes.Cash, 0m, d.TradePayoff, "Paid to settle the trade");

        // F&I: its own revenue line, and its own cost paid to the provider. Kept
        // apart from the car's figures because a dealer principal reads front-end
        // and back-end gross as two separate businesses.
        Line(AccountCodes.FinanceProductRevenue, 0m, d.ProductRevenue, "Warranties and cover sold");
        Line(AccountCodes.CostOfFinanceProducts, d.ProductCost, 0m, "Paid to the providers");
        Line(AccountCodes.Cash, 0m, d.ProductCost, "Paid out to the providers");

        // Tax the dealership is holding for the state. A liability, not revenue —
        // and the line without which the entry does not balance, because
        // AmountDue above already includes it.
        Line(AccountCodes.SalesTaxPayable, 0m, d.TaxCollected, "Sales tax collected");

        // Relieving inventory at cost, so gross profit is visible.
        Line(AccountCodes.CostOfVehicleSales, d.VehicleCost, 0m, "Cost of the vehicle sold");
        Line(AccountCodes.VehicleInventory, 0m, d.VehicleCost, "Vehicle off the lot");

        return lines;
    }

    /// <summary>
    /// The one place that decides which accounts a service invoice moves.
    ///
    /// Debit the cash taken; credit labour, parts, and sublet separately, because
    /// "we sold £900 of service" is useless to a workshop manager and "£600 labour,
    /// £300 parts" is the number they run the department on.
    ///
    /// There is no cost side. Relieving parts at cost needs a parts inventory and
    /// there is not one yet — so this posts revenue honestly and leaves gross
    /// profit on service plainly unavailable rather than quietly wrong.
    /// </summary>
    private static List<(string, Guid, decimal, decimal, string?)> BuildServiceInvoiceLines(
        ServiceInvoicePosting invoice,
        IReadOnlyDictionary<string, Account> accounts)
    {
        var lines = new List<(string, Guid, decimal, decimal, string?)>();

        void Line(string code, decimal debit, decimal credit, string? memo)
        {
            if (debit == 0m && credit == 0m)
            {
                return;
            }

            var account = accounts[code];
            lines.Add((account.Code, account.Id, debit, credit, memo));
        }

        // The three payers, debited. Warranty is a receivable rather than cash
        // because the claim has not been paid — and internal is a charge to the
        // dealership rather than to anybody at all.
        Line(AccountCodes.AccountsReceivable, invoice.AmountDue, 0m, "Owed by the customer");
        Line(AccountCodes.WarrantyReceivable, invoice.Warranty, 0m, "Claimed from the manufacturer");
        // Reconditioning a car we own is not an expense — it is part of what that
        // car cost us, and putting it anywhere else makes used-vehicle gross
        // flatter itself by exactly the amount spent making the car saleable.
        Line(AccountCodes.VehicleInventory, invoice.InternalCapitalised, 0m, "Reconditioning, onto the car");
        Line(AccountCodes.InternalServiceCharge,
            invoice.Internal - invoice.InternalCapitalised, 0m, "Work done for the dealership itself");

        Line(AccountCodes.LabourRevenue, 0m, invoice.Labour, "Labour sold");
        Line(AccountCodes.PartsRevenue, 0m, invoice.Parts, "Parts sold");
        Line(AccountCodes.SubletRevenue, 0m, invoice.Sublet, "Sublet work");

        // The cost side, and the reason service now has a gross profit figure.
        // These two are equal and opposite, so they balance on their own and
        // cannot change whether the entry balances overall — the value simply
        // moves off the shelf and into cost of sales. Zero means nothing on the
        // job came off a shelf, and Line() skips it rather than posting a pair of
        // noughts.
        Line(AccountCodes.CostOfPartsSales, invoice.PartsCost, 0m, "Parts used, at cost");
        Line(AccountCodes.PartsInventory, 0m, invoice.PartsCost, "Off the shelf");

        return lines;
    }

    private async Task<Result<IReadOnlyDictionary<string, Account>>> AccountMapAsync(CancellationToken cancellationToken)
    {
        var accounts = await _db.Accounts.AsNoTracking().ToDictionaryAsync(a => a.Code, cancellationToken);

        string[] required =
        [
            AccountCodes.Cash, AccountCodes.AccountsReceivable,
            AccountCodes.VehicleInventory, AccountCodes.TradeInventory,
            AccountCodes.VehicleSalesRevenue, AccountCodes.FeeRevenue,
            AccountCodes.SalesDiscounts, AccountCodes.CostOfVehicleSales,
            AccountCodes.LabourRevenue, AccountCodes.PartsRevenue, AccountCodes.SubletRevenue,
            AccountCodes.PartsInventory, AccountCodes.CostOfPartsSales,
            AccountCodes.FinanceProductRevenue, AccountCodes.CostOfFinanceProducts,
            AccountCodes.WarrantyReceivable, AccountCodes.InternalServiceCharge,
            AccountCodes.SalesTaxPayable,
        ];

        // 2200 is deliberately NOT in that list. It arrived on 2026-09-14, after
        // dealerships already existed, and this list is checked before EVERY
        // posting — so requiring it here would stop a dealership delivering cars
        // because of an account only an overpayment needs. The three places that
        // do need it ask for it themselves, and get a chart-incomplete error
        // naming exactly what is missing.

        var missing = required.Where(code => !accounts.ContainsKey(code)).ToList();
        if (missing.Count > 0)
        {
            return Result.Failure<IReadOnlyDictionary<string, Account>>(
                LedgerErrors.ChartIncomplete(missing));
        }

        return Result.Success<IReadOnlyDictionary<string, Account>>(accounts);
    }

    private async Task<JournalEntryDetail> DescribeAsync(JournalEntry entry, CancellationToken cancellationToken)
    {
        var names = await _db.Accounts
            .AsNoTracking()
            .ToDictionaryAsync(a => a.Id, a => a.Name, cancellationToken);

        return new JournalEntryDetail(
            entry.Id,
            entry.LegalEntityId,
            entry.RooftopId,
            entry.EntryDate,
            entry.Source.ToString(),
            entry.Reference,
            entry.Memo,
            entry.Currency,
            entry.TotalDebits.Amount,
            entry.TotalCredits.Amount,
            entry.PostedAt,
            entry.PostedByUserId,
            entry.ReversesEntryId,
            entry.Lines
                .OrderByDescending(l => l.Debit)
                .ThenBy(l => l.AccountCode, StringComparer.Ordinal)
                .Select(l => new JournalLineView(
                    l.AccountCode,
                    names.TryGetValue(l.AccountId, out var name) ? name : "(account removed)",
                    l.Debit,
                    l.Credit,
                    l.Memo))
                .ToList());
    }
}

/// <summary>Stable error codes for the Accounting capability (doc 06 §6).</summary>
internal static class LedgerErrors
{
    public static Error Forbidden { get; } = Error.Forbidden(
        "accounting.forbidden",
        "You do not have access to this rooftop's ledger.");

    public static Error AlreadyPosted { get; } = Error.Conflict(
        "accounting.already_posted",
        "This delivery is already in the ledger. Posting it twice would double the revenue.");

    public static Error AlreadyReversed { get; } = Error.Conflict(
        "accounting.already_reversed",
        "That entry has already been reversed.");

    public static Error PeriodForbidden { get; } = Error.Forbidden(
        "accounting.period_forbidden",
        "Opening, closing, and reopening the books needs organization-wide permission. "
            + "The books close as a whole, not one location at a time.");

    public static Error PeriodAlreadyExists { get; } = Error.Conflict(
        "accounting.period_exists",
        "The books for that month are already open.");

    public static Error PeriodNotOpened { get; } = Error.NotFound(
        "accounting.period_not_opened",
        "The books for that month have never been opened.");

    public static Error CannotReverseAReversal { get; } = Error.Conflict(
        "accounting.cannot_reverse_a_reversal",
        "Reverse the original entry instead, or post a fresh one.");

    public static Error ChartIncomplete(IEnumerable<string> missing) => Error.Validation(
        "accounting.chart_incomplete",
        $"The chart of accounts is missing: {string.Join(", ", missing)}.");

    /// <summary>
    /// The work sold and the work paid for are not the same number. Reported
    /// against the caller rather than as a balancing failure, because the ledger
    /// mapping is fine — what arrived was already inconsistent.
    /// </summary>
    public static Error PayerSplitDisagrees(decimal byKind, decimal byPayer) => Error.Validation(
        "accounting.payer_split_disagrees",
        $"Work sold totals {byKind} but the customer, warranty and internal shares total {byPayer}. " +
        "Every line has to be paid for by exactly one of them.");

    /// <summary>
    /// A hand-written entry naming an account that is not in the chart. Distinct
    /// from <see cref="ChartIncomplete"/>, which is the dealership missing an
    /// account the SYSTEM needs: this is a person mistyping one.
    /// </summary>
    public static Error UnknownAccounts(IEnumerable<string> codes) => Error.Validation(
        "accounting.unknown_accounts",
        $"No account in the chart has the code {string.Join(", ", codes)}.");

    public static Error MixedCurrencies(IEnumerable<string> currencies) => Error.Validation(
        "accounting.mixed_currencies",
        $"These entries are in more than one currency ({string.Join(", ", currencies)}). "
        + "Totalling them would produce a number that means nothing — narrow the period "
        + "or the rooftop.");
}
