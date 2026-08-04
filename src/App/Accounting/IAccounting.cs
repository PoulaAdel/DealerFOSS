// IAccounting — what other capabilities may call to reach the ledger.
//
// Use:  Deals posts a delivered sale through this. Nothing writes journal rows
//       any other way.
// Edit: there is deliberately no "create an entry from these lines" method on
//       the public contract. An entry is the consequence of something that
//       happened in the business, so the contract names the events — a delivery,
//       a reversal — rather than offering a general-purpose posting hole.

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
    /// Records the accounting consequence of a car leaving the lot. Called by
    /// Deals when a deal is delivered; it is not something a person does.
    /// </summary>
    Task<Result<JournalEntryDetail>> PostDeliveryAsync(
        DeliveryPosting delivery,
        CancellationToken cancellationToken);

    /// <summary>
    /// Undoes a posted entry by posting its opposite. The original is untouched —
    /// that is the whole point.
    /// </summary>
    Task<Result<JournalEntryDetail>> ReverseAsync(
        Guid entryId,
        string reason,
        CancellationToken cancellationToken);
}

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

public sealed record JournalQuery(
    RooftopId? RooftopId = null,
    string? Reference = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Limit = 50);
