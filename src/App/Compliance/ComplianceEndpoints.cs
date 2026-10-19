// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ComplianceEndpoints — the HTTP surface for jurisdiction packs and for the
//   dealership's choice of which to use.
//
// Usage:
//   Mapped from Program.cs.
//   GET  /api/v1/admin/jurisdiction-packs
//   POST /api/v1/admin/jurisdiction-packs                      (the pack file as the body)
//   POST /api/v1/admin/jurisdiction-packs/{packId}/{version}/review
//   POST /api/v1/admin/jurisdiction-packs/{packId}/{version}/withdraw
//   GET  /api/v1/compliance/posture
//   PUT  /api/v1/compliance/posture
//
// Coding Instructions:
//   TWO GROUPS, TWO CALLERS, AND THE SPLIT IS THE SECURITY PROPERTY. The pack
//   routes sit under /api/v1/admin, which AdministratorMiddleware covers for
//   every path and only those — so they are authorised as a deployment operator
//   and no tenant is resolved on them at all. The posture route is an ordinary
//   tenant route behind Compliance.Configure. Moving a pack route out from under
//   the admin prefix would silently drop its authorisation, because the
//   middleware matches on the prefix.
//
//   THERE IS NO ROUTE ONTO ITaxResolution, deliberately. Resolving tax happens
//   through POST /deals/{id}/tax/from-pack, which authorises the specific deal
//   first. A /compliance/resolve route would be a second path to the same data
//   with no deal to scope against, so do not add one for convenience.
//
//   THE PACK FILE ARRIVES AS RAW BYTES, not as a parsed model. The content hash
//   that a reviewer's approval is tied to has to be over exactly what was sent,
//   so the bytes must not be deserialised and re-serialised on the way in — that
//   would hash a round-trip of the file rather than the file.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using DealerFOSS.App;

namespace DealerFOSS.Compliance;

internal static class ComplianceEndpoints
{
    /// <summary>A pack file is small. This is a limit on nonsense, not on packs.</summary>
    private const int MaxPackBytes = 8 * 1024 * 1024;

    public static void MapCompliance(this IEndpointRouteBuilder app)
    {
        // Grouped on the admin prefix rather than on the packs path, matching
        // AdminEndpoints: a group whose own route is the whole path has to map ""
        // for the collection, and that produces a trailing slash the rest of the
        // operator console does not have.
        var packs = app.MapGroup("/api/v1/admin").WithTags("Jurisdiction packs");

        packs.MapGet("/jurisdiction-packs", ListPacksAsync);
        packs.MapPost("/jurisdiction-packs", LoadPackAsync);
        packs.MapPost("/jurisdiction-packs/shipped", LoadShippedPacksAsync);
        packs.MapPost("/jurisdiction-packs/{packId}/{version:int}/review", ReviewPackAsync);
        packs.MapPost("/jurisdiction-packs/{packId}/{version:int}/withdraw", WithdrawPackAsync);

        var posture = app.MapGroup("/api/v1/compliance").WithTags("Compliance");

        posture.MapGet("/posture", GetPostureAsync);
        posture.MapPut("/posture", SetPostureAsync);
    }

    private static async Task<IResult> ListPacksAsync(
        IJurisdictionPacks packs,
        CancellationToken cancellationToken)
    {
        var result = await packs.ListPacksAsync(cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> LoadPackAsync(
        HttpRequest request,
        IJurisdictionPacks packs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, cancellationToken);

        if (buffer.Length == 0)
        {
            return ComplianceErrors.BadPack("The request carried no pack file.").ToProblem();
        }

        if (buffer.Length > MaxPackBytes)
        {
            return ComplianceErrors
                .BadPack($"A pack file must be under {MaxPackBytes / (1024 * 1024)} MB.")
                .ToProblem();
        }

        var result = await packs.LoadPackAsync(buffer.ToArray(), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> LoadShippedPacksAsync(
        IJurisdictionPacks packs,
        CancellationToken cancellationToken)
    {
        var result = await packs.LoadShippedPacksAsync(cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> ReviewPackAsync(
        string packId,
        int version,
        IJurisdictionPacks packs,
        CancellationToken cancellationToken)
    {
        var result = await packs.MarkReviewedAsync(packId, version, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> WithdrawPackAsync(
        string packId,
        int version,
        IJurisdictionPacks packs,
        CancellationToken cancellationToken)
    {
        var result = await packs.WithdrawPackAsync(packId, version, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> GetPostureAsync(
        ICompliance compliance,
        CancellationToken cancellationToken)
    {
        var result = await compliance.GetPostureAsync(cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> SetPostureAsync(
        SetPostureRequest request,
        ICompliance compliance,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await compliance.SetPostureAsync(
            request.EnabledPackIds ?? [], cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }
}

/// <summary>
/// Which packs the dealership uses. Sending an empty list turns every pack off,
/// which is a legitimate choice and means tax is entered by a person.
/// </summary>
internal sealed record SetPostureRequest(IReadOnlyList<string>? EnabledPackIds);
