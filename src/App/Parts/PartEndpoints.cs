// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   PartEndpoints — the parts catalogue, the stock on each shelf, and how it is costed.
//
// Usage:
//   GET  /api/v1/parts                    the catalogue with stock at my rooftops
//   GET  /api/v1/parts/costing            the current costing method and the choices
//   POST /api/v1/parts/costing            change it (organization-wide only)
//   GET  /api/v1/parts/{id}               one part, its stock, and its layers
//   POST /api/v1/parts                    add to the catalogue (organization-wide only)
//   POST /api/v1/parts/{id}/receipts      book a delivery onto a rooftop's shelf
//
// Coding Instructions:
//   /costing sits before /{id:guid} only by convention — the route constraint
//   already keeps them apart. There is deliberately no endpoint that issues
//   stock: parts leave the shelf when a repair order is invoiced, inside that
//   transaction, and never on their own.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using DealerFOSS.App;
using DealerFOSS.Core;

namespace DealerFOSS.Parts;

internal static class PartEndpoints
{
    public static void MapParts(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/parts").WithTags("Parts");

        group.MapGet("", ListAsync);
        group.MapGet("/costing", GetCostingAsync);
        group.MapPost("/costing", SetCostingAsync);
        group.MapGet("/{partId:guid}", GetAsync);
        group.MapPost("", AddAsync);
        group.MapPost("/{partId:guid}/receipts", ReceiveAsync);
    }

    private static async Task<IResult> ListAsync(
        IParts parts,
        Guid? rooftopId,
        string? search,
        bool? inStockOnly,
        int? limit,
        int? offset,
        CancellationToken cancellationToken)
    {
        var result = await parts.ListAsync(
            new PartQuery(
                rooftopId is { } id ? new RooftopId(id) : null,
                search,
                inStockOnly ?? false,
                limit ?? 100,
                offset ?? 0),
            cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> GetAsync(Guid partId, IParts parts, CancellationToken cancellationToken)
    {
        var result = await parts.GetAsync(partId, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> AddAsync(
        NewPart request,
        IParts parts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await parts.AddAsync(request, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/v1/parts/{result.Value.Id}", result.Value)
            : result.Error.ToProblem();
    }

    private static async Task<IResult> ReceiveAsync(
        Guid partId,
        ReceiveStockRequest request,
        IParts parts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await parts.ReceiveAsync(
            partId,
            new StockDelivery(
                request.Quantity,
                request.UnitCost,
                new RooftopId(request.RooftopId),
                request.Currency ?? "USD",
                request.Reference),
            cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> GetCostingAsync(IParts parts, CancellationToken cancellationToken)
    {
        var result = await parts.GetCostingMethodAsync(cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> SetCostingAsync(
        CostingMethodRequest request,
        IParts parts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.TryParse<PartsCostingMethod>(request.Method, ignoreCase: true, out var method))
        {
            return Error.Validation(
                    "parts.unknown_costing_method",
                    $"'{request.Method}' is not a costing method this system knows.")
                .ToProblem();
        }

        var result = await parts.SetCostingMethodAsync(method, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }
}

internal sealed record ReceiveStockRequest(
    decimal Quantity,
    decimal UnitCost,
    Guid RooftopId,
    string? Currency = null,
    string? Reference = null);

internal sealed record CostingMethodRequest(string Method);
