// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   AppointmentEndpoints — the HTTP surface for the service diary.
//
// Usage:
//   Mapped from Program.cs; routes under /api/v1/appointments.
//   GET  /api/v1/appointments?rooftopId=&from=2026-08-10&to=2026-08-16&openOnly=true
//   GET  /api/v1/appointments/{id}
//   POST /api/v1/appointments
//   POST /api/v1/appointments/{id}/reschedule
//   POST /api/v1/appointments/{id}/arrive     → opens the job and links it
//   POST /api/v1/appointments/{id}/close      → no-show or cancelled
//
// Coding Instructions:
//   Keep it thin — delegate, then map a Result to a status code. The rooftop
//   scope and the arrival transaction live in AppointmentService, not here, so
//   a background caller gets the same checks.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using DealerFOSS.App;
using DealerFOSS.Core;

namespace DealerFOSS.RepairOrders;

internal static class AppointmentEndpoints
{
    public static void MapAppointments(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/appointments").WithTags("Service");

        group.MapGet("", ListAsync);

        // Before the {id} route, so "vehicles-seen" is not offered to the guid
        // constraint as a candidate id.
        group.MapGet("/vehicles-seen", VehiclesSeenAsync);
        group.MapGet("/{appointmentId:guid}", GetAsync);
        group.MapPost("", BookAsync);
        group.MapPost("/{appointmentId:guid}/reschedule", RescheduleAsync);
        group.MapPost("/{appointmentId:guid}/arrive", ArriveAsync);
        group.MapPost("/{appointmentId:guid}/close", CloseAsync);
    }

    private static async Task<IResult> ListAsync(
        IAppointments appointments,
        CancellationToken cancellationToken,
        Guid? rooftopId = null,
        DateOnly? from = null,
        DateOnly? to = null,
        string? status = null,
        Guid? customerId = null,
        Guid? vehicleId = null,
        bool openOnly = false,
        int limit = 200)
    {
        var query = new AppointmentQuery(
            rooftopId is null ? null : new RooftopId(rooftopId.Value),
            from,
            to,
            status,
            customerId,
            vehicleId,
            openOnly,
            limit);

        var result = await appointments.ListAsync(query, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    /// <summary>
    /// The cars this customer has been here with before. A shortlist for the
    /// booking screen, not a search: it is offered beside one.
    /// </summary>
    private static async Task<IResult> VehiclesSeenAsync(
        IAppointments appointments,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var result = await appointments.VehiclesSeenForAsync(customerId, cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> GetAsync(
        Guid appointmentId,
        IAppointments appointments,
        CancellationToken cancellationToken)
    {
        var result = await appointments.GetAsync(appointmentId, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> BookAsync(
        NewAppointment request,
        IAppointments appointments,
        CancellationToken cancellationToken)
    {
        var result = await appointments.BookAsync(request, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/v1/appointments/{result.Value.Id}", result.Value)
            : result.Error.ToProblem();
    }

    private static async Task<IResult> RescheduleAsync(
        Guid appointmentId,
        RescheduleRequest request,
        IAppointments appointments,
        CancellationToken cancellationToken)
    {
        var result = await appointments.RescheduleAsync(appointmentId, request, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> ArriveAsync(
        Guid appointmentId,
        ArrivalRequest request,
        IAppointments appointments,
        CancellationToken cancellationToken)
    {
        var result = await appointments.ArriveAsync(appointmentId, request, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> CloseAsync(
        Guid appointmentId,
        CloseAppointmentRequest request,
        IAppointments appointments,
        CancellationToken cancellationToken)
    {
        var result = await appointments.CloseAsync(appointmentId, request, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }
}
