// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   RepairOrderEndpoints — the HTTP surface for workshop jobs.
//
// Usage:
//   Mapped from Program.cs; routes under /api/v1/repair-orders.
//   GET  /api/v1/repair-orders?rooftopId=&status=InProgress&vehicleId=&openOnly=true
//   GET  /api/v1/repair-orders/{id}
//   POST /api/v1/repair-orders
//   POST /api/v1/repair-orders/{id}/lines
//   DELETE /api/v1/repair-orders/{id}/lines/{lineId}
//   POST /api/v1/repair-orders/{id}/lines/{lineId}/answer
//   POST /api/v1/repair-orders/{id}/technician
//   POST /api/v1/repair-orders/{id}/status
//
// Coding Instructions:
//   Keep it thin — delegate, then map a Result to a status code. The rooftop
//   scope and the authorization rule are applied in RepairOrderService, not
//   here, so a background caller gets the same checks.

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

        // Before the {id} route: "labour" is not a Guid, so the constrained route
        // would not catch it anyway — but keeping the literal first means it stays
        // that way if the constraint is ever loosened.
        group.MapGet("/labour", LabourAsync);
        group.MapGet("/pay-type-reconciliation", PayTypeReconciliationAsync);
        group.MapGet("/{repairOrderId:guid}", GetAsync);
        group.MapPost("", OpenAsync);
        group.MapPost("/{repairOrderId:guid}/lines", AddLineAsync);
        group.MapDelete("/{repairOrderId:guid}/lines/{lineId:guid}", RemoveLineAsync);
        group.MapPost("/{repairOrderId:guid}/lines/{lineId:guid}/answer", AnswerLineAsync);
        group.MapPost("/{repairOrderId:guid}/technician", AssignTechnicianAsync);
        group.MapPost("/{repairOrderId:guid}/clock-on", ClockOnAsync);
        group.MapPost("/{repairOrderId:guid}/clock-off", ClockOffAsync);
        group.MapPost("/{repairOrderId:guid}/status", ChangeStatusAsync);
    }

    /// <summary>
    /// What the workshop sold over a period. The response names the two figures
    /// it cannot produce rather than leaving them out silently.
    /// </summary>
    private static async Task<IResult> LabourAsync(
        IRepairOrders service,
        CancellationToken cancellationToken,
        DateOnly? from = null,
        DateOnly? to = null,
        Guid? rooftopId = null)
    {
        // Defaults to the month so far, because that is what somebody opening the
        // report almost always wants and an unbounded scan is not a useful answer.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? new DateOnly(today.Year, today.Month, 1);

        var result = await service.LabourAsync(
            new LabourQuery(start, to ?? today, rooftopId is null ? null : new RooftopId(rooftopId.Value)),
            cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    /// <summary>What the workshop sold over a period, split by who pays.</summary>
    private static async Task<IResult> PayTypeReconciliationAsync(
        IRepairOrders service,
        CancellationToken cancellationToken,
        DateOnly? from = null,
        DateOnly? to = null,
        Guid? rooftopId = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? new DateOnly(today.Year, today.Month, 1);

        var result = await service.PayTypeReconciliationAsync(
            new LabourQuery(start, to ?? today, rooftopId is null ? null : new RooftopId(rooftopId.Value)),
            cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
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
        int limit = 50,
        int offset = 0)
    {
        var query = new RepairOrderQuery(
            rooftopId is null ? null : new RooftopId(rooftopId.Value),
            status,
            customerId,
            vehicleId,
            technicianUserId,
            openOnly,
            limit,
            offset);

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

    /// <summary>
    /// Puts a technician on the clock. Clocking on here stops whatever they were
    /// on elsewhere — see IRepairOrders.ClockOnAsync for why switching rather
    /// than refusing is the behaviour a workshop actually uses.
    /// </summary>
    private static async Task<IResult> ClockOnAsync(
        Guid repairOrderId,
        ClockOnRequest request,
        IRepairOrders repairOrders,
        CancellationToken cancellationToken)
    {
        var result = await repairOrders.ClockOnAsync(repairOrderId, request, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> ClockOffAsync(
        Guid repairOrderId,
        ClockOffRequest request,
        IRepairOrders repairOrders,
        CancellationToken cancellationToken)
    {
        var result = await repairOrders.ClockOffAsync(repairOrderId, request, cancellationToken);
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
