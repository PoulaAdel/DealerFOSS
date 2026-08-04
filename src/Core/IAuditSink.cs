// IAuditSink — how a security-sensitive event is recorded.
//
// Use:  inject it and call RecordAsync. The Identity module implements it and
//       owns the table; nothing else writes audit rows directly.
// Edit: never put credentials, tokens, credit data, government ids, or document
//       content into an AuditEntry. Audit rows are append-only (ADR-016).

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
