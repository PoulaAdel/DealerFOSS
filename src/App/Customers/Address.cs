// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Address — where a customer is, stored as a value on the customer rather than a
//   separate record.
//
// Usage:
//   new Address("12 Main St", null, "Springfield", "IL", "62704", "US").
//
// Coding Instructions:
//   Deliberately not US-shaped. "AdministrativeArea" covers a state, province,
//   or region, and PostalCode is a string because postcodes are not numbers
//   and leading zeros matter (doc 04 §4). Country is an ISO 3166-1 alpha-2
//   code so a jurisdiction rule pack can key off it later.

namespace DealerFOSS.Customers;

public sealed record Address(
    string Line1,
    string? Line2,
    string City,
    string? AdministrativeArea,
    string? PostalCode,
    string Country)
{
    public static Address Create(
        string line1,
        string? line2,
        string city,
        string? administrativeArea,
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
            string.IsNullOrWhiteSpace(postalCode) ? null : postalCode.Trim(),
            country.Trim().ToUpperInvariant());
    }
}
