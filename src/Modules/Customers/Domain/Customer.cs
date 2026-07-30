// Customer — a person or a business the dealership deals with.
//
// Use:  Customer.Person(...) or Customer.Business(...). Contact details are added
//       separately, because a customer can have several and none is mandatory.
// Edit: a customer belongs to the whole dealer organization, not to one rooftop
//       (doc 04 §1) — the same person may buy at one location and service at
//       another. HomeRooftopId records where they were first met; it is not a
//       permission boundary and must never be used as one.

using OpenDealer360.Core;

namespace OpenDealer360.Customers.Domain;

public sealed class Customer : AuditableEntity
{
    private readonly List<ContactPoint> _contactPoints = [];

    public Guid Id { get; private set; }

    public CustomerKind Kind { get; private set; }

    /// <summary>Family name for a person; the trading name for a business.</summary>
    public string LastName { get; private set; } = string.Empty;

    /// <summary>Empty for a business.</summary>
    public string FirstName { get; private set; } = string.Empty;

    /// <summary>Where this customer was first recorded. Reporting only, never authorization.</summary>
    public RooftopId? HomeRooftopId { get; private set; }

    public Address? Address { get; private set; }

    public bool IsArchived { get; private set; }

    public IReadOnlyCollection<ContactPoint> ContactPoints => _contactPoints;

    /// <summary>What a person sees in a list. Built here so every screen agrees.</summary>
    public string DisplayName => Kind == CustomerKind.Business
        ? LastName
        : $"{FirstName} {LastName}".Trim();

    private Customer()
    {
    }

    private Customer(Guid id, CustomerKind kind, string firstName, string lastName, RooftopId? homeRooftopId)
    {
        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new ArgumentException(
                kind == CustomerKind.Business ? "A business needs a name." : "A person needs a last name.",
                nameof(lastName));
        }

        Id = id;
        Kind = kind;
        FirstName = (firstName ?? string.Empty).Trim();
        LastName = lastName.Trim();
        HomeRooftopId = homeRooftopId;
    }

    public static Customer Person(Guid id, string firstName, string lastName, RooftopId? homeRooftopId = null) =>
        new(id, CustomerKind.Person, firstName, lastName, homeRooftopId);

    public static Customer Business(Guid id, string name, RooftopId? homeRooftopId = null) =>
        new(id, CustomerKind.Business, string.Empty, name, homeRooftopId);

    public void Rename(string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new ArgumentException("A name is required.", nameof(lastName));
        }

        FirstName = (firstName ?? string.Empty).Trim();
        LastName = lastName.Trim();
    }

    public void SetAddress(Address? address) => Address = address;

    /// <summary>
    /// Adds a way to reach this customer. The same value is not added twice, so
    /// importing the same record again does not accumulate duplicates.
    /// </summary>
    public ContactPoint AddContactPoint(Guid id, ContactKind kind, string value, bool isPrimary = false)
    {
        var normalized = ContactPoint.Normalize(kind, value);

        var existing = _contactPoints.Find(c => c.Kind == kind && c.Value == normalized);
        if (existing is not null)
        {
            if (isPrimary)
            {
                MakePrimary(existing);
            }

            return existing;
        }

        var point = new ContactPoint(id, Id, kind, normalized);
        _contactPoints.Add(point);

        // The first contact of a kind becomes primary automatically: otherwise a
        // customer with exactly one phone number has no primary phone.
        if (isPrimary || !_contactPoints.Exists(c => c.Kind == kind && c.IsPrimary && c != point))
        {
            MakePrimary(point);
        }

        return point;
    }

    /// <summary>Archives rather than deletes: history must stay attributable.</summary>
    public void Archive() => IsArchived = true;

    private void MakePrimary(ContactPoint point)
    {
        foreach (var other in _contactPoints.Where(c => c.Kind == point.Kind))
        {
            other.SetPrimary(other == point);
        }
    }
}

public enum CustomerKind
{
    Person = 0,
    Business = 1,
}
