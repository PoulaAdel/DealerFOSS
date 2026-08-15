// AccountingService — reading the ledger, and the two ways something gets into
// it.
//
// Use:  through IAccounting.
// Edit: the posting rules live in JournalEntry, not here, so they hold for any
//       caller. This class does three jobs: scope the reads by rooftop, translate
//       a business event into the accounts it moves, and refuse to reverse
//       something twice.
//
//       The account map below is the one place that decides which accounts a
//       sale touches. When a real chart of accounts arrives it becomes
//       configuration; until then it is deliberately in one readable method
//       rather than scattered.

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

    public async Task<Result<IReadOnlyList<JournalEntrySummary>>> ListAsync(
        JournalQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<IReadOnlyList<JournalEntrySummary>>(LedgerErrors.Forbidden);
        }

        if (query.RooftopId is { } requested && !scope.Covers(requested))
        {
            return Result.Failure<IReadOnlyList<JournalEntrySummary>>(LedgerErrors.Forbidden);
        }

        var take = Math.Clamp(query.Limit <= 0 ? 50 : query.Limit, 1, MaxResults);
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

        var rows = await entries
            .OrderByDescending(e => e.PostedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<JournalEntrySummary>>(
            rows.Select(e => new JournalEntrySummary(
                e.Id,
                e.RooftopId,
                e.EntryDate,
                e.Source.ToString(),
                e.Reference,
                e.Memo,
                e.TotalDebits.Amount,
                e.Currency,
                e.ReversesEntryId is not null)).ToList());
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

        return null;
    }

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

        Line(AccountCodes.Cash, d.AmountDue, 0m, "Taken from the customer");
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
        Line(AccountCodes.Cash, invoice.AmountDue, 0m, "Taken from the customer");
        Line(AccountCodes.WarrantyReceivable, invoice.Warranty, 0m, "Claimed from the manufacturer");
        Line(AccountCodes.InternalServiceCharge, invoice.Internal, 0m, "Work done for the dealership itself");

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
            AccountCodes.Cash, AccountCodes.VehicleInventory, AccountCodes.TradeInventory,
            AccountCodes.VehicleSalesRevenue, AccountCodes.FeeRevenue,
            AccountCodes.SalesDiscounts, AccountCodes.CostOfVehicleSales,
            AccountCodes.LabourRevenue, AccountCodes.PartsRevenue, AccountCodes.SubletRevenue,
            AccountCodes.PartsInventory, AccountCodes.CostOfPartsSales,
            AccountCodes.FinanceProductRevenue, AccountCodes.CostOfFinanceProducts,
            AccountCodes.WarrantyReceivable, AccountCodes.InternalServiceCharge,
        ];

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

    public static Error MixedCurrencies(IEnumerable<string> currencies) => Error.Validation(
        "accounting.mixed_currencies",
        $"These entries are in more than one currency ({string.Join(", ", currencies)}). "
        + "Totalling them would produce a number that means nothing — narrow the period "
        + "or the rooftop.");
}
