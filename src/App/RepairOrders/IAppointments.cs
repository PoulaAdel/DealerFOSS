// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   IAppointments — what the rest of the application may call to reach the diary.
//
// Usage:
//   Inject IAppointments. Nothing outside this folder touches Appointment or
//   the service schema (ADR-014).
//
// Coding Instructions:
//   The list returns the day's LOAD alongside the bookings, in one call. That
//   is not convenience packaging — the only question a service manager asks a
//   diary is "can I fit this in", and answering it from two round trips means
//   a screen that can show a day and its capacity disagreeing.

using DealerFOSS.Core;
using DealerFOSS.Vehicles;

namespace DealerFOSS.RepairOrders;

public interface IAppointments
{
    /// <summary>The diary for a stretch of days, with what each day is carrying.</summary>
    Task<Result<Diary>> ListAsync(AppointmentQuery query, CancellationToken cancellationToken);

    Task<Result<AppointmentView>> GetAsync(Guid appointmentId, CancellationToken cancellationToken);

    /// <summary>Takes a booking against a customer and their own car.</summary>
    Task<Result<AppointmentView>> BookAsync(NewAppointment booking, CancellationToken cancellationToken);

    /// <summary>Moves a booking, or re-estimates it.</summary>
    Task<Result<AppointmentView>> RescheduleAsync(
        Guid appointmentId,
        RescheduleRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// The car is here. Opens the repair order and links it to this booking, in
    /// one transaction — so the diary can never show an arrival with no job, nor a
    /// job the diary has lost track of.
    /// </summary>
    Task<Result<ArrivalResult>> ArriveAsync(
        Guid appointmentId,
        ArrivalRequest request,
        CancellationToken cancellationToken);

    /// <summary>Records that the car did not come, and which kind of not-coming.</summary>
    Task<Result<AppointmentView>> CloseAsync(
        Guid appointmentId,
        CloseAppointmentRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// The cars this customer has already been here with — every vehicle on one
    /// of their repair orders or bookings, most recent first.
    /// </summary>
    /// <remarks>
    /// <b>A vehicle has no owner in this system, and that is not an oversight to
    /// route around here.</b> Cars change hands; ownership is a history with
    /// dates, not a column, and inventing a single <c>CustomerId</c> on
    /// <see cref="DealerFOSS.Vehicles.Vehicle"/> would be wrong the first time
    /// somebody sold their car privately. So this answers the narrower question
    /// the booking screen actually asks — "which cars have we seen this person
    /// with?" — from the workshop's own records, and nothing is stored.
    ///
    /// It is therefore INCOMPLETE ON PURPOSE. A car they bought from us and have
    /// never had serviced does not appear, because deals are another capability
    /// and this one may not read their rows (ADR-017). That is why this is a
    /// shortlist offered ALONGSIDE a search of every vehicle, never instead of
    /// one: a picker that can only offer known cars cannot book a new customer's
    /// car at all.
    ///
    /// Bounded by what a person owns rather than by paging. A customer with
    /// forty cars is a fleet, and the cap is the honest end of the list.
    /// </remarks>
    Task<Result<IReadOnlyList<VehicleSummary>>> VehiclesSeenForAsync(
        Guid customerId,
        CancellationToken cancellationToken);
}

/// <summary>Which bookings a diary covers. Every filter narrows; none widens.</summary>
public sealed record AppointmentQuery(
    RooftopId? RooftopId = null,

    /// <summary>Inclusive, in the workshop's own dates rather than instants.</summary>
    DateOnly? From = null,
    DateOnly? To = null,
    string? Status = null,
    Guid? CustomerId = null,
    Guid? VehicleId = null,

    /// <summary>Only bookings still expected — the working view of a diary.</summary>
    bool OpenOnly = false,
    int Limit = 200);

/// <summary>The bookings, and what each day they fall on is carrying.</summary>
public sealed record Diary(
    IReadOnlyList<AppointmentView> Appointments,
    IReadOnlyList<DiaryDay> Load);

/// <summary>
/// One day's commitment. <paramref name="BookedHours"/> counts only bookings still
/// expected: once a car arrives its hours belong to the job, and counting both
/// would show a workshop twice as busy as it is.
/// </summary>
/// <remarks>
/// <paramref name="Unestimated"/> is reported separately rather than folded into
/// the hours as a zero. A day carrying one car nobody has estimated is not an
/// empty day, and "0 h of work" is exactly how a service manager would come to
/// think it was — the one figure the diary exists to give them, reading the wrong
/// way round.
/// </remarks>
public sealed record DiaryDay(DateOnly Date, int Expected, decimal BookedHours, int Unestimated);

public sealed record AppointmentView(
    Guid Id,
    RooftopId RooftopId,
    DateTimeOffset ScheduledFor,
    decimal? EstimatedHours,
    string Status,
    Guid CustomerId,
    string CustomerName,
    Guid VehicleId,
    string Vehicle,
    string Reason,
    Guid? AdvisorUserId,

    /// <summary>The job this became, and its number — the reconciliation link.</summary>
    Guid? RepairOrderId,
    string? RepairOrderNumber,
    DateTimeOffset? ArrivedAt,
    string? Outcome,

    /// <summary>Whether this booking can still be moved, arrived, or closed.</summary>
    bool IsOpen);

/// <summary>
/// What arriving produced. Both halves are returned because the screen that marks
/// a car in is the screen that then wants the job open in front of it.
/// </summary>
public sealed record ArrivalResult(AppointmentView Appointment, RepairOrderDetail RepairOrder);

public sealed record NewAppointment(
    RooftopId RooftopId,
    Guid CustomerId,
    Guid VehicleId,
    DateTimeOffset ScheduledFor,
    string Reason,
    decimal? EstimatedHours = null,
    Guid? AdvisorUserId = null);

public sealed record RescheduleRequest(DateTimeOffset ScheduledFor, decimal? EstimatedHours = null);

/// <summary>
/// Arriving carries what only the counter knows: the mileage now, and the currency
/// the job will be billed in. The reason travels from the booking unless somebody
/// corrects it — a customer who booked for a service and turns up with a warning
/// light is the ordinary case, not the exception.
/// </summary>
public sealed record ArrivalRequest(
    string? Complaint = null,
    int? OdometerReading = null,
    string Currency = "USD");

/// <summary>
/// Closing a booking that produced no car. <paramref name="Cancelled"/> false means
/// nobody rang — which is the figure worth having.
/// </summary>
public sealed record CloseAppointmentRequest(bool Cancelled, string? Note = null);
