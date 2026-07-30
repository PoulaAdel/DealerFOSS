// CustomersEndpoints — the HTTP surface for customer records.
//
// Use:  mapped by CustomersModule; routes under /api/v1/customers.
//       GET  /api/v1/customers?search=smith&limit=25
//       GET  /api/v1/customers/{id}
//       POST /api/v1/customers
// Edit: keep it thin — authorize, delegate, map a Result to a status code.
//       Authorization lives in the service so background callers get it too.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenDealer360.Core;
using OpenDealer360.Customers.Contracts;

namespace OpenDealer360.Customers;

internal static class CustomersEndpoints
{
    public static void MapCustomers(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/customers").WithTags("Customers");

        group.MapGet("", SearchAsync);
        group.MapGet("/{customerId:guid}", GetAsync);
        group.MapPost("", AddAsync);
    }

    private static async Task<IResult> SearchAsync(
        ICustomerDirectory customers,
        CancellationToken cancellationToken,
        string? search = null,
        int limit = 25)
    {
        var result = await customers.SearchAsync(search, limit, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
    }

    private static async Task<IResult> GetAsync(
        Guid customerId,
        ICustomerDirectory customers,
        CancellationToken cancellationToken)
    {
        var result = await customers.GetAsync(customerId, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
    }

    private static async Task<IResult> AddAsync(
        NewCustomer request,
        ICustomerDirectory customers,
        CancellationToken cancellationToken)
    {
        var result = await customers.AddAsync(request, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/v1/customers/{result.Value.Id}", result.Value)
            : Problem(result.Error);
    }

    /// <summary>Maps a business error to RFC 7807 Problem Details (doc 06 §6).</summary>
    private static IResult Problem(Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError,
        };

        return Results.Problem(
            title: error.Code,
            detail: error.Message,
            statusCode: status,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
