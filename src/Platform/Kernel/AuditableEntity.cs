namespace OpenDealer360.Platform.Kernel;

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
