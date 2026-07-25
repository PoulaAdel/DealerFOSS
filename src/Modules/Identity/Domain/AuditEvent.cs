namespace OpenDealer360.Modules.Identity.Domain;

/// <summary>
/// An append-only audit record (ADR-016). Never updated or deleted: a
/// correction is a new record. Holds no credentials, tokens, or document
/// content (doc 06 §4).
/// </summary>
public sealed class AuditEvent
{
    public Guid Id { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string Outcome { get; private set; } = string.Empty;

    public string? ResourceType { get; private set; }

    public string? ResourceId { get; private set; }

    public Guid? RooftopId { get; private set; }

    public string? Reason { get; private set; }

    public string? SourceIp { get; private set; }

    public string? CorrelationId { get; private set; }

    private AuditEvent()
    {
    }

    public AuditEvent(
        Guid id,
        DateTimeOffset occurredAt,
        Guid? actorUserId,
        string action,
        string outcome,
        string? resourceType,
        string? resourceId,
        Guid? rooftopId,
        string? reason,
        string? sourceIp,
        string? correlationId)
    {
        Id = id;
        OccurredAt = occurredAt;
        ActorUserId = actorUserId;
        Action = action;
        Outcome = outcome;
        ResourceType = resourceType;
        ResourceId = resourceId;
        RooftopId = rooftopId;
        Reason = reason;
        SourceIp = sourceIp;
        CorrelationId = correlationId;
    }
}
