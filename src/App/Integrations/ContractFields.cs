// ContractFields — the field names a contract is spoken in.
//
// Use:  a connector maps the provider's own vocabulary INTO these names; a sink
//       reads them out. Both sides reference this file, so neither has to guess.
// Edit: this is the seam that decides how much work a new connector is. Get it
//       wrong and every sink has to learn every provider's field names, which is
//       one mapping per provider PER capability — the combination that makes an
//       integration layer collapse under its own weight at about the fourth
//       provider.
//
//       So: the connector translates, exactly once, at the edge. Everything
//       inside speaks the contract. A field added here is a change to a
//       published contract and needs a version bump (doc 05 §2) — connectors
//       compiled against v1 must keep working.
//
//       Names are lowerCamelCase and dotted by area, matching the JSON style the
//       API already uses. They are compared with Ordinal, so case matters.

namespace DealerFOSS.Integrations;

/// <summary>
/// Field names for the <c>Customers</c> contract, version 1.
/// </summary>
/// <remarks>
/// Deliberately small. A contract is the set of fields we are prepared to accept
/// responsibility for mapping correctly from any provider — not everything a
/// provider happens to send. Widening it is cheap to type and expensive to keep
/// honest, because every connector then has to fill it.
/// </remarks>
public static class CustomerFields
{
    /// <summary>Contract name, as declared on a <see cref="ConnectorCapability"/>.</summary>
    public const string Contract = "Customers";

    public const int Version = 1;

    /// <summary>"Person" or "Business". Anything else is a rejection, not a guess.</summary>
    public const string Kind = "customer.kind";

    public const string FirstName = "customer.firstName";

    /// <summary>Required. For a business this is the trading name.</summary>
    public const string LastName = "customer.lastName";

    public const string Email = "customer.email";

    public const string Phone = "customer.phone";

    public const string AddressLine1 = "customer.address.line1";

    public const string AddressLine2 = "customer.address.line2";

    public const string City = "customer.address.city";

    public const string AdministrativeArea = "customer.address.area";

    public const string PostalCode = "customer.address.postalCode";

    /// <summary>ISO country code, or the provider's own text if that is all it has.</summary>
    public const string Country = "customer.address.country";
}
