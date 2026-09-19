// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   QuarantinedRecord — a record that arrived and could not be applied.
//
// Usage:
//   Written by the runtime when a record will not map. Holds the provider's
//   own payload so the decision can be reviewed against what actually
//   arrived, not against what we made of it.
//
// Coding Instructions:
//   The payload is PERSONAL DATA — customer names, addresses and telephone
//   numbers, exactly as the provider sent them. ADR-022 therefore applies in
//   full: it carries an expiry set from a declared retention, and reads
//   filter on that expiry so an out-of-date row stops being visible whether
//   or not anything has physically deleted it yet.
//
//   The uncomfortable consequence is deliberate and must not be designed
//   away: a quarantined record left unresolved past its retention is gone,
//   and that is the right trade. Keeping a customer's details indefinitely
//   because a mapping bug was never fixed is not a data-quality feature, it
//   is a data-protection failure with a queue in front of it.

using DealerFOSS.Core;

namespace DealerFOSS.Integrations;

/// <summary>One record held back from applying, with the payload that caused it.</summary>
public sealed class QuarantinedRecord : AuditableEntity
{
    /// <summary>
    /// How long a held record is kept before it stops counting. Long enough for
    /// somebody back from leave to still find it; short enough that a forgotten
    /// queue is not an indefinite store of other people's customers.
    /// </summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(90);

    public Guid Id { get; private set; }

    public string Connector { get; private set; } = string.Empty;

    public RooftopId RooftopId { get; private set; }

    public string Contract { get; private set; } = string.Empty;

    public int Version { get; private set; }

    /// <summary>The provider's identifier for the record, so a fix can be checked.</summary>
    public string ExternalId { get; private set; } = string.Empty;

    public string? ExternalVersion { get; private set; }

    /// <summary>
    /// The provider's fields as they arrived, serialized. Personal data — see the
    /// header, and never write this to a log.
    /// </summary>
    public string Payload { get; private set; } = string.Empty;

    /// <summary>The stable code from <see cref="IntegrationErrors"/>.</summary>
    public string ReasonCode { get; private set; } = string.Empty;

    public string ReasonDetail { get; private set; } = string.Empty;

    public DateTimeOffset QuarantinedAt { get; private set; }

    /// <summary>When this row stops counting, whatever has or has not deleted it.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>When somebody dealt with it. Null while it is still waiting.</summary>
    public DateTimeOffset? ResolvedAt { get; private set; }

    public string? ResolutionNote { get; private set; }

    private QuarantinedRecord()
    {
    }

    public QuarantinedRecord(
        Guid id,
        string connector,
        RooftopId rooftopId,
        ConnectorCapability capability,
        string externalId,
        string? externalVersion,
        string payload,
        Error reason,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(reason);

        if (string.IsNullOrWhiteSpace(externalId))
        {
            throw new ArgumentException("A provider identifier is required.", nameof(externalId));
        }

        Id = id;
        Connector = connector;
        RooftopId = rooftopId;
        Contract = capability.Contract;
        Version = capability.Version;
        ExternalId = externalId;
        ExternalVersion = externalVersion;
        Payload = payload;
        ReasonCode = reason.Code;
        ReasonDetail = reason.Message;
        QuarantinedAt = now;
        ExpiresAt = now + Retention;
    }

    /// <summary>
    /// True once the retention has run out. Reads filter on this rather than
    /// waiting for a purge, so the expiry means something from the day it is
    /// written — there is no purge job yet, and a column nothing enforces is
    /// worse than no column.
    /// </summary>
    public bool HasExpiredAt(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>How many times somebody has run this record again.</summary>
    public int ReplayAttempts { get; private set; }

    /// <summary>When it was last run again, or null if it never has been.</summary>
    public DateTimeOffset? LastReplayedAt { get; private set; }

    /// <summary>
    /// Records that a replay was refused again, with the reason it was refused
    /// THIS time.
    /// </summary>
    /// <remarks>
    /// The new reason replaces the old deliberately. Fixing one mapping
    /// routinely reveals the next problem behind it, and a screen still showing
    /// the original refusal would send somebody to look in a place that is
    /// already correct. The attempt count is what preserves the history that
    /// this has been tried before.
    /// </remarks>
    public void Refuse(Error reason, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(reason);

        if (ResolvedAt is not null)
        {
            throw new InvalidOperationException("This record has already been resolved.");
        }

        ReasonCode = reason.Code;
        ReasonDetail = reason.Message;
        ReplayAttempts++;
        LastReplayedAt = now;
    }

    /// <summary>
    /// Mark it dealt with — because a replay applied it, or because somebody
    /// decided in writing that it never will.
    /// </summary>
    public void Resolve(DateTimeOffset now, string note)
    {
        if (ResolvedAt is not null)
        {
            throw new InvalidOperationException("This record has already been resolved.");
        }

        ResolvedAt = now;
        ResolutionNote = string.IsNullOrWhiteSpace(note) ? "Resolved." : note.Trim();
    }
}
