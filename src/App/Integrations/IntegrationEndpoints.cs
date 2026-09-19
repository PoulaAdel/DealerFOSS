// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   IntegrationEndpoints — the edge, over HTTP.
//
//   GET  /api/v1/integrations/connectors            what ships, and how far proven
//   GET  /api/v1/integrations/quarantine            what would not apply
//   POST /api/v1/integrations/quarantine/{id}/replay   run it again for real
//   POST /api/v1/integrations/quarantine/{id}/dismiss  it will never apply, and why
//
// Usage:
//   Mapped in Program.cs alongside the other capability endpoint groups.
//
// Coding Instructions:
//   THIS FILE DID NOT EXIST UNTIL 2026-09-19, and its absence was two of the
//   five unmet stage-2 exit criteria at once. CertificationStatus had four
//   levels and the shipped connector correctly declared FixtureTested, with no
//   route by which a reader could ever learn it. Quarantined records could be
//   resolved in code and by nothing else.
//
//   THE PAYLOAD IS NEVER RETURNED. A held record's fields are a customer's
//   name, address and telephone number exactly as a provider sent them
//   (ADR-022). The list gives the reason and the provider's id; replay reads
//   the payload server-side and it stays there. A quarantine screen that
//   listed the field values would be a personal-data export with a review
//   queue painted on it.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using DealerFOSS.App;
using DealerFOSS.Core;

namespace DealerFOSS.Integrations;

internal static class IntegrationEndpoints
{
    public static void MapIntegrations(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/integrations").WithTags("Integrations");

        group.MapGet("/connectors", ConnectorsAsync);
        group.MapGet("/quarantine", QuarantineAsync);
        group.MapPost("/quarantine/{id:guid}/replay", ReplayAsync);
        group.MapPost("/quarantine/{id:guid}/dismiss", DismissAsync);
    }

    private static async Task<IResult> ConnectorsAsync(
        IIntegrations integrations,
        CancellationToken cancellationToken)
    {
        var result = await integrations.ConnectorsAsync(cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> QuarantineAsync(
        Guid? rooftopId,
        IIntegrations integrations,
        CancellationToken cancellationToken)
    {
        var result = await integrations.QuarantineAsync(
            rooftopId is { } id ? new RooftopId(id) : null, cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> ReplayAsync(
        Guid id,
        IIntegrations integrations,
        CancellationToken cancellationToken)
    {
        var result = await integrations.ReplayAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        // 200 either way, including when the record was refused again — the
        // request succeeded, and what it found out is the body. A 409 for a
        // second refusal would make "we ran it and it still does not fit" look
        // like a failure of the replay rather than its answer.
        return Results.Ok(result.Value);
    }

    private static async Task<IResult> DismissAsync(
        Guid id,
        DismissRequest request,
        IIntegrations integrations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await integrations.DismissAsync(id, request.Note, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
    }

    /// <summary>Why this record will never apply. Required — see IIntegrations.</summary>
    internal sealed record DismissRequest(string Note);
}
