// ICustomers — what other capabilities may call to reach customer records.
//
// Use:  Leads, Sales, and Service confirm a customer exists and read a summary
//       through this. They never touch the customer tables.
// Edit: keep the returned shapes small. A capability that needs a field not here
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

    /// <summary>
    /// Names for a known set of customers, in one query. A work list that shows
    /// customer names would otherwise fetch them one at a time — this exists so a
    /// caller never has to choose between a slow screen and a stale copy of the
    /// name. Unknown ids are simply absent from the result.
    /// </summary>
    Task<Result<IReadOnlyList<CustomerSummary>>> GetManyAsync(
        IReadOnlyCollection<Guid> customerIds,
        CancellationToken cancellationToken);

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
