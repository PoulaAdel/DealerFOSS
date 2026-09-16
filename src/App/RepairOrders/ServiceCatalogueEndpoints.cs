// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ServiceCatalogueEndpoints — the jobs the workshop sells and what an hour
//   costs.
//
// Usage:
//   GET   /api/v1/service/op-codes                what we sell, searchable
//   POST  /api/v1/service/op-codes                add one
//   POST  /api/v1/service/op-codes/{id}           revise it
//   POST  /api/v1/service/op-codes/{id}/active    withdraw or restore it
//   GET   /api/v1/service/labour-rates            what an hour costs, per lot
//   POST  /api/v1/service/labour-rates            set one
//
// Coding Instructions:
//   Keep it thin — delegate, then map a Result to a status code. Who may do
//   what is decided in ServiceCatalogueService, not here.
//
//   THERE IS DELIBERATELY NO DELETE. Withdrawing is the operation; a catalogue
//   entry a job already cites must keep reading correctly forever.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using DealerFOSS.App;
using DealerFOSS.Core;

namespace DealerFOSS.RepairOrders;

internal static class ServiceCatalogueEndpoints
{
    public static void MapServiceCatalogue(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/service").WithTags("Service");

        group.MapGet("/op-codes", ListOpCodesAsync);
        group.MapPost("/op-codes", AddOpCodeAsync);
        group.MapPost("/op-codes/{opCodeId:guid}", ReviseOpCodeAsync);
        group.MapPost("/op-codes/{opCodeId:guid}/active", SetOpCodeActiveAsync);

        group.MapGet("/labour-rates", ListRatesAsync);
        group.MapPost("/labour-rates", SetRateAsync);
    }

    private static async Task<IResult> ListOpCodesAsync(
        IServiceCatalogue catalogue,
        CancellationToken cancellationToken,
        string? search = null,
        bool activeOnly = true,
        int limit = 50,
        int offset = 0)
    {
        var result = await catalogue.ListOpCodesAsync(
            new OpCodeQuery(search, activeOnly, limit, offset), cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> AddOpCodeAsync(
        IServiceCatalogue catalogue,
        NewOpCode opCode,
        CancellationToken cancellationToken)
    {
        var result = await catalogue.AddOpCodeAsync(opCode, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/v1/service/op-codes/{result.Value.Id}", result.Value)
            : result.Error.ToProblem();
    }

    private static async Task<IResult> ReviseOpCodeAsync(
        IServiceCatalogue catalogue,
        Guid opCodeId,
        ReviseOpCode revision,
        CancellationToken cancellationToken)
    {
        var result = await catalogue.ReviseOpCodeAsync(opCodeId, revision, cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> SetOpCodeActiveAsync(
        IServiceCatalogue catalogue,
        Guid opCodeId,
        SetActive request,
        CancellationToken cancellationToken)
    {
        var result = await catalogue.SetOpCodeActiveAsync(opCodeId, request.Active, cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> ListRatesAsync(
        IServiceCatalogue catalogue,
        CancellationToken cancellationToken,
        Guid? rooftopId = null)
    {
        var result = await catalogue.ListRatesAsync(
            rooftopId is { } id ? new RooftopId(id) : null, cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> SetRateAsync(
        IServiceCatalogue catalogue,
        NewLabourRate rate,
        CancellationToken cancellationToken)
    {
        var result = await catalogue.SetRateAsync(rate, cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    /// <summary>
    /// Withdrawing and restoring share one endpoint with a body, rather than
    /// being a DELETE and a POST. Neither removes anything, so a DELETE would be
    /// describing an operation that does not happen.
    /// </summary>
    private sealed record SetActive(bool Active);
}
