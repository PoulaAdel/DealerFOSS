using OpenDealer360.Platform.Kernel;

namespace OpenDealer360.Modules.Organization.Domain;

/// <summary>
/// A rooftop — one physical dealership location. Inventory, deals, and repair
/// orders record their rooftop; a user can hold different roles at different
/// rooftops (doc 04 §1).
/// </summary>
public sealed class Rooftop : AuditableEntity
{
    public RooftopId Id { get; private set; }

    public LegalEntityId LegalEntityId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Short internal code (e.g. "NAG-01").</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>IANA time zone id used to derive dealership-local dates (doc 04 §4).</summary>
    public string TimeZone { get; private set; } = "UTC";

    public ICollection<Department> Departments { get; } = new List<Department>();

    private Rooftop()
    {
    }

    public Rooftop(RooftopId id, LegalEntityId legalEntityId, string name, string code, string timeZone)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Rooftop name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Rooftop code is required.", nameof(code));
        }

        Id = id;
        LegalEntityId = legalEntityId;
        Name = name;
        Code = code;
        TimeZone = string.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone;
    }
}
