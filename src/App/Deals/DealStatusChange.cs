// DealStatusChange — one line of a deal's history: it moved from here to there,
// then, by whom, and why.
//
// Use:  written by Deal.ChangeStatus; never constructed directly.
// Edit: append-only, and more strictly so than the other histories. This is the
//       record of who approved what and when — if it can be rewritten, an
//       approval means nothing (ADR-016). TenantDb refuses to update or delete
//       anything marked IAppendOnly.

using OpenDealer360.Core;

namespace OpenDealer360.Deals;

public sealed class DealStatusChange : IAppendOnly
{
    public Guid Id { get; private set; }

    public Guid DealId { get; private set; }

    /// <summary>Null on the first entry — the deal did not come from anywhere.</summary>
    public DealStatus? FromStatus { get; private set; }

    public DealStatus ToStatus { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public Guid? ChangedByUserId { get; private set; }

    public string? Note { get; private set; }

    /// <summary>
    /// The deal total at the moment of the move, so an approval records the
    /// number that was approved rather than whatever the total says today.
    /// </summary>
    public decimal AmountAtChange { get; private set; }

    private DealStatusChange()
    {
    }

    internal DealStatusChange(
        Guid id,
        Guid dealId,
        DealStatus? fromStatus,
        DealStatus toStatus,
        DateTimeOffset occurredAt,
        Guid? changedByUserId,
        string? note,
        decimal amountAtChange)
    {
        Id = id;
        DealId = dealId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        OccurredAt = occurredAt;
        ChangedByUserId = changedByUserId;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        AmountAtChange = amountAtChange;
    }
}
