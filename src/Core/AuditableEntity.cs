// AuditableEntity — base for records that carry audit columns and an optimistic
// concurrency stamp.
//
// Use:  inherit from it. Do not set CreatedAt/ModifiedAt by hand — each module's
//       DbContext stamps them centrally in SaveChangesAsync.
// Edit: rarely. Adding a field here changes every table that inherits it, so it
//       needs a migration in every module.

namespace OpenDealer360.Core;

/// <summary>
/// Base for business records that carry audit columns and an optimistic
/// concurrency stamp (doc 04 §4). The stamp and audit fields are set centrally
/// by the owning module's data context on save — not by hand at each call site
/// (doc 08 §5). This base is deliberately EF-free; mapping is configured in each
/// module's Data layer.
/// </summary>
public abstract class AuditableEntity
{
    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = "system";

    public DateTimeOffset? ModifiedAt { get; set; }

    public string? ModifiedBy { get; set; }

    /// <summary>Optimistic-concurrency token; refreshed on every save.</summary>
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
