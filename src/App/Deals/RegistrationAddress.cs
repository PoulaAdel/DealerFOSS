// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   RegistrationAddress — where the buyer will register or garage the car, kept
//   on the deal rather than the customer.
//
// Usage:
//   Set through Deal.SetRegistrationAddress while the deal is open, alongside
//   the terms and the tax.
//
// Coding Instructions:
//   NOT THE CUSTOMER'S MAILING ADDRESS, AND NOT COPIED FROM Customers.Address
//   (ADR-024). A customer's Address.cs already carries the reasoning for the
//   individual fields; the reason THIS type exists at all, separately, is that
//   ADR-024 traces the taxing address to "the buyer's registration address...
//   not the customer's current address" — somebody may live in one state and
//   register the car in another, and a customer who moves house afterwards must
//   not retroactively change what a past deal was taxed at (R3). A deal may not
//   depend on Customers' entities (FeatureBoundaryTests) even if it could copy
//   the shape; keeping the type here also keeps that true structurally, not by
//   convention.
//
//   Full postal shape — Line1/Line2/City included — unlike the narrower
//   TaxAddress next to it in DealTaxLine.cs, which keeps only the four fields a
//   rate needs. This is the address that ends up on registration paperwork, so
//   it has to be postable, not just taxable.

namespace DealerFOSS.Deals;

public sealed record RegistrationAddress(
    string Line1,
    string? Line2,
    string City,
    string? AdministrativeArea,
    string? County,
    string? PostalCode,
    string Country)
{
    public static RegistrationAddress Create(
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
            throw new ArgumentException("A registration address needs a first line.", nameof(line1));
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            throw new ArgumentException("A registration address needs a city.", nameof(city));
        }

        if (string.IsNullOrWhiteSpace(country) || country.Trim().Length != 2)
        {
            throw new ArgumentException(
                "Country must be a two-letter ISO 3166-1 code.", nameof(country));
        }

        return new RegistrationAddress(
            line1.Trim(),
            string.IsNullOrWhiteSpace(line2) ? null : line2.Trim(),
            city.Trim(),
            string.IsNullOrWhiteSpace(administrativeArea) ? null : administrativeArea.Trim(),
            string.IsNullOrWhiteSpace(county) ? null : county.Trim(),
            string.IsNullOrWhiteSpace(postalCode) ? null : postalCode.Trim(),
            country.Trim().ToUpperInvariant());
    }
}
