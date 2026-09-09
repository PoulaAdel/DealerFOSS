// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   DealTaxLine — one tax charged on one deal, recorded as evidence rather than
//   as something to work out again later. Also TaxProvenance, which says where
//   the figure came from, and TaxAddress, the address it was resolved from.
//
// Usage:
//   Added through Deal.SetTax, never constructed directly, so the Draft-only
//   rule and the provenance rule always apply.
//
// Coding Instructions:
//   THE LINE IS THE ANSWER, NOT A POINTER TO ONE (ADR-024 R3). It carries the
//   amount, the basis it was worked out on, the rate, the jurisdiction, the
//   pack and version that produced it, and the address that resolved it.
//   Nothing recomputes any of that on read. A customer who moves house does not
//   retroactively change what a sale was taxed at, and a rate table updated next
//   quarter does not rewrite last quarter's deals.
//
//   PROVENANCE IS REQUIRED AND "A PERSON TYPED IT" IS A REAL ANSWER (R4). That
//   is not a gap to close later: it is what lets the product work in a
//   jurisdiction nobody has written a pack for, which is every jurisdiction on
//   the first day. An unsupported jurisdiction is a label, not a blocker (R5).
//   A number with no provenance is the thing this type exists to prevent.
//
//   The address is a SNAPSHOT and deliberately not the customer's Address type.
//   Deals may not reference Customers' entities (FeatureBoundaryTests), and more
//   importantly this is a record of what was used at the time — copying the four
//   fields that decide a rate is the honest shape, not a foreign key to a row
//   somebody can edit afterwards.

namespace DealerFOSS.Deals;

/// <summary>Where a tax figure came from. Every line has one (ADR-024 R4).</summary>
public enum TaxProvenance
{
    /// <summary>
    /// A person typed it. Valid, and the reason an unsupported jurisdiction is a
    /// label rather than a blocker — but it must say so on the screen and in the
    /// record, because nobody can audit a figure whose source is unknown.
    /// </summary>
    EnteredByPerson = 0,

    /// <summary>A jurisdiction pack's rate table, at the version named on the line.</summary>
    Pack = 1,

    /// <summary>A tax provider answered. The call's own evidence lives with the run.</summary>
    Vendor = 2,
}

/// <summary>
/// The parts of an address that decide a tax rate, copied at the moment the tax
/// was worked out.
/// </summary>
/// <remarks>
/// State AND county, separately, because US sales tax varies by both — the gap
/// closed on 2026-09-05. Postcode because Streamlined Sales Tax member states
/// publish their boundaries keyed to it.
/// </remarks>
public sealed record TaxAddress(
    string? AdministrativeArea,
    string? County,
    string? PostalCode,
    string Country)
{
    public static TaxAddress Create(
        string? administrativeArea,
        string? county,
        string? postalCode,
        string country)
    {
        if (string.IsNullOrWhiteSpace(country) || country.Trim().Length != 2)
        {
            throw new ArgumentException(
                "A tax address needs a two-letter ISO 3166-1 country.", nameof(country));
        }

        return new TaxAddress(
            string.IsNullOrWhiteSpace(administrativeArea) ? null : administrativeArea.Trim(),
            string.IsNullOrWhiteSpace(county) ? null : county.Trim(),
            string.IsNullOrWhiteSpace(postalCode) ? null : postalCode.Trim(),
            country.Trim().ToUpperInvariant());
    }
}

/// <summary>One tax charged on a deal, with everything needed to explain it later.</summary>
public sealed class DealTaxLine
{
    public Guid Id { get; private set; }

    public Guid DealId { get; private set; }

    /// <summary>What a person reads on the paperwork — "Illinois sales tax".</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>The taxing authority, in whatever code the pack uses. Free text when a person typed it.</summary>
    public string Jurisdiction { get; private set; } = string.Empty;

    /// <summary>The amount the rate was applied to. Recorded because it is not recoverable later.</summary>
    public decimal Basis { get; private set; }

    /// <summary>As a fraction: 0.0625m is six and a quarter percent. Zero when a person typed the amount.</summary>
    public decimal Rate { get; private set; }

    /// <summary>What was actually charged. Never negative — tax off is a different act.</summary>
    public decimal Amount { get; private set; }

    public TaxProvenance Provenance { get; private set; }

    /// <summary>The pack that produced this, and its version. Null when a person typed it.</summary>
    public string? PackId { get; private set; }

    public int? PackVersion { get; private set; }

    private DealTaxLine()
    {
    }

    internal DealTaxLine(
        Guid id,
        Guid dealId,
        string description,
        string jurisdiction,
        decimal basis,
        decimal rate,
        decimal amount,
        TaxProvenance provenance,
        string? packId,
        int? packVersion)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("A tax line needs a description.", nameof(description));
        }

        if (string.IsNullOrWhiteSpace(jurisdiction))
        {
            throw new ArgumentException(
                "A tax line needs a jurisdiction, even if it is only the country.", nameof(jurisdiction));
        }

        if (amount < 0)
        {
            throw new ArgumentException(
                "A tax line cannot be negative. Money coming off is a discount, not a tax.", nameof(amount));
        }

        if (rate < 0)
        {
            throw new ArgumentException("A tax rate cannot be negative.", nameof(rate));
        }

        // A pack-produced figure that cannot name its pack is unauditable, which
        // is the whole failure this type exists to prevent. The reverse matters
        // just as much: a person-entered figure carrying a pack id would claim an
        // authority nobody exercised.
        if (provenance == TaxProvenance.Pack && (string.IsNullOrWhiteSpace(packId) || packVersion is null))
        {
            throw new ArgumentException(
                "A tax from a pack must name the pack and its version, or it cannot be audited.",
                nameof(packId));
        }

        if (provenance == TaxProvenance.EnteredByPerson && packId is not null)
        {
            throw new ArgumentException(
                "A figure a person typed does not come from a pack. Leave the pack unnamed.",
                nameof(packId));
        }

        Id = id;
        DealId = dealId;
        Description = description.Trim();
        Jurisdiction = jurisdiction.Trim();
        Basis = basis;
        Rate = rate;
        Amount = amount;
        Provenance = provenance;
        PackId = string.IsNullOrWhiteSpace(packId) ? null : packId.Trim();
        PackVersion = packVersion;
    }
}
