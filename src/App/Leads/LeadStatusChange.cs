// LeadStatusChange — one line of a lead's history: it moved from here to there,
// then, and this is what was said.
//
// Use:  written by Lead.ChangeStatus; never constructed directly.
// Edit: history is append-only. A wrong entry is corrected by making the opposite
//       move with a reason, not by editing the record. TenantDb refuses to update
//       or delete these rows.

using DealerFOSS.Core;

namespace DealerFOSS.Leads;

public sealed class LeadStatusChange : IAppendOnly
{
    public Guid Id { get; private set; }

    public Guid LeadId { get; private set; }

    /// <summary>Null on the first entry — the lead did not come from anywhere.</summary>
    public LeadStatus? FromStatus { get; private set; }

    public LeadStatus ToStatus { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Who made the move, or null when the system did.</summary>
    public Guid? ChangedByUserId { get; private set; }

    /// <summary>
    /// What happened — "left a voicemail", "wants to see it Saturday", "bought at
    /// the dealer down the road". This is the useful half of a lead history.
    /// </summary>
    public string? Note { get; private set; }

    private LeadStatusChange()
    {
    }

    internal LeadStatusChange(
        Guid id,
        Guid leadId,
        LeadStatus? fromStatus,
        LeadStatus toStatus,
        DateTimeOffset occurredAt,
        Guid? changedByUserId,
        string? note)
    {
        Id = id;
        LeadId = leadId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        OccurredAt = occurredAt;
        ChangedByUserId = changedByUserId;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
