// AppointmentStatus — where a booking got to.
//
// Use:  AppointmentStatusRules.IsOpen(status) before changing anything;
//       Appointment already applies it.
// Edit: there is no transition table here, unlike RepairOrderStatusRules, and
//       that is not an omission. A booking has exactly one open state and three
//       terminal ones, so "can this move" is the single question "is it still
//       open" — a table would be three rows of the same answer.
//
//       The three endings are deliberately three and not one. "Cancelled" is the
//       customer telling you; "NoShow" is silence; "Arrived" is the car being
//       here. Collapsing them into "closed" would throw away the only figures a
//       service manager actually wants from a diary.

namespace DealerFOSS.RepairOrders;

public enum AppointmentStatus
{
    /// <summary>Booked in. The car is expected and has not arrived.</summary>
    Scheduled = 0,

    /// <summary>The car is here and a job was opened. Carries the job's id.</summary>
    Arrived = 1,

    /// <summary>The car never came and nobody rang.</summary>
    NoShow = 2,

    /// <summary>The customer told us it was off.</summary>
    Cancelled = 3,
}

public static class AppointmentStatusRules
{
    /// <summary>Whether the booking can still be changed, moved, or arrived.</summary>
    public static bool IsOpen(AppointmentStatus status) => status == AppointmentStatus.Scheduled;

    /// <summary>
    /// The statuses that count against a day's workload. An arrived car is in the
    /// workshop and its hours belong to the repair order, not to the diary — so
    /// counting it here as well would double the day.
    /// </summary>
    public static bool CountsTowardLoad(AppointmentStatus status) =>
        status == AppointmentStatus.Scheduled;
}
