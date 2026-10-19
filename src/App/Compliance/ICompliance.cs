// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ICompliance and ITaxResolution — the whole public surface of this
//   capability, plus the views that cross it and the errors it returns.
//
// Usage:
//   Deals depends on ITaxResolution and on nothing else here.
//   ICompliance is the operator surface: load a pack, record a review, withdraw
//   one, and choose which packs this dealership uses.
//
// Coding Instructions:
//   TWO INTERFACES ON PURPOSE. Deals needs to resolve a rate and must not be
//   able to load a pack or mark one supported — those are a deployment
//   operator's acts, and a feature that could do them from a deal screen would
//   make "reviewed by a person" reachable from a sales workflow. Splitting the
//   interface is how that stays structurally true rather than merely unlikely.
//
//   NOTHING HERE EXPOSES AN ENTITY. JurisdictionPack and its rows are internal,
//   so Deals cannot hold one even by accident — asserted by
//   FeatureBoundaryTests. The views below are flat records carrying the figures
//   and the provenance, which is all a tax line needs.
//
//   NO PACK IS NOT AN ERROR (ADR-024 R5). That is why resolution answers with an
//   OUTCOME rather than a success-or-failure: "nothing is enabled for this
//   country" and "no rate covers this address" are ordinary answers that a
//   screen reports and a person works around by typing the tax. Returning a
//   failure would have made an unsupported jurisdiction a dead end, which is the
//   one thing this design exists to avoid.
//
//   THE RESOLUTION DOES NOT RETURN TaxBasisRules. It returns the three flags as
//   plain booleans, and Deals assembles its own TaxBasisRules from them. That
//   keeps the dependency one-way — Deals knows about Compliance, Compliance
//   knows nothing about Deals — so the two features cannot become a cycle.

using DealerFOSS.Core;

namespace DealerFOSS.Compliance;

/// <summary>What happened when an address was looked up. None of these is a fault.</summary>
public enum TaxResolutionOutcome
{
    /// <summary>A supported, enabled pack covers this address, and <c>Tax</c> says what it charges.</summary>
    Resolved = 0,

    /// <summary>
    /// The dealership has not enabled a pack for this country, or the one it
    /// enabled is not supported yet. A person enters the tax (R5).
    /// </summary>
    NoPackEnabled = 1,

    /// <summary>
    /// A pack is enabled and it has nothing for this address — a state it does
    /// not cover, or a date outside its effective range. A person enters the tax.
    /// </summary>
    NoRateForAddress = 2,

    /// <summary>
    /// The deal has no registration address, so there is nothing to resolve
    /// from. Tax follows where the car will be registered (ADR-024), never the
    /// rooftop and never an IP address, so this is the honest answer rather than
    /// a guess.
    /// </summary>
    NoAddress = 3,
}

/// <summary>One rate that applies, ready to become a tax line.</summary>
public sealed record ResolvedRate(
    string JurisdictionCode,
    string Description,
    string Kind,
    decimal Rate);

/// <summary>
/// What a pack says about one address: what the sale is taxed on, what rates
/// apply, and which pack generation said so.
/// </summary>
public sealed record ResolvedTax(
    string PackId,
    int PackVersion,
    bool TradeInReducesBasis,
    bool DocumentationFeeIsTaxable,
    bool OtherFeesAreTaxable,
    IReadOnlyList<ResolvedRate> Rates);

/// <summary>The answer, with its outcome. <c>Tax</c> is set only when resolved.</summary>
public sealed record TaxResolution(TaxResolutionOutcome Outcome, ResolvedTax? Tax)
{
    public static TaxResolution None(TaxResolutionOutcome outcome) => new(outcome, null);
}

/// <summary>The address a rate is resolved from, as at a date.</summary>
/// <remarks>
/// State AND county separately, because a US rate depends on both. The date is
/// the sale's date, not today's: a pack effective from January does not govern a
/// deal written in December.
/// </remarks>
public sealed record TaxResolutionRequest(
    string Country,
    string? AdministrativeArea,
    string? County,
    string? PostalCode,
    DateOnly On);

/// <summary>
/// Turning an address into rates. The only part of this capability Deals can
/// reach.
/// </summary>
public interface ITaxResolution
{
    Task<Result<TaxResolution>> ResolveAsync(
        TaxResolutionRequest request,
        CancellationToken cancellationToken);
}

/// <summary>One loaded pack generation, as an operator sees it.</summary>
public sealed record PackView(
    string PackId,
    string Country,
    string Scheme,
    int Version,
    string Status,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string SourceUri,
    DateTimeOffset FetchedAt,
    string ContentHash,
    string? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    int RateCount,

    /// <summary>
    /// The basis flags still undecided. While this is non-empty the pack cannot
    /// be marked supported, and the screen says which ones are missing rather
    /// than refusing without explanation.
    /// </summary>
    IReadOnlyList<string> MissingRules);

/// <summary>Which packs this dealership uses. One row per tenant.</summary>
public sealed record CompliancePosture(IReadOnlyList<string> EnabledPackIds);

/// <summary>
/// Pack administration, which is a DEPLOYMENT OPERATOR's job and not a
/// dealership's.
/// </summary>
/// <remarks>
/// Separate from <see cref="ICompliance"/> because the two answer to different
/// callers and neither can stand in for the other. Packs are host-catalog
/// reference data shared by every tenant, so one dealership's user marking a
/// pack supported would be deciding for all of them — it is authorised against
/// <c>ICurrentAdministrator</c>, on a control-plane route, where no tenant is
/// resolved at all. The posture next door is the opposite: ordinary tenant data,
/// authorised against the tenant's own permission catalogue. One service cannot
/// hold both, because a control-plane request has no tenant connection to open.
/// </remarks>
public interface IJurisdictionPacks
{
    Task<Result<IReadOnlyList<PackView>>> ListPacksAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Loads a pack file as a new generation. Never overwrites an existing
    /// version — that is ADR-024 R2, and the reason an audit next year can still
    /// read last year's rate.
    /// </summary>
    Task<Result<PackView>> LoadPackAsync(byte[] content, CancellationToken cancellationToken);

    /// <summary>
    /// Loads the packs built into this build, skipping any version already
    /// present. They arrive Draft like any other, so this charges nobody
    /// anything until a person reviews one and a dealership enables it.
    /// </summary>
    Task<Result<IReadOnlyList<PackView>>> LoadShippedPacksAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Records that a named person reviewed this exact pack content. Refuses
    /// while any basis flag is still undecided.
    /// </summary>
    Task<Result<PackView>> MarkReviewedAsync(
        string packId,
        int version,
        CancellationToken cancellationToken);

    Task<Result<PackView>> WithdrawPackAsync(
        string packId,
        int version,
        CancellationToken cancellationToken);
}

/// <summary>
/// The dealership's own choice: which of the supported packs are theirs.
/// Ordinary tenant data, held behind <c>Compliance.Configure</c>.
/// </summary>
public interface ICompliance
{
    Task<Result<CompliancePosture>> GetPostureAsync(CancellationToken cancellationToken);

    Task<Result<CompliancePosture>> SetPostureAsync(
        IReadOnlyList<string> enabledPackIds,
        CancellationToken cancellationToken);
}

/// <summary>The refusals this capability returns, each saying what to do about it.</summary>
public static class ComplianceErrors
{
    public static readonly Error Forbidden =
        Error.Forbidden("compliance.forbidden", "You cannot change this deployment's tax packs.");

    public static readonly Error PackNotFound =
        Error.NotFound("compliance.pack_not_found", "There is no pack with that id and version.");

    public static Error BadPack(string detail) =>
        Error.Validation("compliance.pack_unreadable", detail);

    public static Error NotReviewable(string detail) =>
        Error.Conflict("compliance.pack_not_reviewable", detail);

    public static readonly Error VersionAlreadyLoaded =
        Error.Conflict(
            "compliance.version_already_loaded",
            "That pack version is already loaded. A change is a new version, never an edit to this one.");

    public static readonly Error OnePackPerCountry =
        Error.Validation(
            "compliance.one_pack_per_country",
            "Only one pack can be enabled per country, so there is never a question which rate applied.");

    public static Error UnknownPack(string packId) =>
        Error.Validation("compliance.unknown_pack", $"There is no pack called '{packId}' to enable.");
}
