// InventoryEndpoints — the HTTP surface for stock on a lot.
//
// Use:  mapped from Program.cs; routes under /api/v1/inventory.
//       GET  /api/v1/inventory?rooftopId=&status=Available&stock=A1234
//       GET  /api/v1/inventory/{id}
//       POST /api/v1/inventory
//       POST /api/v1/inventory/{id}/status
// Edit: keep it thin — delegate, then map a Result to a status code. The rooftop
//       scope is applied in InventoryService, not here, so a background caller
//       gets the same check.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenDealer360.App;
using OpenDealer360.Core;

namespace OpenDealer360.Inventory;

internal static class InventoryEndpoints
{
    public static void MapInventory(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/inventory").WithTags("Inventory");

        group.MapGet("", ListAsync);
        group.MapGet("/{unitId:guid}", GetAsync);
        group.MapPost("", ReceiveAsync);
        group.MapPost("/{unitId:guid}/status", ChangeStatusAsync);
    }

    private static async Task<IResult> ListAsync(
        IInventory inventory,
        CancellationToken cancellationToken,
        Guid? rooftopId = null,
        string? status = null,
        string? stock = null,
        string? search = null,
        int limit = 50)
    {
        var query = new InventoryQuery(
            rooftopId is null ? null : new RooftopId(rooftopId.Value),
            status,
            stock,
            search,
            limit);

        var result = await inventory.ListAsync(query, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> GetAsync(
        Guid unitId,
        IInventory inventory,
        CancellationToken cancellationToken)
    {
        var result = await inventory.GetAsync(unitId, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> ReceiveAsync(
        NewInventoryUnit request,
        IInventory inventory,
        CancellationToken cancellationToken)
    {
        var result = await inventory.ReceiveAsync(request, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/v1/inventory/{result.Value.Id}", result.Value)
            : result.Error.ToProblem();
    }

    private static async Task<IResult> ChangeStatusAsync(
        Guid unitId,
        StatusChangeRequest request,
        IInventory inventory,
        CancellationToken cancellationToken)
    {
        var result = await inventory.ChangeStatusAsync(unitId, request, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }
}
