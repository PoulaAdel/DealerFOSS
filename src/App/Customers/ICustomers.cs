// ICustomers — the Customers module's public contract, and the only part
// of it other modules may reference (ADR-008).
//
// Use:  Sales and Service will confirm a customer exists and read a summary
//       through this. They never touch the customer tables.
// Edit: keep the returned shapes small. A module that needs a field not here
//       should say why — widening the contract couples every caller to it.

using OpenDealer360.Core;

namespace OpenDealer360.Customers;

public interface ICustomers
{
    /// <summary>
    /// Finds customers by name, email, or phone. An empty term returns the most
    /// recently added, so the screen has something to show before typing.
    /// </summary>
    Task<Result<IReadOnlyList<CustomerSummary>>> SearchAsync(
        string? term,
        int limit,
        CancellationToken cancellationToken);

    Task<Result<CustomerDetail>> GetAsync(Guid customerId, CancellationToken cancellationToken);

    Task<Result<CustomerDetail>> AddAsync(NewCustomer customer, CancellationToken cancellationToken);
}

/// <summary>Enough to identify a customer in a list.</summary>
public sealed record CustomerSummary(
    Guid Id,
    string DisplayName,
    string Kind,
    string? PrimaryEmail,
    string? PrimaryPhone);

/// <summary>One customer in full, as a screen or another module would show them.</summary>
public sealed record CustomerDetail(
    Guid Id,
    string DisplayName,
    string Kind,
    string FirstName,
    string LastName,
    RooftopId? HomeRooftopId,
    AddressView? Address,
    IReadOnlyList<ContactPointView> ContactPoints);

public sealed record ContactPointView(Guid Id, string Kind, string Value, bool IsPrimary);

public sealed record AddressView(
    string Line1,
    string? Line2,
    string City,
    string? AdministrativeArea,
    string? PostalCode,
    string Country);

/// <summary>What a caller supplies to create a customer.</summary>
public sealed record NewCustomer(
    string Kind,
    string? FirstName,
    string LastName,
    RooftopId? HomeRooftopId,
    string? Email,
    string? Phone,
    AddressView? Address);
