// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   PackDocument — the file format a jurisdiction pack is written in, and the
//   reader that turns one into rows. Also PackFormatException, which is how a
//   bad pack fails: loudly, naming what is wrong, before anything is stored.
//
// Usage:
//   PackDocumentReader.Read(bytes) -> a JurisdictionPack with its rates and
//   rules attached, not yet saved and always Draft.
//
// Coding Instructions:
//   UNKNOWN KEYS ARE REFUSED, by UnmappedMemberHandling.Disallow rather than by
//   hand-written checks. This is ADR-024 R1 made mechanical: a pack author who
//   writes "leaseTaxedOnPayment": true into the rules block gets a refusal
//   naming the key, instead of a pack that loads, looks complete, and silently
//   taxes a lease the wrong way because nothing reads that field. The moment a
//   pack genuinely needs a flag these shapes cannot express, the answer is to
//   add it to TaxBasisRules in the domain and here — never to let the pack carry
//   a rule the engine does not honour.
//
//   COMMENTS ARE ALLOWED IN A PACK FILE, which strict JSON does not permit, and
//   the reason is worth the deviation: these files are tax data whose numbers
//   need their reasoning attached — which state publication, which worked
//   example, which figure was double-checked. A rate with no explanation beside
//   it is how a pack rots. Only this reader parses these files, so nothing else
//   has to tolerate the comments.
//
//   THE HASH IS OVER THE RAW BYTES, including the comments. A reviewer approves
//   a file, not a summary of one: if somebody edits a comment that changes what
//   a number means, the hash changes and the approval no longer covers it. That
//   is the intended behaviour, not an inconvenience.
//
//   The reader never consults the clock and never writes. It cannot produce a
//   Supported pack, because JurisdictionPack's constructor does not offer that —
//   review is a separate act by a person, recorded separately.

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DealerFOSS.Compliance;

/// <summary>A pack file that cannot be loaded, and why, in words a person can act on.</summary>
public sealed class PackFormatException(string message) : Exception(message);

/// <summary>
/// The on-disk shape of a pack. Every field is data; there is nowhere to put a
/// conditional, which is the point (ADR-024 R1).
/// </summary>
internal sealed record PackDocument
{
    public string? PackId { get; init; }

    public string? Country { get; init; }

    public string? Scheme { get; init; }

    public int Version { get; init; }

    public DateOnly? EffectiveFrom { get; init; }

    public DateOnly? EffectiveTo { get; init; }

    public string? SourceUri { get; init; }

    public DateTimeOffset? FetchedAt { get; init; }

    /// <summary>
    /// Null is legal and meaningful: it says nobody has decided what this
    /// jurisdiction taxes yet, which is honest for a freshly authored pack. The
    /// pack then cannot be marked Supported until somebody does.
    /// </summary>
    public PackRulesDocument? Rules { get; init; }

    public IReadOnlyList<PackRateDocument>? Rates { get; init; }
}

/// <summary>
/// The basis flags, each optional so a reviewer can fill them one at a time, and
/// all three required before the pack may be supported.
/// </summary>
internal sealed record PackRulesDocument
{
    public bool? TradeInReducesBasis { get; init; }

    public bool? DocumentationFeeIsTaxable { get; init; }

    public bool? OtherFeesAreTaxable { get; init; }
}

internal sealed record PackRateDocument
{
    public string? JurisdictionCode { get; init; }

    public string? Description { get; init; }

    public string? RateKind { get; init; }

    public decimal Rate { get; init; }

    public string? AdministrativeArea { get; init; }

    public string? County { get; init; }

    public string? PostalKey { get; init; }

    public DateOnly? EffectiveFrom { get; init; }

    public DateOnly? EffectiveTo { get; init; }

    /// <summary>Per rate, because one pack can cite a different publication for each figure.</summary>
    public string? SourceUri { get; init; }
}

internal static class PackDocumentReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,

        // See the header: a pack carries tax numbers that need their reasoning
        // beside them, and the alternative to comments is a parallel document
        // that goes stale.
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,

        // ADR-024 R1, mechanically. A key nothing reads is a rule nothing
        // honours, and it must fail rather than load.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>
    /// Parses and validates a pack file. Returns a Draft pack with its rates and
    /// rules attached; saves nothing.
    /// </summary>
    /// <exception cref="PackFormatException">The file is not a usable pack.</exception>
    public static JurisdictionPack Read(byte[] content, DateTimeOffset loadedAt)
    {
        ArgumentNullException.ThrowIfNull(content);

        PackDocument? document;

        try
        {
            document = JsonSerializer.Deserialize<PackDocument>(content, Options);
        }
        catch (JsonException ex)
        {
            // The message names the offending key when the cause was an unknown
            // member, which is the whole value of failing here rather than later.
            throw new PackFormatException($"This pack is not readable: {ex.Message}");
        }

        if (document is null)
        {
            throw new PackFormatException("This pack is empty.");
        }

        var packId = Required(document.PackId, "packId");
        var country = Required(document.Country, "country");

        if (country.Trim().Length != 2)
        {
            throw new PackFormatException("A pack's country must be a two-letter ISO 3166-1 code.");
        }

        if (!Enum.TryParse<PackScheme>(Required(document.Scheme, "scheme"), ignoreCase: true, out var scheme))
        {
            throw new PackFormatException(
                $"'{document.Scheme}' is not a scheme this product knows. "
                + $"Use one of: {string.Join(", ", Enum.GetNames<PackScheme>())}.");
        }

        if (document.Version < 1)
        {
            throw new PackFormatException("A pack's version starts at 1 and goes up; it is never zero.");
        }

        var effectiveFrom = document.EffectiveFrom
            ?? throw new PackFormatException("A pack needs an effectiveFrom date — tax is always as at a date.");

        if (document.EffectiveTo is { } packEnd && packEnd < effectiveFrom)
        {
            throw new PackFormatException("A pack cannot stop being effective before it starts.");
        }

        var sourceUri = Required(document.SourceUri, "sourceUri");
        var fetchedAt = document.FetchedAt
            ?? throw new PackFormatException(
                "A pack needs fetchedAt: when the source was actually read. "
                + "A sourceUri nobody recorded reading is not provenance.");

        var pack = new JurisdictionPack(
            Guid.NewGuid(),
            packId.Trim(),
            country.Trim().ToUpperInvariant(),
            scheme,
            document.Version,
            effectiveFrom,
            document.EffectiveTo,
            sourceUri.Trim(),
            fetchedAt,
            HashOf(content),
            loadedAt);

        if (document.Rules is { } rules)
        {
            Declare(pack, "tradeInReducesBasis", rules.TradeInReducesBasis);
            Declare(pack, "documentationFeeIsTaxable", rules.DocumentationFeeIsTaxable);
            Declare(pack, "otherFeesAreTaxable", rules.OtherFeesAreTaxable);
        }

        foreach (var rate in document.Rates ?? [])
        {
            pack.Add(ReadRate(pack, rate, effectiveFrom, sourceUri));
        }

        return pack;
    }

    /// <summary>SHA-256, lowercase hex, over exactly the bytes that were read.</summary>
    public static string HashOf(byte[] content) =>
        Convert.ToHexStringLower(SHA256.HashData(content));

    private static JurisdictionRate ReadRate(
        JurisdictionPack pack,
        PackRateDocument rate,
        DateOnly packEffectiveFrom,
        string packSourceUri)
    {
        if (!Enum.TryParse<RateKind>(Required(rate.RateKind, "rateKind"), ignoreCase: true, out var kind))
        {
            throw new PackFormatException(
                $"'{rate.RateKind}' is not a rate kind. "
                + $"Use one of: {string.Join(", ", Enum.GetNames<RateKind>())}.");
        }

        if (rate.Rate < 0)
        {
            throw new PackFormatException("A tax rate cannot be negative.");
        }

        // A sanity bound, not a legal one. Rates are FRACTIONS here, so anything
        // at or above 1 is almost certainly a percentage somebody forgot to
        // divide — and 7 instead of 0.07 would tax a $30,000 car $210,000.
        if (rate.Rate >= 1m)
        {
            throw new PackFormatException(
                $"A rate of {rate.Rate.ToString(CultureInfo.InvariantCulture)} is not a fraction. "
                + "Seven percent is 0.07, not 7.");
        }

        var from = rate.EffectiveFrom ?? packEffectiveFrom;

        if (rate.EffectiveTo is { } to && to < from)
        {
            throw new PackFormatException("A rate cannot stop being effective before it starts.");
        }

        return new JurisdictionRate(
            Guid.NewGuid(),
            pack.Id,
            Required(rate.JurisdictionCode, "jurisdictionCode").Trim(),
            Required(rate.Description, "description").Trim(),
            kind,
            rate.Rate,
            Blank(rate.AdministrativeArea),
            Blank(rate.County),
            Blank(rate.PostalKey),
            from,
            rate.EffectiveTo,
            string.IsNullOrWhiteSpace(rate.SourceUri) ? packSourceUri.Trim() : rate.SourceUri.Trim());
    }

    private static void Declare(JurisdictionPack pack, string key, bool? value)
    {
        if (value is { } decided)
        {
            pack.Add(new JurisdictionRule(
                Guid.NewGuid(),
                pack.Id,
                key,
                decided ? "true" : "false"));
        }
    }

    private static string Required(string? value, string field) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new PackFormatException($"A pack needs {field}.")
            : value;

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
