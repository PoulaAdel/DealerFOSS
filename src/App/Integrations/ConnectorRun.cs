// ConnectorRun — what happened, per connector, per dealership, per run.
//
// Use:  Started() before the work, Succeeded()/Failed()/Misconfigured() after.
//       Written in its own save so a crash leaves a row behind.
// Edit: this is a record, not a log line, because the question an operator asks
//       is "has this dealership been failing all week?" — and metrics are
//       aggregates that expire. A store producing zero rows every night looks
//       exactly like a quiet store until somebody can compare last night with
//       the one before.
//
//       The row is inserted BEFORE the fetch and completed after, so a process
//       killed mid-run leaves FinishedAt null. That unfinished row is the only
//       evidence such a run ever happened, and it is worth more than the tidiness
//       of writing one row at the end.
//
//       CursorHeld is deliberately separate from Outcome. A run can succeed —
//       records arrived, records applied — and still not move the cursor, and
//       collapsing the two would hide the more important half.

using DealerFOSS.Core;

namespace DealerFOSS.Integrations;

/// <summary>How a dealership's run ended.</summary>
public enum RunOutcome
{
    /// <summary>Still going, or the process died before it could say.</summary>
    Running = 0,

    Succeeded = 1,

    /// <summary>Records arrived and some were quarantined.</summary>
    SucceededWithQuarantine = 2,

    /// <summary>
    /// The provider accepted the job and had not finished by the poll deadline.
    /// Not a failure — the next run picks it up.
    /// </summary>
    StillRunningAtProvider = 3,

    /// <summary>This dealership failed. The others in the run carried on.</summary>
    Failed = 4,

    /// <summary>Configuration is invalid, so nothing was attempted for it.</summary>
    Misconfigured = 5,
}

/// <summary>One dealership's share of one run.</summary>
public sealed class ConnectorRun : AuditableEntity
{
    public Guid Id { get; private set; }

    /// <summary>Provider name from the manifest.</summary>
    public string Connector { get; private set; } = string.Empty;

    public RooftopId RooftopId { get; private set; }

    /// <summary>The capability that was read.</summary>
    public string Contract { get; private set; } = string.Empty;

    public int Version { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>When it ended. Null means it never did — see the header.</summary>
    public DateTimeOffset? FinishedAt { get; private set; }

    public RunOutcome Outcome { get; private set; } = RunOutcome.Running;

    public int RecordsApplied { get; private set; }

    /// <summary>
    /// Records already present and left alone. Separate from
    /// <see cref="RecordsApplied"/> because a held cursor re-reads the same
    /// window every night: without this column that reads as five hundred
    /// records a night arriving, which is exactly the healthy-looking history a
    /// stuck feed should not be able to produce.
    /// </summary>
    public int RecordsUnchanged { get; private set; }

    public int RecordsQuarantined { get; private set; }

    /// <summary>Values that did not survive intact but did not stop the record.</summary>
    public int WarningCount { get; private set; }

    /// <summary>
    /// The period the provider reported serving, or null when it did not say.
    /// Stored split because a range is two columns to a database.
    /// </summary>
    public DateTimeOffset? CoveredFrom { get; private set; }

    public DateTimeOffset? CoveredTo { get; private set; }

    /// <summary>True when the run finished without the cursor being able to move.</summary>
    public bool CursorHeld { get; private set; }

    /// <summary>The stable code explaining the hold; null when the cursor moved.</summary>
    public string? CursorHeldReason { get; private set; }

    /// <summary>The stable error code when it failed.</summary>
    public string? FailureCode { get; private set; }

    private ConnectorRun()
    {
    }

    private ConnectorRun(
        Guid id,
        string connector,
        RooftopId rooftopId,
        string contract,
        int version,
        DateTimeOffset startedAt)
    {
        Id = id;
        Connector = connector;
        RooftopId = rooftopId;
        Contract = contract;
        Version = version;
        StartedAt = startedAt;
    }

    /// <summary>Open a run. Save this before doing any work.</summary>
    public static ConnectorRun Started(
        Guid id,
        string connector,
        RooftopId rooftopId,
        ConnectorCapability capability,
        DateTimeOffset startedAt)
    {
        ArgumentNullException.ThrowIfNull(capability);

        if (string.IsNullOrWhiteSpace(connector))
        {
            throw new ArgumentException("A connector name is required.", nameof(connector));
        }

        return new ConnectorRun(id, connector.Trim(), rooftopId, capability.Contract, capability.Version, startedAt);
    }

    /// <summary>Records arrived and were applied.</summary>
    public void Completed(
        DateTimeOffset finishedAt,
        int applied,
        int unchanged,
        int quarantined,
        int warnings,
        DateRange? covered)
    {
        Finish(finishedAt);
        RecordsApplied = applied;
        RecordsUnchanged = unchanged;
        RecordsQuarantined = quarantined;
        WarningCount = warnings;
        CoveredFrom = covered?.Start;
        CoveredTo = covered?.End;
        Outcome = quarantined > 0 ? RunOutcome.SucceededWithQuarantine : RunOutcome.Succeeded;
    }

    /// <summary>
    /// The run did its work but the window could not be accounted for, so the
    /// cursor stayed where it was. Called alongside <see cref="Completed"/>,
    /// never instead of it.
    /// </summary>
    public void HeldCursor(string reasonCode) =>
        (CursorHeld, CursorHeldReason) = (true, reasonCode);

    /// <summary>The provider was still working when the poll deadline passed.</summary>
    public void StillRunningAtProvider(DateTimeOffset finishedAt)
    {
        Finish(finishedAt);
        Outcome = RunOutcome.StillRunningAtProvider;
    }

    /// <summary>This dealership failed; the rest of the run carried on.</summary>
    public void Failed(DateTimeOffset finishedAt, string failureCode)
    {
        Finish(finishedAt);
        Outcome = RunOutcome.Failed;
        FailureCode = failureCode;
    }

    /// <summary>Nothing was attempted, because the configuration is not usable.</summary>
    public void Misconfigured(DateTimeOffset finishedAt, string failureCode)
    {
        Finish(finishedAt);
        Outcome = RunOutcome.Misconfigured;
        FailureCode = failureCode;
    }

    private void Finish(DateTimeOffset finishedAt)
    {
        if (FinishedAt is not null)
        {
            throw new InvalidOperationException("This run has already been completed.");
        }

        FinishedAt = finishedAt;
    }
}
