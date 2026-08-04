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

        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<TrialBalance>(LedgerErrors.Forbidden);
        }

        if (query.RooftopId is { } requested && !scope.Covers(requested))
        {
            return Result.Failure<TrialBalance>(LedgerErrors.Forbidden);
        }

        var entries = _db.JournalEntries.AsNoTracking();

        // The same rooftop filter as every other read: a balance must never
        // total up a location the caller cannot see.
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
            return Result.Failure<TrialBalance>(LedgerErrors.MixedCurrencies(currencies));
        }

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
            currencies.Count == 1 ? currencies[0] : string.Empty,
            totalDebits,
            totalCredits,
            totalDebits == totalCredits,
            balances));
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

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, PostPermission, original.RooftopId, cancellationToken))
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
            new AuditEntry(_currentUser.Id, PostPermission, AuditOutcome.Allowed,
                "JournalEntry", reversal.Id.ToString(), original.RooftopId.Value,
                $"Reversed {entryId}", null, null),
            cancellationToken);

        return Result.Success(await DescribeAsync(reversal, cancellationToken));
    }

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

        // Relieving inventory at cost, so gross profit is visible.
        Line(AccountCodes.CostOfVehicleSales, d.VehicleCost, 0m, "Cost of the vehicle sold");
        Line(AccountCodes.VehicleInventory, 0m, d.VehicleCost, "Vehicle off the lot");

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

    public static Error CannotReverseAReversal { get; } = Error.Conflict(
        "accounting.cannot_reverse_a_reversal",
        "Reverse the original entry instead, or post a fresh one.");

    public static Error ChartIncomplete(IEnumerable<string> missing) => Error.Validation(
        "accounting.chart_incomplete",
        $"The chart of accounts is missing: {string.Join(", ", missing)}.");

    public static Error MixedCurrencies(IEnumerable<string> currencies) => Error.Validation(
        "accounting.mixed_currencies",
        $"These entries are in more than one currency ({string.Join(", ", currencies)}). "
        + "Totalling them would produce a number that means nothing — narrow the period "
        + "or the rooftop.");
}
