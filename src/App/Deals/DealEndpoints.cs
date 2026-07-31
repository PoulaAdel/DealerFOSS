// DealEndpoints — the HTTP surface for deals.
//
// Use:  mapped from Program.cs; routes under /api/v1/deals.
//       GET  /api/v1/deals?rooftopId=&status=Submitted&openOnly=true
//       GET  /api/v1/deals/{id}
//       POST /api/v1/deals
//       POST /api/v1/deals/{id}/terms
//       POST /api/v1/deals/{id}/status
// Edit: approving goes through the same status endpoint as everything else. The
//       separate permission is checked in DealService, where a background caller
//       gets it too — not here, where only HTTP would.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenDealer360.App;
using OpenDealer360.Core;

namespace OpenDealer360.Deals;

internal static class DealEndpoints
{
    public static void MapDeals(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/deals").WithTags("Deals");

        group.MapGet("", ListAsync);
        group.MapGet("/{dealId:guid}", GetAsync);
        group.MapPost("", StartAsync);
        group.MapPost("/{dealId:guid}/terms", SetTermsAsync);
        group.MapPost("/{dealId:guid}/status", ChangeStatusAsync);
    }

    private static async Task<IResult> ListAsync(
        IDeals deals,
        CancellationToken cancellationToken,
        Guid? rooftopId = null,
        string? status = null,
        Guid? customerId = null,
        Guid? salesperson = null,
        bool openOnly = false,
        int limit = 50)
    {
        var query = new DealQuery(
            rooftopId is null ? null : new RooftopId(rooftopId.Value),
            status,
            customerId,
            salesperson,
            openOnly,
            limit);

        var result = await deals.ListAsync(query, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> GetAsync(
        Guid dealId,
        IDeals deals,
        CancellationToken cancellationToken)
    {
        var result = await deals.GetAsync(dealId, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> StartAsync(
        NewDeal request,
        IDeals deals,
        CancellationToken cancellationToken)
    {
        var result = await deals.StartAsync(request, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/v1/deals/{result.Value.Id}", result.Value)
            : result.Error.ToProblem();
    }

    private static async Task<IResult> SetTermsAsync(
        Guid dealId,
        DealTerms request,
        IDeals deals,
        CancellationToken cancellationToken)
    {
        var result = await deals.SetTermsAsync(dealId, request, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> ChangeStatusAsync(
        Guid dealId,
        DealStatusChangeRequest request,
        IDeals deals,
        CancellationToken cancellationToken)
    {
        var result = await deals.ChangeStatusAsync(dealId, request, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }
}
