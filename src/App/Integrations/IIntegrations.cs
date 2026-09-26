// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   IIntegrations — what a person can ask about the edge, and do to it.
//
// Usage:
//   Through the endpoints in IntegrationEndpoints. The runtime itself is not
//   a read surface; this is.
//
// Coding Instructions:
//   THIS EXISTS BECAUSE NONE OF IT WAS REACHABLE. Until 2026-09-19 there was
//   no endpoints file for Integrations at all: CertificationStatus had four
//   levels and the shipped connector correctly declared FixtureTested, and no
//   reader could ever see it. A status nobody can read cannot be read
//   honestly, which is how the exit criterion put it.
//
//   Quarantine was the same shape. A rejected record was kept with the payload
//   that caused it and could be marked resolved in code, and `Resolve` carried
//   the comment "Does not replay it — nothing replays yet". Marking a bad
//   record dealt-with without re-running it is a queue that empties without
//   anything being fixed.
//
//   REPLAY RE-RUNS THE STORED PAYLOAD THROUGH THE REAL SINK. It does not
//   pretend, and it does not resolve on hope: a replay that fails again leaves
//   the row exactly where it was, with the new reason recorded. The only way
//   out of quarantine other than expiry is a record that actually applied, or
//   a person saying in writing why it never will.
//
//   ARMING A SCHEDULE IS AN EXERCISE OF THE CALLER'S OWN AUTHORITY, not a grant
//   of a new one. Whoever arms a feed becomes the name its runs are made under,
//   and the grant is re-checked every time it fires — so a schedule can never
//   outlive the permission that created it (ADR-028). A caller cannot arm a feed
//   for a rooftop they could not import to by hand.
//
//   NO SETTING OF KIND SECRET IS EVER RETURNED, not even in its protected form.
//   Same rule as the quarantine payload: a ciphertext looks safe to hand out and
//   is not, because it travels to the browser and into logs, and whoever later
//   obtains the key gets every one. A screen needs to know a credential IS SET,
//   which is a boolean.

using DealerFOSS.Core;

namespace DealerFOSS.Integrations;

public interface IIntegrations
{
    /// <summary>
    /// Every connector this build ships, with how far each has been proven.
    /// </summary>
    Task<Result<IReadOnlyList<ConnectorSummary>>> ConnectorsAsync(CancellationToken cancellationToken);

    /// <summary>Records held back and not yet dealt with, oldest first.</summary>
    Task<Result<IReadOnlyList<QuarantineEntry>>> QuarantineAsync(
        RooftopId? rooftopId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Re-runs one held record's stored payload through the sink that refused
    /// it.
    /// </summary>
    /// <remarks>
    /// Resolves the row only if it actually applies. A second refusal updates
    /// the reason and leaves it in the queue — the point of replay is to find
    /// out, not to clear the list.
    /// </remarks>
    Task<Result<ReplayOutcome>> ReplayAsync(Guid quarantinedRecordId, CancellationToken cancellationToken);

    /// <summary>
    /// Marks a held record dealt with, without re-running it — for the ones
    /// that will never apply and should stop being counted.
    /// </summary>
    /// <remarks>
    /// A note is required. "Resolved" with no reason is how a queue is cleared
    /// by somebody who did not look at it, and the note is the only thing that
    /// distinguishes a decision from a dismissal.
    /// </remarks>
    Task<Result> DismissAsync(Guid quarantinedRecordId, string note, CancellationToken cancellationToken);

    /// <summary>
    /// Every feed this dealership has a standing instruction to read, with how
    /// each one is doing.
    /// </summary>
    Task<Result<IReadOnlyList<SyncScheduleView>>> SchedulesAsync(
        RooftopId? rooftopId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Arm a feed to be read on its own, every <c>intervalMinutes</c>.
    /// </summary>
    /// <remarks>
    /// The caller becomes the schedule's authority: runs carry their permissions
    /// and are audited under their name, and the grant is re-checked every time
    /// it fires (ADR-028). Settings are validated against the connector's
    /// manifest here, so a missing required setting fails this request rather
    /// than every run at three in the morning.
    /// </remarks>
    Task<Result<Guid>> ArmAsync(ArmSyncRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Stop a feed firing, keeping its configuration and its history.
    /// </summary>
    Task<Result> DisarmAsync(Guid scheduleId, CancellationToken cancellationToken);

    /// <summary>
    /// Turn a feed back on — including one we suspended — under the calling
    /// person's authority, which is re-stated rather than inherited from
    /// whoever armed it before.
    /// </summary>
    Task<Result> RearmAsync(
        Guid scheduleId,
        int intervalMinutes,
        IReadOnlyDictionary<string, string?>? settings,
        CancellationToken cancellationToken);
}

/// <summary>What to read, how often, and with what settings.</summary>
/// <param name="Settings">
/// Keyed by the setting names the connector's manifest declares. A value for a
/// setting of kind <c>Secret</c> is plaintext here and is protected before it is
/// stored; it is never returned by any read.
/// </param>
public sealed record ArmSyncRequest(
    string Connector,
    Guid RooftopId,
    string Contract,
    int Version,
    int IntervalMinutes,
    IReadOnlyDictionary<string, string?> Settings);

/// <summary>One standing instruction, as a screen shows it.</summary>
/// <param name="ArmedBy">
/// The person whose authority runs carry. Shown because "who is this running
/// as" is the first question anybody asks about work that happens overnight,
/// and the answer being visible is half of what makes it answerable.
/// </param>
/// <param name="SuspendedReason">
/// Why we stopped it, when <paramref name="State"/> is <c>Suspended</c>. Null
/// otherwise — a feed somebody turned off has no reason to explain.
/// </param>
public sealed record SyncScheduleView(
    Guid Id,
    string Connector,
    RooftopId RooftopId,
    string Contract,
    int Version,
    int IntervalMinutes,

    /// <summary>
    /// <c>Armed</c>, <c>Disarmed</c> or <c>Suspended</c>, sent raw so the screen
    /// translates it rather than the server picking English.
    /// </summary>
    string State,
    string? SuspendedReason,
    Guid ArmedBy,
    DateTimeOffset? NextRunAt,
    DateTimeOffset? LastRunAt,

    /// <summary>The last run's outcome as its enum name, or null if it has never run.</summary>
    string? LastOutcome,

    /// <summary>
    /// What this feed needs told, and what it has been told. A secret reports
    /// only that it is set — see <see cref="ConnectorSettings"/>.
    /// </summary>
    IReadOnlyList<ConnectorSettingState> Settings);

/// <summary>One connector, as a screen shows it.</summary>
public sealed record ConnectorSummary(
    string Provider,
    string Version,

    /// <summary>
    /// The enum name — <c>FixtureTested</c>, <c>SandboxCertified</c>,
    /// <c>ProductionCertified</c> or <c>Experimental</c>. Sent raw so the
    /// screen translates it rather than the server picking English.
    /// </summary>
    string Certification,

    /// <summary>
    /// What each level actually promises, from the enum's own documentation, so
    /// a reader is not left to guess whether "fixture tested" means it works.
    /// </summary>
    IReadOnlyList<string> Capabilities,

    /// <summary>
    /// The connector's own declared limitations, shown whatever the
    /// certification says. The shipped fixture's first limitation is "Serves
    /// fabricated records. Never certify anything against this." — which is the
    /// single most important sentence on the screen.
    /// </summary>
    IReadOnlyList<string> KnownLimitations);

/// <summary>One held record, with enough to decide what to do about it.</summary>
public sealed record QuarantineEntry(
    Guid Id,
    string Connector,
    RooftopId RooftopId,
    string Contract,
    int Version,
    string ExternalId,
    string ReasonCode,
    string ReasonDetail,
    DateTimeOffset QuarantinedAt,
    DateTimeOffset ExpiresAt);

/// <summary>What happened when a held record was run again.</summary>
/// <param name="Applied">
/// True when the record went in. False means it was refused a second time and
/// is still held — see <paramref name="ReasonDetail"/> for the new reason,
/// which may differ from the original.
/// </param>
public sealed record ReplayOutcome(
    bool Applied,
    string? ReasonCode,
    string? ReasonDetail);
