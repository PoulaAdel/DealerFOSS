// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   How a security-sensitive event is recorded. Declared in Core so any
//   capability can write one, implemented in Identity so only Identity owns the
//   table — the same shape as every other cross-capability contract here.
//
//   Audit rows are append-only (ADR-016), and the data context enforces that
//   rather than trusting callers. An audit trail that can be edited is not an
//   audit trail.
//
// Usage:
//   Inject IAuditSink, call RecordAsync. Denials are recorded as well as
//   successes — a refused attempt is usually the more interesting row.
//
// Coding Instructions:
//   NEVER put credentials, tokens, credit data, government identifiers, or
//   document content into an AuditEntry. The trail is retained long, exported,
//   and read by people who are not entitled to those values.
//
//   Record what was attempted, by whom, against what, and the outcome. A
//   before/after summary is welcome; a payload dump is not.

namespace DealerFOSS.Core;

/// <summary>
/// Records security-sensitive events. Audit is append-only (ADR-016) and is
/// written through shared behaviour rather than a line each caller can forget
/// (doc 08 §5).
/// </summary>
public interface IAuditSink
{
    Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken);
}

/// <summary>
/// One audit record: who did what, to which resource, in which scope, and with
/// what outcome (doc 06 §5). Carries no credentials, tokens, or document
/// content — those must never reach the audit trail.
/// </summary>
public sealed record AuditEntry(
    Guid? ActorUserId,
    string Action,
    string Outcome,
    string? ResourceType,
    string? ResourceId,
    Guid? RooftopId,
    string? Reason,
    string? SourceIp,
    string? CorrelationId)
{
    public static AuditEntry Denied(
        Guid? actorUserId,
        string action,
        string? resourceType,
        string? resourceId,
        Guid? rooftopId,
        string reason,
        string? sourceIp = null,
        string? correlationId = null) =>
        new(actorUserId, action, AuditOutcome.Denied, resourceType, resourceId, rooftopId, reason, sourceIp, correlationId);
}

public static class AuditOutcome
{
    public const string Allowed = "Allowed";
    public const string Denied = "Denied";
}
