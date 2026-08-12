// ConnectorRun — what happened, per connector, per dealership, per run.
//
// Use:  one of these per dealership per run, whether it succeeded or not.
// Edit: this is a record, not a log line, because the question an operator asks
//       is "has this dealership been failing all week?" — and metrics are
//       aggregates that expire. A store producing zero rows every night looks
//       exactly like a quiet store until somebody can compare last night with
//       the one before.
//
//       Not yet persisted: there is no table, no migration and no endpoint
//       behind this. It is the shape the runtime will record, and saying so
//       here is better than a README claiming a facility that does not exist.

namespace DealerFOSS.Integrations;

/// <summary>How a dealership's run ended.</summary>
public enum RunOutcome
{
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
/// <param name="Connector">Provider name from the manifest.</param>
/// <param name="RooftopId">The dealership this covers.</param>
/// <param name="Contract">The capability that was read.</param>
/// <param name="StartedAt">When it began.</param>
/// <param name="FinishedAt">When it ended; null while running.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="RecordsApplied">Records committed.</param>
/// <param name="RecordsQuarantined">Records held back for an operator.</param>
/// <param name="WarningCount">Values that did not survive intact.</param>
/// <param name="Covered">
/// The period the provider reported serving, or null when it did not say — in
/// which case the cursor did not move, and this row is the evidence of why.
/// </param>
/// <param name="FailureCode">The stable error code when it failed.</param>
public sealed record ConnectorRun(
    string Connector,
    Guid RooftopId,
    string Contract,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    RunOutcome Outcome,
    int RecordsApplied,
    int RecordsQuarantined,
    int WarningCount,
    DateRange? Covered,
    string? FailureCode);
