// SqlAuditSink — writes audit entries into the tenant's identity.AuditEvents
// table. Identity owns the audit store; other modules go through IAuditSink.
//
// Use:  registered for IAuditSink; callers never reference this class.
// Edit: audit rows are append-only (ADR-016) — write new rows, never update.
//       Nothing sensitive may be placed in an entry; see IAuditSink.

using DealerFOSS.Identity;
using DealerFOSS.Core;

namespace DealerFOSS.Identity;

/// <summary>
/// Writes audit entries to the tenant's <c>identity.AuditEvents</c> table.
/// Identity owns the audit store (doc 04 §3); other modules record through the
/// <see cref="IAuditSink"/> abstraction and never touch the table.
/// </summary>
internal sealed class SqlAuditSink(IdentityDb db, IClock clock) : IAuditSink
{
    private readonly IdentityDb _db = db;
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
