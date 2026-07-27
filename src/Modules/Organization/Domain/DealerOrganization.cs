// DealerOrganization — the tenant itself, and the root of the structure tree.
//
// Use:  exactly one row exists per tenant database.
// Edit: this is pure domain — no EF or ASP.NET types may appear here, and an
//       architecture test enforces it. Invariants belong in the constructor so
//       an invalid organization cannot be created at all.

using OpenDealer360.Core;

namespace OpenDealer360.Organization.Domain;

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
