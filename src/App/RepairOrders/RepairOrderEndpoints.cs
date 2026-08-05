// RepairOrderEndpoints — the HTTP surface for workshop jobs.
//
// Use:  mapped from Program.cs; routes under /api/v1/repair-orders.
//       GET  /api/v1/repair-orders?rooftopId=&status=InProgress&vehicleId=&openOnly=true
//       GET  /api/v1/repair-orders/{id}
//       POST /api/v1/repair-orders
//       POST /api/v1/repair-orders/{id}/lines
//       DELETE /api/v1/repair-orders/{id}/lines/{lineId}
//       POST /api/v1/repair-orders/{id}/lines/{lineId}/answer
//       POST /api/v1/repair-orders/{id}/technician
//       POST /api/v1/repair-orders/{id}/status
// Edit: keep it thin — delegate, then map a Result to a status code. The rooftop
//       scope and the authorization rule are applied in RepairOrderService, not
//       here, so a background caller gets the same checks.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using DealerFOSS.App;
using DealerFOSS.Core;

namespace DealerFOSS.RepairOrders;

internal static class RepairOrderEndpoints
{
    public static void MapRepairOrders(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/repair-orders").WithTags("Service");

        group.MapGet("", ListAsync);
        group.MapGet("/{repairOrderId:guid}", GetAsync);
        group.MapPost("", OpenAsync);
        group.MapPost("/{repairOrderId:guid}/lines", AddLineAsync);
        group.MapDelete("/{repairOrderId:guid}/lines/{lineId:guid}", RemoveLineAsync);
        group.MapPost("/{repairOrderId:guid}/lines/{lineId:guid}/answer", AnswerLineAsync);
        group.MapPost("/{repairOrderId:guid}/technician", AssignTechnicianAsync);
        group.MapPost("/{repairOrderId:guid}/status", ChangeStatusAsync);
    }

    private static async Task<IResult> ListAsync(
        IRepairOrders repairOrders,
        CancellationToken cancellationToken,
        Guid? rooftopId = null,
        string? status = null,
        Guid? customerId = null,
        Guid? vehicleId = null,
        Guid? technicianUserId = null,
        bool openOnly = false,
        int limit = 50)
    {
        var query = new RepairOrderQuery(
            rooftopId is null ? null : new RooftopId(rooftopId.Value),
            status,
            customerId,
            vehicleId,
            technicianUserId,
            openOnly,
            limit);

        var result = await repairOrders.ListAsync(query, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> GetAsync(
        Guid repairOrderId,
        IRepairOrders repairOrders,
        CancellationToken cancellationToken)
    {
        var result = await repairOrders.GetAsync(repairOrderId, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> OpenAsync(
        NewRepairOrder request,
        IRepairOrders repairOrders,
        CancellationToken cancellationToken)
    {
        var result = await repairOrders.OpenAsync(request, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/v1/repair-orders/{result.Value.Id}", result.Value)
            : result.Error.ToProblem();
    }

    private static async Task<IResult> AddLineAsync(
        Guid repairOrderId,
        NewServiceLine request,
        IRepairOrders repairOrders,
        CancellationToken cancellationToken)
    {
        var result = await repairOrders.AddLineAsync(repairOrderId, request, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> RemoveLineAsync(
        Guid repairOrderId,
        Guid lineId,
        IRepairOrders repairOrders,
        CancellationToken cancellationToken)
    {
        var result = await repairOrders.RemoveLineAsync(repairOrderId, lineId, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> AnswerLineAsync(
        Guid repairOrderId,
        Guid lineId,
        LineAnswerRequest request,
        IRepairOrders repairOrders,
        CancellationToken cancellationToken)
    {
        var result = await repairOrders.AnswerLineAsync(repairOrderId, lineId, request, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> AssignTechnicianAsync(
        Guid repairOrderId,
        AssignTechnicianRequest request,
        IRepairOrders repairOrders,
        CancellationToken cancellationToken)
    {
        var result = await repairOrders.AssignTechnicianAsync(repairOrderId, request, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> ChangeStatusAsync(
        Guid repairOrderId,
        RepairOrderStatusChangeRequest request,
        IRepairOrders repairOrders,
        CancellationToken cancellationToken)
    {
        var result = await repairOrders.ChangeStatusAsync(repairOrderId, request, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }
}
