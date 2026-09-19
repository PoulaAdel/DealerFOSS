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
//   PUT  /api/v1/customers/{id}/credit-limit
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
        group.MapPut("/{customerId:guid}/credit-limit", SetCreditLimitAsync);
        group.MapPut("/{customerId:guid}/address", SetAddressAsync);
    }

    private static async Task<IResult> SearchAsync(
        ICustomers customers,
        CancellationToken cancellationToken,
        string? search = null,
        int limit = 25,
        int offset = 0)
    {
        var result = await customers.SearchAsync(search, limit, offset, cancellationToken);
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

    private static async Task<IResult> SetCreditLimitAsync(
        Guid customerId,
        SetCreditLimitRequest request,
        ICustomers customers,
        CancellationToken cancellationToken)
    {
        var result = await customers.SetCreditLimitAsync(customerId, request.Limit, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> SetAddressAsync(
        Guid customerId,
        SetAddressRequest request,
        ICustomers customers,
        CancellationToken cancellationToken)
    {
        var result = await customers.SetAddressAsync(customerId, request.Address, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }
}

/// <summary>What a caller supplies to set or clear a customer's credit limit.</summary>
public sealed record SetCreditLimitRequest(decimal? Limit);

/// <summary>What a caller supplies to set or clear a customer's mailing address.</summary>
public sealed record SetAddressRequest(AddressView? Address);
