// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   JurisdictionPackService — loading a pack, recording that somebody reviewed
//   it, and withdrawing one. The deployment operator's half of ADR-024.
//
// Usage:
//   Through IJurisdictionPacks, from the control-plane routes under
//   /api/v1/admin/jurisdiction-packs.
//
// Coding Instructions:
//   AUTHORISED AGAINST THE ADMINISTRATOR, NEVER AGAINST A TENANT USER. A pack is
//   host-catalog reference data shared by every dealership in the deployment, so
//   a dealership's own user marking one supported would be deciding on behalf of
//   all of them. There is deliberately no IAccessDirectory here and no TenantDb:
//   a control-plane request has no tenant, so there is nothing for this service
//   to reach into even by mistake.
//
//   THE REVIEW IS THE RECORD. ADR-024's review trigger says a pack found
//   Supported without a recorded human review would mean the status means
//   nothing — so the reviewer's email and the moment are columns on the pack
//   row, written here from ICurrentAdministrator and from the clock, never
//   accepted from the caller. A caller who could name the reviewer could name
//   somebody else.
//
//   A NEW VERSION IS A NEW ROW, NEVER AN EDIT (R2). There is no update path for
//   a rate in this file and there must not be one. The unique index on
//   (PackId, Version) is what makes that true under concurrency rather than only
//   when everyone follows the rule.

using Microsoft.EntityFrameworkCore;
using DealerFOSS.Administration;
using DealerFOSS.Core;

namespace DealerFOSS.Compliance;

internal sealed class JurisdictionPackService(
    JurisdictionDb packs,
    ICurrentAdministrator administrator,
    IClock clock)
    : IJurisdictionPacks
{
    private readonly JurisdictionDb _packs = packs;
    private readonly ICurrentAdministrator _administrator = administrator;
    private readonly IClock _clock = clock;

    public async Task<Result<IReadOnlyList<PackView>>> ListPacksAsync(CancellationToken cancellationToken)
    {
        if (!Allowed())
        {
            return Result.Failure<IReadOnlyList<PackView>>(ComplianceErrors.Forbidden);
        }

        var rows = await _packs.Packs
            .AsNoTracking()
            .OrderBy(p => p.PackId)
            .ThenByDescending(p => p.Version)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<PackView>>([.. rows.Select(Describe)]);
    }

    public async Task<Result<PackView>> LoadPackAsync(byte[] content, CancellationToken cancellationToken)
    {
        if (!Allowed())
        {
            return Result.Failure<PackView>(ComplianceErrors.Forbidden);
        }

        JurisdictionPack pack;

        try
        {
            pack = PackDocumentReader.Read(content, _clock.UtcNow);
        }
        catch (PackFormatException ex)
        {
            return Result.Failure<PackView>(ComplianceErrors.BadPack(ex.Message));
        }

        // Checked before the insert so the operator gets a sentence rather than a
        // constraint violation. The index still backs it: two operators loading
        // the same version at once get one success and one conflict, which is the
        // correct outcome and not something this check can deliver on its own.
        var alreadyThere = await _packs.Packs.AnyAsync(
            p => p.PackId == pack.PackId && p.Version == pack.Version,
            cancellationToken);

        if (alreadyThere)
        {
            return Result.Failure<PackView>(ComplianceErrors.VersionAlreadyLoaded);
        }

        _packs.Packs.Add(pack);

        try
        {
            await _packs.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result.Failure<PackView>(ComplianceErrors.VersionAlreadyLoaded);
        }

        return Result.Success(Describe(pack));
    }

    public async Task<Result<IReadOnlyList<PackView>>> LoadShippedPacksAsync(
        CancellationToken cancellationToken)
    {
        if (!Allowed())
        {
            return Result.Failure<IReadOnlyList<PackView>>(ComplianceErrors.Forbidden);
        }

        var loaded = new List<PackView>();

        foreach (var (name, content) in ShippedPacks.All())
        {
            JurisdictionPack pack;

            try
            {
                pack = PackDocumentReader.Read(content, _clock.UtcNow);
            }
            catch (PackFormatException ex)
            {
                // A shipped pack that does not parse is our bug, not the
                // operator's, so it says which file rather than only what is
                // wrong with it.
                return Result.Failure<IReadOnlyList<PackView>>(
                    ComplianceErrors.BadPack($"The pack '{name}' that ships with this build is unreadable: {ex.Message}"));
            }

            // Already loaded is not a failure here, unlike loading one by hand.
            // An operator pressing this twice, or after an upgrade that adds one
            // pack, wants the new ones and no complaint about the old ones.
            var alreadyThere = await _packs.Packs.AnyAsync(
                p => p.PackId == pack.PackId && p.Version == pack.Version,
                cancellationToken);

            if (alreadyThere)
            {
                continue;
            }

            _packs.Packs.Add(pack);
            loaded.Add(Describe(pack));
        }

        if (loaded.Count > 0)
        {
            await _packs.SaveChangesAsync(cancellationToken);
        }

        return Result.Success<IReadOnlyList<PackView>>(loaded);
    }

    public async Task<Result<PackView>> MarkReviewedAsync(
        string packId,
        int version,
        CancellationToken cancellationToken)
    {
        if (!Allowed())
        {
            return Result.Failure<PackView>(ComplianceErrors.Forbidden);
        }

        var pack = await LoadAsync(packId, version, cancellationToken);
        if (pack is null)
        {
            return Result.Failure<PackView>(ComplianceErrors.PackNotFound);
        }

        try
        {
            pack.MarkReviewed(_administrator.Email, _clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<PackView>(ComplianceErrors.NotReviewable(ex.Message));
        }

        await _packs.SaveChangesAsync(cancellationToken);
        return Result.Success(Describe(pack));
    }

    public async Task<Result<PackView>> WithdrawPackAsync(
        string packId,
        int version,
        CancellationToken cancellationToken)
    {
        if (!Allowed())
        {
            return Result.Failure<PackView>(ComplianceErrors.Forbidden);
        }

        var pack = await LoadAsync(packId, version, cancellationToken);
        if (pack is null)
        {
            return Result.Failure<PackView>(ComplianceErrors.PackNotFound);
        }

        // Withdrawn and KEPT. It stops resolving new deals and stays readable,
        // because every deal already taxed under it still has to be explainable
        // — deleting the rows would make those tax lines unauditable, which is
        // the one thing DealTaxLine exists to prevent.
        pack.Withdraw();

        await _packs.SaveChangesAsync(cancellationToken);
        return Result.Success(Describe(pack));
    }

    /// <summary>
    /// An authenticated administrator who has finished enrolling a second factor.
    /// The enrolment check matters: a half-enrolled administrator may do nothing
    /// but enrol, and blessing a tax pack is emphatically something else.
    /// </summary>
    private bool Allowed() =>
        _administrator.IsAuthenticated && !_administrator.MustEnrolSecondFactor;

    private async Task<JurisdictionPack?> LoadAsync(
        string packId,
        int version,
        CancellationToken cancellationToken) =>
        await _packs.Packs.SingleOrDefaultAsync(
            p => p.PackId == packId && p.Version == version,
            cancellationToken);

    private static PackView Describe(JurisdictionPack pack) => new(
        pack.PackId,
        pack.Country,
        pack.Scheme.ToString(),
        pack.Version,
        pack.Status.ToString(),
        pack.EffectiveFrom,
        pack.EffectiveTo,
        pack.SourceUri,
        pack.FetchedAt,
        pack.ContentHash,
        pack.ReviewedBy,
        pack.ReviewedAt,
        pack.Rates.Count,
        pack.MissingRequiredRules());
}
