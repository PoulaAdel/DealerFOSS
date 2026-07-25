using OpenDealer360.Modules.Identity.Data;
using OpenDealer360.Modules.Identity.Domain;
using OpenDealer360.Core;

namespace OpenDealer360.Modules.Identity;

/// <summary>
/// Writes audit entries to the tenant's <c>identity.AuditEvents</c> table.
/// Identity owns the audit store (doc 04 §3); other modules record through the
/// <see cref="IAuditSink"/> abstraction and never touch the table.
/// </summary>
public sealed class SqlAuditSink(IdentityDbContext db, IClock clock) : IAuditSink
{
    private readonly IdentityDbContext _db = db;
    private readonly IClock _clock = clock;

    public async Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _db.AuditEvents.Add(new AuditEvent(
            Guid.NewGuid(),
            _clock.UtcNow,
            entry.ActorUserId,
            entry.Action,
            entry.Outcome,
            entry.ResourceType,
            entry.ResourceId,
            entry.RooftopId,
            entry.Reason,
            entry.SourceIp,
            entry.CorrelationId));

        await _db.SaveChangesAsync(cancellationToken);
    }
}
