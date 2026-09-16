// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   TechnicianClocking — a technician on a job, from when they started to when
//   they stopped.
//
//   WHAT IT MAKES POSSIBLE, AND WHY IT COULD NOT EXIST BEFORE 2026-09-16. The
//   labour report has always known HOURS SOLD — what the customer was billed.
//   It had no idea how long the work actually took, so "are we quick" had no
//   answer. That comparison is called EFFICIENCY and it is the number a
//   workshop is genuinely run on: flagged hours divided by clocked hours, where
//   flagged is what the job was worth and clocked is what it cost in time.
//
//   Until op codes landed the same morning there was nothing to flag against
//   either — every labour line carried whatever hours somebody typed. Clocking
//   without a standard would have measured a technician against a number the
//   advisor had invented, which is worse than not measuring them at all.
//
//   THIS IS NOT ATTENDANCE AND MUST NOT BECOME IT. Efficiency is flagged over
//   clocked. PRODUCTIVITY is clocked over ATTENDED — how much of a paid day was
//   spent on paying work — and attendance is payroll: hours, absence, a pay
//   rate, a tax position. None of that exists here and none of it should arrive
//   through this file.
//
// Usage:
//   Through IRepairOrders.ClockOnAsync and ClockOffAsync.
//
// Coding Instructions:
//   ONE OPEN CLOCKING PER TECHNICIAN, AND CLOCKING ON ELSEWHERE CLOSES IT.
//   A technician cannot be on two jobs at once, and a shop where the system
//   refuses the second clock-on is a shop where people stop clocking at all —
//   they move between jobs constantly and a refusal is friction they will route
//   around. Switching is what a real workshop does, so switching is what this
//   does, and it is recorded rather than silent.
//
//   SEVERAL TECHNICIANS ON ONE JOB IS ORDINARY, not a conflict. A gearbox out
//   is two people. Nothing here limits a job to one open clocking.
//
//   A CLOSED ENTRY IS NEVER REOPENED OR EDITED. A correction is a new entry, the
//   same way a payment is corrected by a reversal — somebody's recorded time is
//   evidence in a pay dispute, and evidence that can be rewritten is not
//   evidence. Stop() enforces it by refusing a second stop.
//
//   THIS IS NOT IAppendOnly, AND THAT IS NOT AN OVERSIGHT. That marker forbids
//   the context from ever UPDATING a row, and closing a clocking is an update —
//   the one this type exists to perform. It was tried, and every clock-on failed
//   with "TechnicianClocking is append-only". The property actually wanted is
//   narrower than the marker expresses, so it lives in Stop() where it can be
//   stated exactly.

using DealerFOSS.Core;

namespace DealerFOSS.RepairOrders;

/// <summary>One stretch of time one technician spent on one job.</summary>
public sealed class TechnicianClocking : AuditableEntity
{
    public Guid Id { get; private set; }

    public Guid RepairOrderId { get; private set; }

    public RooftopId RooftopId { get; private set; }

    public Guid TechnicianUserId { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>Null while the technician is still on the job.</summary>
    public DateTimeOffset? StoppedAt { get; private set; }

    /// <summary>
    /// Why it stopped, when it was not a person pressing stop — "switched to
    /// RO-1084". Null on an ordinary clock-off.
    /// </summary>
    public string? StoppedBecause { get; private set; }

    public bool IsOpen => StoppedAt is null;

    /// <summary>
    /// How long this stretch lasted. Zero while it is still open, rather than
    /// counting up to now: a figure that changes every time somebody looks at it
    /// cannot be reconciled against anything.
    /// </summary>
    public decimal Hours => StoppedAt is { } stopped
        ? Math.Round((decimal)(stopped - StartedAt).TotalHours, 2, MidpointRounding.AwayFromZero)
        : 0m;

    private TechnicianClocking()
    {
    }

    public static TechnicianClocking Start(
        Guid id,
        Guid repairOrderId,
        RooftopId rooftopId,
        Guid technicianUserId,
        DateTimeOffset startedAt)
    {
        if (technicianUserId == Guid.Empty)
        {
            throw new ArgumentException("A clocking needs a technician.", nameof(technicianUserId));
        }

        return new TechnicianClocking
        {
            Id = id,
            RepairOrderId = repairOrderId,
            RooftopId = rooftopId,
            TechnicianUserId = technicianUserId,
            StartedAt = startedAt,
        };
    }

    public void Stop(DateTimeOffset stoppedAt, string? because = null)
    {
        if (StoppedAt is not null)
        {
            throw new InvalidOperationException("That clocking has already been stopped.");
        }

        // Refused rather than clamped. A stop before the start means the clock
        // somebody is trusting has moved, and silently recording zero hours
        // would hide it — which is the failure a technician notices in their pay
        // and nowhere else.
        if (stoppedAt < StartedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stoppedAt), "A clocking cannot stop before it started.");
        }

        StoppedAt = stoppedAt;
        StoppedBecause = string.IsNullOrWhiteSpace(because) ? null : because.Trim();
    }
}
