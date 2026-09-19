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
}

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
