// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ConnectorSchedule — a dealership's standing instruction to read one feed,
//   and the place a run can start from when nobody is at a keyboard.
//
//   Until this existed nothing in the database said which connector a rooftop
//   used, with what settings. ConnectorRuntime.RunAsync took them all as
//   arguments, so the only way to start a run was for a caller to already hold
//   a connector instance and a settings dictionary — which meant a test, and
//   nothing else. A trigger needs somewhere to fire FROM before it needs a
//   clock.
//
// Usage:
//   Armed through IIntegrations. ScheduleWorker claims the earliest due row and
//   runs it. Nothing else writes to this table.
//
// Coding Instructions:
//   THE AUTHORITY IS A COLUMN, NOT AN ABSENCE. ArmedByUserId is the person
//   whose standing instruction this is, and a run carries their permissions —
//   see ADR-028. There is no value of it meaning "the system", for the same
//   reason JobContext.RequestedByUserId is a Guid and not a Guid?: a feed that
//   could write records nobody is accountable for would be the one way into
//   this application that leaves no name on the audit trail.
//
//   NEXTRUNAT ADVANCES FROM NOW, NOT FROM THE SLOT IT MISSED. An installation
//   down for a day would otherwise come back up owing a hundred catch-up runs
//   and fire them back to back at a provider that is rate-limiting it. Skipping
//   the missed slots costs nothing here because the CURSOR holds the position,
//   not the schedule: one run reads the whole gap, which is the behaviour the
//   window arithmetic was built for.
//
//   THE CLAIM IS A CONDITIONAL UPDATE ON THIS COLUMN, so two application
//   instances sharing one database cannot both fire the same schedule — the
//   loser's update matches no row and it finds nothing to do. Same mechanism as
//   ImportWorker's Queued-to-Running move. It is NOT a general poll lease: it
//   serialises one row and says nothing about a provider's per-tenant limits.
//
//   SUSPENDED IS NOT DISARMED, and keeping them apart is the point. Disarmed is
//   somebody's decision. Suspended is ours, always with a reason a person can
//   read, and it is what a schedule does when it cannot honestly run — its
//   owner lost the permission, or its settings no longer validate. The
//   alternative was a row that failed silently every quarter-hour for months,
//   which is the failure this whole area keeps finding.

using DealerFOSS.Core;

namespace DealerFOSS.Integrations;

/// <summary>Whether a schedule will fire, and if not, whose decision that was.</summary>
public enum ScheduleState
{
    /// <summary>Due to fire at <see cref="ConnectorSchedule.NextRunAt"/>.</summary>
    Armed = 1,

    /// <summary>Somebody turned it off. It stays configured and can be re-armed.</summary>
    Disarmed = 2,

    /// <summary>
    /// We stopped it, and <see cref="ConnectorSchedule.SuspendedReason"/> says
    /// why. Distinct from <see cref="Disarmed"/> because a person needs to know
    /// the difference between a feed they turned off and one that stopped being
    /// able to run.
    /// </summary>
    Suspended = 3,
}

/// <summary>One dealership's standing instruction to read one feed.</summary>
public sealed class ConnectorSchedule : AuditableEntity
{
    /// <summary>
    /// The floor on how often a feed may be read. Below this a poll is a load
    /// generator pointed at somebody else's API, and it buys nothing: a
    /// provider's settlement delay is routinely longer than five minutes, so
    /// the extra requests ask for a window that cannot have changed.
    /// </summary>
    public const int MinimumIntervalMinutes = 5;

    /// <summary>A week. Past this a "schedule" is a reminder to do it by hand.</summary>
    public const int MaximumIntervalMinutes = 7 * 24 * 60;

    private ConnectorSchedule()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>The provider, matching <see cref="ConnectorManifest.Provider"/>.</summary>
    public string Connector { get; private set; } = string.Empty;

    public RooftopId RooftopId { get; private set; }

    public string Contract { get; private set; } = string.Empty;

    public int Version { get; private set; }

    /// <summary>
    /// This dealership's settings as stored: JSON, with every value whose
    /// declared kind is <see cref="SettingKind.Secret"/> already protected. See
    /// <see cref="ConnectorSettings"/> — nothing else may read or write this.
    /// </summary>
    public string Settings { get; private set; } = "{}";

    /// <summary>
    /// The person whose standing instruction this is. A run carries their
    /// permissions and is audited under their name (ADR-028). Never
    /// <see cref="Guid.Empty"/> — rejected at construction.
    /// </summary>
    public Guid ArmedByUserId { get; private set; }

    public int IntervalMinutes { get; private set; }

    public ScheduleState State { get; private set; } = ScheduleState.Armed;

    /// <summary>Set whenever <see cref="State"/> is <see cref="ScheduleState.Suspended"/>.</summary>
    public string? SuspendedReason { get; private set; }

    /// <summary>
    /// When this is next due. Null unless armed, so the dispatcher's query
    /// cannot pick up a disarmed or suspended row even if it forgets to filter
    /// on the state.
    /// </summary>
    public DateTimeOffset? NextRunAt { get; private set; }

    public DateTimeOffset? LastRunAt { get; private set; }

    /// <summary>The <see cref="ConnectorRun"/> this schedule last started.</summary>
    public Guid? LastRunId { get; private set; }

    /// <summary>
    /// What that run came to, as the enum name. Kept here as well as on the run
    /// so a list of feeds can say how each one is doing without a join per row.
    /// </summary>
    public string? LastOutcome { get; private set; }

    /// <summary>The only way to make one.</summary>
    public static Result<ConnectorSchedule> Arm(
        Guid id,
        string connector,
        RooftopId rooftopId,
        ConnectorCapability capability,
        string storedSettings,
        Guid armedByUserId,
        int intervalMinutes,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(capability);

        if (armedByUserId == Guid.Empty)
        {
            // The same refusal JobContext makes, for the same reason: a default
            // Guid satisfies every type in the system and authorizes against
            // nobody's roles.
            throw new ArgumentException(
                "A schedule must name the person whose instruction it is.", nameof(armedByUserId));
        }

        if (intervalMinutes < MinimumIntervalMinutes || intervalMinutes > MaximumIntervalMinutes)
        {
            return Result.Failure<ConnectorSchedule>(
                IntegrationErrors.IntervalOutOfRange(MinimumIntervalMinutes, MaximumIntervalMinutes));
        }

        return Result.Success(new ConnectorSchedule
        {
            Id = id,
            Connector = connector,
            RooftopId = rooftopId,
            Contract = capability.Contract,
            Version = capability.Version,
            Settings = storedSettings,
            ArmedByUserId = armedByUserId,
            IntervalMinutes = intervalMinutes,
            State = ScheduleState.Armed,

            // Due immediately. Somebody who has just configured a feed wants to
            // find out tonight whether it works, not after the first interval.
            NextRunAt = now,
        });
    }

    /// <summary>Somebody turned it off. Configuration and history are kept.</summary>
    public void Disarm()
    {
        State = ScheduleState.Disarmed;
        SuspendedReason = null;
        NextRunAt = null;
    }

    /// <summary>
    /// Turned back on, by somebody who may be a different person from whoever
    /// armed it first — so the authority is re-stated rather than inherited.
    /// </summary>
    public void Rearm(Guid armedByUserId, int intervalMinutes, DateTimeOffset now)
    {
        if (armedByUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "A schedule must name the person whose instruction it is.", nameof(armedByUserId));
        }

        ArmedByUserId = armedByUserId;
        IntervalMinutes = intervalMinutes;
        State = ScheduleState.Armed;
        SuspendedReason = null;
        NextRunAt = now;
    }

    /// <summary>
    /// It cannot honestly run any more. Always with a reason, because a feed
    /// that has stopped is a question somebody will ask.
    /// </summary>
    public void Suspend(string reason)
    {
        State = ScheduleState.Suspended;
        SuspendedReason = reason.Length > 300 ? reason[..300] : reason;
        NextRunAt = null;
    }

    /// <summary>
    /// Move the due time forward. Called only through the conditional update in
    /// ScheduleWorker, which is what stops two instances firing the same row —
    /// this method exists for the in-memory copy to agree with the database.
    /// </summary>
    public void Claimed(DateTimeOffset now) =>
        NextRunAt = now.AddMinutes(IntervalMinutes);

    /// <summary>What the run it started came to.</summary>
    public void Recorded(Guid runId, RunOutcome outcome, DateTimeOffset finishedAt)
    {
        LastRunId = runId;
        LastOutcome = outcome.ToString();
        LastRunAt = finishedAt;
    }

    /// <summary>Overwrite the stored settings, already protected.</summary>
    public void Reconfigure(string storedSettings) => Settings = storedSettings;
}
