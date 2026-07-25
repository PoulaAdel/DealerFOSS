using OpenDealer360.Core;

namespace OpenDealer360.Modules.Organization.Domain;

/// <summary>
/// The dealer organization — the tenant itself (doc 04 §1). Exactly one row
/// exists per tenant database; it is the root of the legal-entity / rooftop /
/// department tree. Pure domain: no EF or ASP.NET dependency.
/// </summary>
public sealed class DealerOrganization : AuditableEntity
{
    public DealerOrganizationId Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public ICollection<LegalEntity> LegalEntities { get; } = new List<LegalEntity>();

    private DealerOrganization()
    {
        // EF materialization.
    }

    public DealerOrganization(DealerOrganizationId id, string name, string slug)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Organization name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException("Organization slug is required.", nameof(slug));
        }

        Id = id;
        Name = name;
        Slug = slug;
    }
}
