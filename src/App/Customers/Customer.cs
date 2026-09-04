// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Customer — a person or a business the dealership deals with.
//
// Usage:
//   Customer.Person(...) or Customer.Business(...). Contact details are added
//   separately, because a customer can have several and none is mandatory.
//
// Coding Instructions:
//   A customer belongs to the whole dealer organization, not to one rooftop
//   (doc 04 §1) — the same person may buy at one location and service at
//   another. HomeRooftopId records where they were first met; it is not a
//   permission boundary and must never be used as one.

using DealerFOSS.Core;

namespace DealerFOSS.Customers;

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

    /// <summary>
    /// This customer's identifier in the system they came from, when they were
    /// imported rather than typed in. It is what makes re-running an import
    /// update the same person instead of creating a second one, and it is why a
    /// dealership can import a corrected file without cleaning up afterwards.
    /// </summary>
    /// <remarks>
    /// Null for a customer created by hand. Unique among those that have one, so
    /// two source records cannot quietly collapse into one — see the note in
    /// CustomerTables about why a filtered index is used rather than a plain one.
    /// </remarks>
    public string? ExternalReference { get; private set; }

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
    /// Records where this customer came from. Set once at import; changing it
    /// later would break the link an importer relies on to find them again.
    /// </summary>
    public void SetExternalReference(string? reference)
    {
        if (ExternalReference is not null)
        {
            throw new InvalidOperationException(
                "This customer is already linked to a record in another system. "
                + "Re-pointing that link would orphan the original.");
        }

        var trimmed = reference?.Trim();
        ExternalReference = string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

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
