// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ContractFields — the field names a contract is spoken in.
//
// Usage:
//   A connector maps the provider's own vocabulary INTO these names; a sink
//   reads them out. Both sides reference this file, so neither has to guess.
//
// Coding Instructions:
//   This is the seam that decides how much work a new connector is. Get it
//   wrong and every sink has to learn every provider's field names, which is
//   one mapping per provider PER capability — the combination that makes an
//   integration layer collapse under its own weight at about the fourth
//   provider.
//
//   So: the connector translates, exactly once, at the edge. Everything
//   inside speaks the contract. A field added here is a change to a
//   published contract and needs a version bump (doc 05 §2) — connectors
//   compiled against v1 must keep working.
//
//   Names are lowerCamelCase and dotted by area, matching the JSON style the
//   API already uses. They are compared with Ordinal, so case matters.
//
//   THESE NAMES ARE OURS ON PURPOSE, AND THAT WAS DECIDED RATHER THAN DEFAULTED
//   (ADR-023, 2026-09-04). The industry standard is STAR, whose vocabulary is
//   published openly — so the question was settled on merit, not on access.
//
//   STAR is a WIRE FORMAT: nested XML Business Object Documents where an address
//   offers a choice between five free-text lines and a structured form, and
//   several elements repeat. This dictionary is flat `string -> string?`, so it
//   can carry STAR's names but not STAR's shape — and STAR-looking names on a
//   non-STAR structure imply an interoperability that does not exist. A STAR
//   connector translates at the edge like any other. Read the ADR before
//   renaming anything here; it is a question that looks new every time.
//
//   What STAR IS good for is coverage. When you extend a contract, check it
//   against the matching STAR noun and record what you deliberately leave out.
//   Doing that once already found three gaps, listed in the ADR — the sharpest
//   being that `customer.address.area` collapses state and county, while US
//   sales tax varies by both.

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
