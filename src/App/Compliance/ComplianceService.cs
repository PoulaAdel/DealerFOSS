// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ComplianceService — the dealership's half: which packs are theirs, and
//   turning a registration address into the rates that apply to it.
//
// Usage:
//   Through ICompliance (the posture) and ITaxResolution (the lookup). Deals
//   calls the lookup after it has authorised the deal.
//
// Coding Instructions:
//   WHY RESOLUTION DOES NOT CHECK A PERMISSION. It returns published tax rates —
//   that Indiana charges seven percent is not one dealership's data, and there
//   is nothing rooftop-scoped about it. The caller is DealService, which has
//   already authorised Deals.Write on the specific deal before asking, and there
//   is deliberately NO HTTP route onto ITaxResolution: the only way in is through
//   a deal endpoint that scopes first. Adding a public /compliance/resolve route
//   would create a second path with no deal to scope against, so do not.
//
//   CHANGING THE POSTURE IS ORGANIZATION-WIDE, the same reasoning as the parts
//   costing method and the second-factor policy: which rate table the dealership
//   charges tax from is not a decision one lot makes for the others.
//
//   A MISSING FLAG IS NEVER DEFAULTED. If a supported pack somehow lacks one of
//   the three basis flags, this falls back to "no pack" and the deal is typed by
//   hand. It does not assume the common-US answer. Assuming would mean quietly
//   taxing the trade-in allowance on every deal in a state that exempts it,
//   which is tax on ten thousand dollars in one direction — and it would wear a
//   pack's name while doing it. The review gate makes this unreachable; the
//   fallback is here because "unreachable" and "harmless if reached" are
//   different claims.
//
//   THE DATE IS THE SALE'S, NOT TODAY'S. A pack effective from January does not
//   govern a deal written last December, which is the same property that keeps a
//   tax line frozen (R3) seen from the other side.

using Microsoft.EntityFrameworkCore;
using DealerFOSS.Core;
using DealerFOSS.Data;
using DealerFOSS.Identity;

namespace DealerFOSS.Compliance;

internal sealed class ComplianceService(
    JurisdictionDb packs,
    TenantDb db,
    IAccessDirectory access,
    ICurrentUser currentUser,
    IAuditSink audit)
    : ICompliance, ITaxResolution
{
    private const string ConfigurePermission = Permissions.ComplianceConfigure;

    private readonly JurisdictionDb _packs = packs;
    private readonly TenantDb _db = db;
    private readonly IAccessDirectory _access = access;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditSink _audit = audit;

    public async Task<Result<TaxResolution>> ResolveAsync(
        TaxResolutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var enabled = await EnabledPackIdsAsync(cancellationToken);
        if (enabled.Count == 0)
        {
            return Result.Success(TaxResolution.None(TaxResolutionOutcome.NoPackEnabled));
        }

        var country = request.Country.Trim().ToUpperInvariant();

        var candidates = await _packs.Packs
            .AsNoTracking()
            .Where(p => p.Country == country
                && p.Status == PackStatus.Supported
                && enabled.Contains(p.PackId))
            .ToListAsync(cancellationToken);

        // Newest effective generation wins. Several versions of one pack can sit
        // in the table at once — that is R2 working as intended — and the one
        // that governs a sale is the latest whose effective window covers its
        // date, not the latest full stop.
        var pack = candidates
            .Where(p => p.IsEffectiveOn(request.On))
            .OrderByDescending(p => p.Version)
            .FirstOrDefault();

        if (pack is null)
        {
            return Result.Success(TaxResolution.None(TaxResolutionOutcome.NoPackEnabled));
        }

        if (!TryReadRules(pack, out var tradeIn, out var docFee, out var otherFees))
        {
            return Result.Success(TaxResolution.None(TaxResolutionOutcome.NoPackEnabled));
        }

        // EVERY matching row, one line each. State, county and city taxes stack
        // on one car, so a precedence rule here would silently undercharge every
        // deal in a county that levies on top of its state.
        var rates = pack.Rates
            .Where(r => r.EffectiveFrom <= request.On
                && (r.EffectiveTo is null || r.EffectiveTo >= request.On))
            .Where(r => r.Matches(request.AdministrativeArea, request.County, request.PostalCode))
            .OrderBy(r => r.Kind)
            .ThenBy(r => r.JurisdictionCode, StringComparer.Ordinal)
            .Select(r => new ResolvedRate(r.JurisdictionCode, r.Description, r.Kind.ToString(), r.Rate))
            .ToList();

        if (rates.Count == 0)
        {
            // Including a Manual pack, which has no rates by design. Such a pack
            // is a versioned, sourced statement that this jurisdiction is handled
            // by hand — so the honest answer is the same one an uncovered address
            // gets, and the screen tells the person to enter it (R5).
            return Result.Success(TaxResolution.None(TaxResolutionOutcome.NoRateForAddress));
        }

        return Result.Success(new TaxResolution(
            TaxResolutionOutcome.Resolved,
            new ResolvedTax(pack.PackId, pack.Version, tradeIn, docFee, otherFees, rates)));
    }

    public async Task<Result<CompliancePosture>> GetPostureAsync(CancellationToken cancellationToken)
    {
        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ConfigurePermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<CompliancePosture>(ComplianceErrors.Forbidden);
        }

        return Result.Success(new CompliancePosture(await EnabledPackIdsAsync(cancellationToken)));
    }

    public async Task<Result<CompliancePosture>> SetPostureAsync(
        IReadOnlyList<string> enabledPackIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(enabledPackIds);

        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ConfigurePermission, cancellationToken);
        if (!scope.IsOrganizationWide)
        {
            await DenyAsync("Attempted to change which tax packs this dealership uses.", cancellationToken);
            return Result.Failure<CompliancePosture>(ComplianceErrors.Forbidden);
        }

        var wanted = enabledPackIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var known = await _packs.Packs
            .AsNoTracking()
            .Where(p => wanted.Contains(p.PackId))
            .Select(p => new { p.PackId, p.Country })
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var id in wanted)
        {
            if (!known.Any(k => string.Equals(k.PackId, id, StringComparison.OrdinalIgnoreCase)))
            {
                return Result.Failure<CompliancePosture>(ComplianceErrors.UnknownPack(id));
            }
        }

        // One per country, refused rather than resolved by a tie-break. Two
        // enabled packs for the US would mean the answer to "what tax did we
        // charge, and why" depends on an ordering nobody chose.
        if (known.GroupBy(k => k.Country).Any(g => g.Count() > 1))
        {
            return Result.Failure<CompliancePosture>(ComplianceErrors.OnePackPerCountry);
        }

        var settings = await _db.ComplianceSettings.SingleOrDefaultAsync(
            s => s.Id == ComplianceSettings.SingletonId, cancellationToken);

        var before = settings?.EnabledPackIds ?? [];

        if (settings is null)
        {
            settings = new ComplianceSettings(wanted);
            _db.ComplianceSettings.Add(settings);
        }
        else
        {
            settings.Enable(wanted);
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Audited because it changes what every future deal is charged. Already
        // frozen tax lines are untouched (R3): switching a pack off does not
        // rewrite a deal somebody has already signed.
        await _audit.RecordAsync(
            new AuditEntry(
                _currentUser.Id, "Compliance.PostureChanged", AuditOutcome.Allowed,
                "ComplianceSettings", ComplianceSettings.SingletonId.ToString(), null,
                $"Tax packs changed from [{string.Join(", ", before)}] to [{string.Join(", ", wanted)}]. "
                    + "Applies to tax worked out from now on; tax already recorded on a deal is unchanged.",
                null, null),
            cancellationToken);

        return Result.Success(new CompliancePosture(wanted));
    }

    private async Task<List<string>> EnabledPackIdsAsync(CancellationToken cancellationToken)
    {
        var settings = await _db.ComplianceSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == ComplianceSettings.SingletonId, cancellationToken);

        return [.. settings?.EnabledPackIds ?? []];
    }

    /// <summary>
    /// Reads the three basis flags, or reports that this pack cannot say what it
    /// taxes. Never guesses one — see the file header.
    /// </summary>
    private static bool TryReadRules(
        JurisdictionPack pack,
        out bool tradeInReducesBasis,
        out bool documentationFeeIsTaxable,
        out bool otherFeesAreTaxable)
    {
        tradeInReducesBasis = false;
        documentationFeeIsTaxable = false;
        otherFeesAreTaxable = false;

        if (!TryFlag(pack, "tradeInReducesBasis", out tradeInReducesBasis)
            || !TryFlag(pack, "documentationFeeIsTaxable", out documentationFeeIsTaxable)
            || !TryFlag(pack, "otherFeesAreTaxable", out otherFeesAreTaxable))
        {
            return false;
        }

        return true;
    }

    private static bool TryFlag(JurisdictionPack pack, string key, out bool value)
    {
        value = false;

        var rule = pack.Rules.SingleOrDefault(r => string.Equals(r.RuleKey, key, StringComparison.Ordinal));
        if (rule is null)
        {
            return false;
        }

        return bool.TryParse(rule.Value, out value);
    }

    private async Task DenyAsync(string what, CancellationToken cancellationToken) =>
        await _audit.RecordAsync(
            new AuditEntry(
                _currentUser.Id, "Compliance.PostureChanged", AuditOutcome.Denied,
                "ComplianceSettings", ComplianceSettings.SingletonId.ToString(), null,
                what, null, null),
            cancellationToken);
}
