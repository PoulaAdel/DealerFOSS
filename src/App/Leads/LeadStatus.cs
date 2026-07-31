// LeadStatus — where an enquiry has got to, and which moves are legal between
// those states.
//
// Use:  LeadStatusRules.CanMove(from, to) before changing a status;
//       Lead.ChangeStatus already applies it.
// Edit: adding a status means deciding what it can move to AND what can move to
//       it. Leaving either out silently strands leads in a state nobody can get
//       them out of.

namespace OpenDealer360.Leads;

public enum LeadStatus
{
    /// <summary>Just arrived. Nobody has spoken to them yet.</summary>
    New = 0,

    /// <summary>Someone is actively following up.</summary>
    Working = 1,

    /// <summary>They have agreed to come in at a specific time.</summary>
    Appointment = 2,

    /// <summary>They bought.</summary>
    Won = 3,

    /// <summary>They did not buy — bought elsewhere, changed their mind, or went cold.</summary>
    Lost = 4,
}

/// <summary>
/// The legal moves between lead statuses. A lead moves in a small number of
/// understood ways; anything else is a mistake worth refusing where it is made.
/// </summary>
public static class LeadStatusRules
{
    private static readonly Dictionary<LeadStatus, LeadStatus[]> Allowed = new()
    {
        [LeadStatus.New] = [LeadStatus.Working, LeadStatus.Lost],
        [LeadStatus.Working] = [LeadStatus.Appointment, LeadStatus.Won, LeadStatus.Lost],
        [LeadStatus.Appointment] = [LeadStatus.Working, LeadStatus.Won, LeadStatus.Lost],

        // Won is the end of this lead. A second purchase is a second enquiry.
        [LeadStatus.Won] = [],

        // A lost lead legitimately comes back — they were not ready in March and
        // walked in again in June. That is a reopened lead with a reason, not a
        // new record that loses the history of the first attempt.
        [LeadStatus.Lost] = [LeadStatus.Working],
    };

    public static bool CanMove(LeadStatus from, LeadStatus to) =>
        from != to && Allowed[from].Contains(to);

    /// <summary>The moves available from a status, for a screen to offer.</summary>
    public static IReadOnlyList<LeadStatus> MovesFrom(LeadStatus from) => Allowed[from];

    /// <summary>Whether this status means the lead is no longer being worked.</summary>
    public static bool IsClosed(LeadStatus status) =>
        status is LeadStatus.Won or LeadStatus.Lost;
}

/// <summary>How the enquiry reached the dealership. Drives source reporting later.</summary>
public enum LeadSource
{
    Unknown = 0,
    WalkIn = 1,
    Phone = 2,
    Website = 3,
    Referral = 4,
    Marketplace = 5,
}
