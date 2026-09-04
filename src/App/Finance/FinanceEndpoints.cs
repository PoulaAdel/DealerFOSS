// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   FinanceEndpoints — the F&I product catalogue.
//
// Usage:
//   GET  /api/v1/finance/products                 what can be sold
//   POST /api/v1/finance/products                 add one
//   POST /api/v1/finance/products/{id}/price      move the defaults
//   POST /api/v1/finance/products/{id}/available  withdraw or restore
//
// Coding Instructions:
//   Selling a product is not here. It happens on the deal
//   (POST /api/v1/deals/{id}/products), because the price is negotiated per
//   deal and the sale has to live or die with the deal it is on.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using DealerFOSS.App;

namespace DealerFOSS.Finance;

internal static class FinanceEndpoints
{
    public static void MapFinance(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance").WithTags("Finance");

        group.MapGet("/products", ListAsync);
        group.MapPost("/products", AddAsync);
        group.MapPost("/products/{productId:guid}/price", RepriceAsync);
        group.MapPost("/products/{productId:guid}/available", SetAvailableAsync);
    }

    private static async Task<IResult> ListAsync(
        IFinanceProducts products,
        CancellationToken cancellationToken,
        bool availableOnly = false)
    {
        var result = await products.ListAsync(availableOnly, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> AddAsync(
        NewFinanceProduct request,
        IFinanceProducts products,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await products.AddAsync(request, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/v1/finance/products/{result.Value.Id}", result.Value)
            : result.Error.ToProblem();
    }

    private static async Task<IResult> RepriceAsync(
        Guid productId,
        RepriceRequest request,
        IFinanceProducts products,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await products.RepriceAsync(
            productId, request.DefaultPrice, request.DefaultCost, cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> SetAvailableAsync(
        Guid productId,
        AvailabilityRequest request,
        IFinanceProducts products,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await products.SetAvailableAsync(productId, request.Available, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }
}

/// <summary>Moves the catalogue defaults. Deals already done keep their own figures.</summary>
internal sealed record RepriceRequest(decimal DefaultPrice, decimal DefaultCost);

internal sealed record AvailabilityRequest(bool Available);
