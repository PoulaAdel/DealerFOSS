// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Appointment — a promise that a car will arrive, and the job it becomes.
//
// Usage:
//   Appointment.Book(...), then Arrive(repairOrderId) when the car turns up,
//   or MissedIt / Cancel when it does not.
//
// Coding Instructions:
//   An appointment is not a repair order with an earlier date on it. It is a
//   promise, and the difference matters in three places.
//
//   ONE. A promise becomes a job EXACTLY ONCE. Arriving twice is refused by
//   name, because the second call would open a second job against the same
//   car and the workshop would bill for one visit twice. This is the
//   invariant that makes "Appointment -> RO reconciles to its source"
//   (roadmap I5) true rather than asserted.
//
//   TWO. A car that never came is RECORDED, not deleted. A no-show is the
//   single most useful thing in a service diary — it is how a manager knows
//   which customers to ring the day before — and deleting the row destroys
//   exactly that. Cancelled and NoShow are different facts and are kept
//   apart: one is the customer telling you, the other is silence.
//
//   THREE. The estimate is hours of WORKSHOP TIME, not a duration on a
//   calendar. Two cars booked at nine o'clock is normal; twelve hours of work
//   booked into an eight-hour day is not. The entity records the estimate and
//   refuses nothing — see the note on capacity in AppointmentService.

using DealerFOSS.Core;

namespace DealerFOSS.RepairOrders;

public sealed class Appointment : AuditableEntity
{
    public Guid Id { get; private set; }

    /// <summary>The workshop expecting the car. A permission boundary.</summary>
    public RooftopId RooftopId { get; private set; }

    public Guid CustomerId { get; private set; }

    /// <summary>The customer's own car, exactly as for a repair order.</summary>
    public Guid VehicleId { get; private set; }

    /// <summary>When the car is expected.</summary>
    public DateTimeOffset ScheduledFor { get; private set; }

    /// <summary>
    /// Workshop time the job is expected to take. Null means nobody estimated,
    /// which is honest and different from estimating zero.
    /// </summary>
    public decimal? EstimatedHours { get; private set; }

    /// <summary>What the customer says is wrong. Becomes the job's complaint.</summary>
    public string Reason { get; private set; } = string.Empty;

    public AppointmentStatus Status { get; private set; }

    /// <summary>Who took the booking.</summary>
    public Guid? AdvisorUserId { get; private set; }

    /// <summary>
    /// The job this appointment turned into. Null until the car arrives, and the
    /// only link between the diary and the workshop — which is what lets the two
    /// be reconciled instead of drifting.
    /// </summary>
    public Guid? RepairOrderId { get; private set; }

    public DateTimeOffset? ArrivedAt { get; private set; }

    /// <summary>Why it was cancelled, or what happened. Recorded, never inferred.</summary>
    public string? Outcome { get; private set; }

    public bool IsOpen => AppointmentStatusRules.IsOpen(Status);

    private Appointment()
    {
    }

    /// <summary>
    /// Takes a booking. It begins Scheduled with no job behind it — the job is
    /// created when the car is actually there, because a workshop that opens jobs
    /// for cars that never arrive cannot tell you what it did today.
    /// </summary>
    public static Appointment Book(
        Guid id,
        RooftopId rooftopId,
        Guid customerId,
        Guid vehicleId,
        DateTimeOffset scheduledFor,
        string reason,
        DateTimeOffset now,
        decimal? estimatedHours = null,
        Guid? advisorUserId = null)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("A booking needs a customer.", nameof(customerId));
        }

        if (vehicleId == Guid.Empty)
        {
            throw new ArgumentException("A booking needs a car.", nameof(vehicleId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "Record what the customer is bringing it in for. A booking with no reason cannot be planned for.",
                nameof(reason));
        }

        // A booking in the past is a typo every time. Recording work that already
        // happened is what a repair order is for, and it has its own route in.
        if (scheduledFor < now)
        {
            throw new ArgumentException(
                "That time has already passed. Book the car in for a time still to come, or open a job for a car that is already here.",
                nameof(scheduledFor));
        }

        if (estimatedHours is < 0m)
        {
            throw new ArgumentException("An estimate cannot be negative.", nameof(estimatedHours));
        }

        return new Appointment
        {
            Id = id,
            RooftopId = rooftopId,
            CustomerId = customerId,
            VehicleId = vehicleId,
            ScheduledFor = scheduledFor,
            Reason = reason.Trim(),
            Status = AppointmentStatus.Scheduled,
            EstimatedHours = estimatedHours,
            AdvisorUserId = advisorUserId,
        };
    }

    /// <summary>Moves the booking, while it is still a booking.</summary>
    public void Reschedule(DateTimeOffset scheduledFor, DateTimeOffset now, decimal? estimatedHours)
    {
        EnsureOpen("moved");

        if (scheduledFor < now)
        {
            throw new InvalidOperationException(
                "That time has already passed. Pick a time still to come.");
        }

        if (estimatedHours is < 0m)
        {
            throw new InvalidOperationException("An estimate cannot be negative.");
        }

        ScheduledFor = scheduledFor;
        EstimatedHours = estimatedHours;
    }

    /// <summary>
    /// The car is here, and this is the job it became.
    /// </summary>
    /// <remarks>
    /// The refusal names the existing job rather than saying "already arrived",
    /// because the person reading it is standing at a counter with a customer and
    /// needs to know which job to open, not that they made a mistake.
    /// </remarks>
    public void Arrive(Guid repairOrderId, DateTimeOffset arrivedAt)
    {
        if (RepairOrderId is { } already)
        {
            throw new InvalidOperationException(
                $"This booking is already job {already}. A car that arrives once produces one job.");
        }

        EnsureOpen("marked as arrived");

        if (repairOrderId == Guid.Empty)
        {
            throw new ArgumentException("Arriving records the job the car became.", nameof(repairOrderId));
        }

        RepairOrderId = repairOrderId;
        ArrivedAt = arrivedAt;
        Status = AppointmentStatus.Arrived;
    }

    /// <summary>The car never came and nobody rang. A fact worth keeping.</summary>
    public void MissedIt(string? note)
    {
        EnsureOpen("recorded as a no-show");

        Status = AppointmentStatus.NoShow;
        Outcome = Trimmed(note);
    }

    /// <summary>The customer told us. Different from silence, and recorded as such.</summary>
    public void Cancel(string? reason)
    {
        EnsureOpen("cancelled");

        Status = AppointmentStatus.Cancelled;
        Outcome = Trimmed(reason);
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void EnsureOpen(string attempted)
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException(
                $"A {Status} booking cannot be {attempted}. Take a new booking instead.");
        }
    }
}
