// IReporting — the one question a dealer principal asks: how did we do?
//
// Use:  the dashboard calls MonthAsync and renders what comes back.
// Edit: this capability owns no data and no table. It composes what Accounting
//       and Inventory already publish, and its whole value is that the answer
//       arrives in one round trip instead of four — which is what lets the screen
//       be a single view rather than a set of tabs.
//
//       It deliberately holds no account codes and no aging rules. Those belong
//       to the capabilities that own the figures; a second copy here would be a
//       second thing to keep true.

using DealerFOSS.Accounting;
using DealerFOSS.Core;
using DealerFOSS.Inventory;

namespace DealerFOSS.Reporting;

public interface IReporting
{
    /// <summary>
    /// One month, from every angle a manager looks at it: what each department
    /// made, how that compares with the month before, what is standing on the lot,
    /// and whether the books for it are still open.
    /// </summary>
    Task<Result<MonthInReview>> MonthAsync(MonthQuery query, CancellationToken cancellationToken);
}

/// <summary>Which month, and optionally which rooftop within it.</summary>
public sealed record MonthQuery(int Year, int Month, RooftopId? RooftopId = null);

/// <summary>
/// A month as a dashboard shows it.
/// </summary>
/// <remarks>
/// <para>
/// Every section is nullable, and <see cref="Withheld"/> names the ones that are
/// missing. A salesperson may read stock and not the ledger; showing them the half
/// they are entitled to, and saying plainly why the rest is absent, is more useful
/// than one refusal for the whole screen — and it is honest, which a silently
/// empty panel is not.
/// </para>
/// <para>
/// When a caller may see nothing at all the call fails outright, so "an empty
/// dashboard" never means "a dashboard you were not allowed to see".
/// </para>
/// </remarks>
public sealed record MonthInReview(
    int Year,
    int Month,
    DateOnly StartsOn,

    /// <summary>The cutoff — the 30th or 31st, whichever this month has.</summary>
    DateOnly EndsOn,

    /// <summary>One of <see cref="BooksState"/>.</summary>
    string Books,
    DateTimeOffset? ClosedAt,
    LedgerPerformance? Trading,

    /// <summary>
    /// The month before, on the same basis. A gross figure on its own tells nobody
    /// whether it was a good month; the comparison is most of the meaning.
    /// </summary>
    LedgerPerformance? PriorMonth,
    StockAging? Stock,
    IReadOnlyList<string> Withheld);

/// <summary>
/// What the books for the month are doing. Not a date: a month is closed when
/// somebody finishes the close and locks it, which is days after the cutoff.
/// </summary>
public static class BooksState
{
    /// <summary>Nobody has opened this month, so nothing can post into it.</summary>
    public const string NotOpened = "NotOpened";

    /// <summary>Posting, and the figures can still move.</summary>
    public const string Open = "Open";

    /// <summary>Locked. The figures are the ones that were reported.</summary>
    public const string Closed = "Closed";

    /// <summary>The caller may not read the ledger, so the state is not theirs to know.</summary>
    public const string Unknown = "Unknown";
}

/// <summary>
/// The names <see cref="MonthInReview.Withheld"/> uses. Constants rather than
/// prose, so a screen can decide what to say instead of matching on a sentence.
/// </summary>
public static class WithheldSection
{
    public const string Trading = "Trading";
    public const string Stock = "Stock";
}
