// Department — a department inside a rooftop (Sales, Service, Parts...).
//
// Use:  created beneath a Rooftop.
// Edit: pure domain — no EF or ASP.NET types. DepartmentKind is persisted as a
//       string, so renaming a member is a data migration, not a rename.

using OpenDealer360.Core;

namespace OpenDealer360.Organization.Domain;

/// <summary>A department within a rooftop (doc 04 §1).</summary>
public sealed class Department : AuditableEntity
{
    public DepartmentId Id { get; private set; }

    public RooftopId RooftopId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DepartmentKind Kind { get; private set; }

    private Department()
    {
    }

    public Department(DepartmentId id, RooftopId rooftopId, string name, DepartmentKind kind)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Department name is required.", nameof(name));
        }

        Id = id;
        RooftopId = rooftopId;
        Name = name;
        Kind = kind;
    }
}

public enum DepartmentKind
{
    Sales = 0,
    FinanceAndInsurance = 1,
    Service = 2,
    Parts = 3,
    Accounting = 4,
    Administration = 5,
}
