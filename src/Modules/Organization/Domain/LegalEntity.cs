using OpenDealer360.Platform.Kernel;

namespace OpenDealer360.Modules.Organization.Domain;

/// <summary>
/// A legal entity within the dealer organization. Deals, accounting entries, and
/// other regulated records are owned by a legal entity as well as a rooftop
/// (doc 04 §1).
/// </summary>
public sealed class LegalEntity : AuditableEntity
{
    public LegalEntityId Id { get; private set; }

    public DealerOrganizationId OrganizationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? RegisteredName { get; private set; }

    public string? TaxId { get; private set; }

    public ICollection<Rooftop> Rooftops { get; } = new List<Rooftop>();

    private LegalEntity()
    {
    }

    public LegalEntity(LegalEntityId id, DealerOrganizationId organizationId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Legal entity name is required.", nameof(name));
        }

        Id = id;
        OrganizationId = organizationId;
        Name = name;
    }

    public void SetRegistration(string? registeredName, string? taxId)
    {
        RegisteredName = registeredName;
        TaxId = taxId;
    }
}
