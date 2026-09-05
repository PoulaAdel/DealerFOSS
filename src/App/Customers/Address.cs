// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Address — where a customer is, stored as a value on the customer rather than a
//   separate record.
//
// Usage:
//   new Address("12 Main St", null, "Springfield", "IL", "Sangamon", "62704", "US").
//
// Coding Instructions:
//   Deliberately not US-shaped. "AdministrativeArea" covers a state, province,
//   or region, and PostalCode is a string because postcodes are not numbers
//   and leading zeros matter (doc 04 §4). Country is an ISO 3166-1 alpha-2
//   code so a jurisdiction rule pack can key off it later.
//
//   COUNTY IS A SEPARATE FIELD AND THAT IS COMMERCIAL, NOT TIDINESS
//   (2026-09-05, ADR-024). US sales tax varies by state, county, and sometimes
//   city, so an address that folds county into "area" cannot express the thing
//   that decides the rate. That gap was found by measuring these fields against
//   STAR's address structure (ADR-023), which separates
//   StateOrProvinceCountrySub-DivisionID from CountyCountrySub-Division for the
//   same reason.
//
//   The two are NOT interchangeable and neither is derivable from the other.
//   Do not "helpfully" copy one into the other when a provider sends only one:
//   an absent county is absent (ADR-021), and a wrong county is a wrong tax
//   rate on a real invoice.
//
//   County is meaningless in most countries and null there. That is expected —
//   it is a sub-division slot, not a promise that every address has one.
//
//   Still not modelled, deliberately: an address TYPE. A customer holds one
//   address, and a type only discriminates between several. The registration
//   or garaging address that drives tax is a fact about a DEAL, frozen with it
//   (ADR-024 R3), so it belongs there rather than as a second customer field
//   nothing would populate.

namespace DealerFOSS.Customers;

public sealed record Address(
    string Line1,
    string? Line2,
    string City,
    string? AdministrativeArea,
    string? County,
    string? PostalCode,
    string Country)
{
    public static Address Create(
        string line1,
        string? line2,
        string city,
        string? administrativeArea,
        string? county,
        string? postalCode,
        string country)
    {
        if (string.IsNullOrWhiteSpace(line1))
        {
            throw new ArgumentException("An address needs a first line.", nameof(line1));
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            throw new ArgumentException("An address needs a city.", nameof(city));
        }

        if (string.IsNullOrWhiteSpace(country) || country.Trim().Length != 2)
        {
            throw new ArgumentException(
                "Country must be a two-letter ISO 3166-1 code.", nameof(country));
        }

        return new Address(
            line1.Trim(),
            string.IsNullOrWhiteSpace(line2) ? null : line2.Trim(),
            city.Trim(),
            string.IsNullOrWhiteSpace(administrativeArea) ? null : administrativeArea.Trim(),
            string.IsNullOrWhiteSpace(county) ? null : county.Trim(),
            string.IsNullOrWhiteSpace(postalCode) ? null : postalCode.Trim(),
            country.Trim().ToUpperInvariant());
    }
}
