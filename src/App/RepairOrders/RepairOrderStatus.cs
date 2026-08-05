// RepairOrderStatus — where a job has got to, plus the two smaller enums that
// describe a line on it.
//
// Use:  RepairOrderStatusRules.CanMove(from, to) before changing a status;
//       RepairOrder.ChangeStatus already applies it.
// Edit: the Completed boundary mirrors the Draft boundary on a deal. While a job
//       is Booked or InProgress its lines can be changed freely; once it is
//       Completed the work is what will be billed, and changing it means sending
//       it back — a recorded move somebody has to make deliberately.
//
//       Cancelled is reachable right up to Completed, because cars are collected
//       unrepaired more often than anybody would like.

namespace DealerFOSS.RepairOrders;

public enum RepairOrderStatus
{
    /// <summary>The car is expected or just arrived. Nothing has been done to it.</summary>
    Booked = 0,

    /// <summary>Somebody is working on it. Lines can still be added and changed.</summary>
    InProgress = 1,

    /// <summary>The work is finished. The lines are frozen from here.</summary>
    Completed = 2,

    /// <summary>Billed, and posted to the ledger. The end of the line.</summary>
    Invoiced = 3,

    /// <summary>The customer took it away, or never brought it in.</summary>
    Cancelled = 4,
}

/// <summary>
/// The legal moves between repair-order statuses. A job that could jump straight
/// from Booked to Invoiced would be a bill for work nobody recorded.
/// </summary>
public static class RepairOrderStatusRules
{
    private static readonly Dictionary<RepairOrderStatus, RepairOrderStatus[]> Allowed = new()
    {
        [RepairOrderStatus.Booked] = [RepairOrderStatus.InProgress, RepairOrderStatus.Cancelled],

        [RepairOrderStatus.InProgress] = [RepairOrderStatus.Completed, RepairOrderStatus.Cancelled],

        // Sent back because the work is not right is an ordinary outcome. It is
        // also the only way to change the lines on a completed job, which is the
        // point — a bill that can be edited quietly is not a bill.
        [RepairOrderStatus.Completed] = [RepairOrderStatus.Invoiced, RepairOrderStatus.InProgress],

        [RepairOrderStatus.Invoiced] = [],
        [RepairOrderStatus.Cancelled] = [],
    };

    public static bool CanMove(RepairOrderStatus from, RepairOrderStatus to) =>
        from != to && Allowed[from].Contains(to);

    /// <summary>The moves available from a status, for a screen to offer.</summary>
    public static IReadOnlyList<RepairOrderStatus> MovesFrom(RepairOrderStatus from) => Allowed[from];

    /// <summary>Whether the work on the job may still be edited.</summary>
    public static bool LinesAreOpen(RepairOrderStatus status) =>
        status is RepairOrderStatus.Booked or RepairOrderStatus.InProgress;

    /// <summary>Whether the job is finished, one way or the other.</summary>
    public static bool IsClosed(RepairOrderStatus status) =>
        status is RepairOrderStatus.Invoiced or RepairOrderStatus.Cancelled;
}

/// <summary>What a line on a repair order is for.</summary>
public enum ServiceLineKind
{
    /// <summary>Somebody's time: hours at a rate.</summary>
    Labour = 0,

    /// <summary>A part fitted to the car.</summary>
    Part = 1,

    /// <summary>Work sent out to another business, billed on.</summary>
    Sublet = 2,
}

/// <summary>
/// Whether the customer has agreed to pay for a line.
///
/// This is the control the whole capability exists to hold. A technician who
/// strips a wheel off and finds the discs are gone has found real work — but
/// nobody may bill it until somebody has actually asked the customer. Recording
/// that answer is a deliberate act with its own permission, and an unanswered
/// line stops the invoice.
/// </summary>
public enum LineAuthorization
{
    /// <summary>Found during the work. Nobody has asked the customer yet.</summary>
    Pending = 0,

    /// <summary>The customer said yes — or it is what they booked the car in for.</summary>
    Authorized = 1,

    /// <summary>The customer said no. It stays on the record and off the bill.</summary>
    Declined = 2,
}
