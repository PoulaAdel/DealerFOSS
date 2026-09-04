// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   CustomerEndpoints — the HTTP surface for customer records.
//
// Usage:
//   Mapped from Program.cs; routes under /api/v1/customers.
//   GET  /api/v1/customers?search=smith&limit=25
//   GET  /api/v1/customers/{id}
//   POST /api/v1/customers
//
// Coding Instructions:
//   Keep it thin — delegate, then map a Result to a status code.
//   Authorization lives in CustomerService so background callers get it too.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using DealerFOSS.App;

namespace DealerFOSS.Customers;

internal static class CustomerEndpoints
{
    public static void MapCustomers(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/customers").WithTags("Customers");

        group.MapGet("", SearchAsync);
        group.MapGet("/{customerId:guid}", GetAsync);
        group.MapPost("", AddAsync);
    }

    private static async Task<IResult> SearchAsync(
        ICustomers customers,
        CancellationToken cancellationToken,
        string? search = null,
        int limit = 25)
    {
        var result = await customers.SearchAsync(search, limit, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> GetAsync(
        Guid customerId,
        ICustomers customers,
        CancellationToken cancellationToken)
    {
        var result = await customers.GetAsync(customerId, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> AddAsync(
        NewCustomer request,
        ICustomers customers,
        CancellationToken cancellationToken)
    {
        var result = await customers.AddAsync(request, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/v1/customers/{result.Value.Id}", result.Value)
            : result.Error.ToProblem();
    }
}
