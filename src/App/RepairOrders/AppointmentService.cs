// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   AppointmentService — the service diary, and the one moment it hands over to the
//   workshop.
//
// Usage:
//   Through IAppointments.
//
// Coding Instructions:
//   Four things here are load-bearing.
//
//   The rooftop scope, as everywhere else: every read is filtered in the query
//   and every write authorizes the rooftop first. A diary is a list of
//   customers' names and cars, so an unscoped read here leaks exactly what
//   the rooftop boundary exists to protect.
//
//   Arriving opens the repair order and links it INSIDE ONE TRANSACTION. Both
//   halves commit or neither does. Without that, a failure between them
//   leaves either a job the diary has lost or an arrival with no job, and
//   both are invisible until somebody tries to reconcile a month later.
//
//   The permission to book is Service.Write — the same right that opens a
//   job — and that is deliberate. Arriving a car IS opening a job, so a
//   weaker booking permission would be a way to reach the stronger one.
//
//   Capacity is REPORTED, NOT ENFORCED. A workshop that is full still takes
//   the booking, because real shops overbook on purpose: jobs come in under
//   estimate, cars are collected late, and a diary that refuses at eight
//   hours would be worked around within a week by booking everything as an
//   estimate of zero. Showing the load makes the decision visible; refusing
//   it makes the data wrong.

using Microsoft.EntityFrameworkCore;
using DealerFOSS.Core;
using DealerFOSS.Customers;
using DealerFOSS.Data;
using DealerFOSS.Identity;
using DealerFOSS.Vehicles;

namespace DealerFOSS.RepairOrders;

public sealed class AppointmentService(
    TenantDb db,
    IAccessDirectory access,
    ICustomers customers,
    IVehicles vehicles,
    IRepairOrders repairOrders,
    ICurrentUser currentUser,
    IAuditSink audit,
    IClock clock)
    : IAppointments
{
    private const string ReadPermission = Permissions.ServiceRead;
    private const string WritePermission = Permissions.ServiceWrite;

    private const int MaxResults = 400;

    private readonly TenantDb _db = db;
    private readonly IAccessDirectory _access = access;
    private readonly ICustomers _customers = customers;
    private readonly IVehicles _vehicles = vehicles;
    private readonly IRepairOrders _repairOrders = repairOrders;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditSink _audit = audit;
    private readonly IClock _clock = clock;

    public async Task<Result<Diary>> ListAsync(
        AppointmentQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<Diary>(AppointmentErrors.Forbidden);
        }

        if (query.RooftopId is { } requested && !scope.Covers(requested))
        {
            return Result.Failure<Diary>(AppointmentErrors.Forbidden);
        }

        AppointmentStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse(query.Status, ignoreCase: true, out AppointmentStatus parsed))
            {
                return Result.Failure<Diary>(AppointmentErrors.UnknownStatus);
            }

            status = parsed;
        }

        if (query.From is { } from && query.To is { } to && to < from)
        {
            return Result.Failure<Diary>(AppointmentErrors.BackwardsRange);
        }

        var take = Math.Clamp(query.Limit <= 0 ? 200 : query.Limit, 1, MaxResults);
        var bookings = _db.Appointments.AsNoTracking();

        if (!scope.IsOrganizationWide)
        {
            var allowed = scope.Rooftops.ToList();
            bookings = bookings.Where(a => allowed.Contains(a.RooftopId));
        }

        if (query.RooftopId is { } only)
        {
            bookings = bookings.Where(a => a.RooftopId == only);
        }

        // Compared as instants at the edges of the day. A booking at 17:30 on the
        // last day of the range is inside it, which "< To" alone would drop.
        if (query.From is { } start)
        {
            var startsAt = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            bookings = bookings.Where(a => a.ScheduledFor >= startsAt);
        }

        if (query.To is { } end)
        {
            var endsAt = new DateTimeOffset(end.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            bookings = bookings.Where(a => a.ScheduledFor < endsAt);
        }

        if (status is { } wanted)
        {
            bookings = bookings.Where(a => a.Status == wanted);
        }

        if (query.CustomerId is { } customer)
        {
            bookings = bookings.Where(a => a.CustomerId == customer);
        }

        if (query.VehicleId is { } vehicle)
        {
            bookings = bookings.Where(a => a.VehicleId == vehicle);
        }

        if (query.OpenOnly)
        {
            bookings = bookings.Where(a => a.Status == AppointmentStatus.Scheduled);
        }

        // Soonest first. A diary read newest-first would put next month above this
        // afternoon, which is the wrong way round for the only person using it.
        var rows = await bookings
            .OrderBy(a => a.ScheduledFor)
            .Take(take)
            .ToListAsync(cancellationToken);

        var context = await LookupAsync(rows, cancellationToken);
        if (context.IsFailure)
        {
            return Result.Failure<Diary>(context.Error);
        }

        var views = rows.Select(a => Describe(a, context.Value)).ToList();

        return Result.Success(new Diary(views, LoadOf(rows)));
    }

    public async Task<Result<AppointmentView>> GetAsync(
        Guid appointmentId,
        CancellationToken cancellationToken)
    {
        var booking = await LoadAsync(appointmentId, tracked: false, cancellationToken);

        // Unknown and unauthorized answer identically, so a caller cannot probe
        // for bookings at a workshop they may not see.
        if (booking is null)
        {
            return Result.Failure<AppointmentView>(AppointmentErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, ReadPermission, booking.RooftopId, cancellationToken))
        {
            return Result.Failure<AppointmentView>(AppointmentErrors.Forbidden);
        }

        return await DescribeAsync(booking, cancellationToken);
    }

    public async Task<Result<AppointmentView>> BookAsync(
        NewAppointment booking,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(booking);

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, WritePermission, booking.RooftopId, cancellationToken))
        {
            return Result.Failure<AppointmentView>(AppointmentErrors.Forbidden);
        }

        var customer = await _customers.GetAsync(booking.CustomerId, cancellationToken);
        if (customer.IsFailure)
        {
            return Result.Failure<AppointmentView>(
                customer.Error.Type == ErrorType.NotFound
                    ? AppointmentErrors.CustomerNotFound
                    : customer.Error);
        }

        var vehicle = await _vehicles.GetAsync(booking.VehicleId, cancellationToken);
        if (vehicle.IsFailure)
        {
            return Result.Failure<AppointmentView>(
                vehicle.Error.Type == ErrorType.NotFound
                    ? AppointmentErrors.VehicleNotFound
                    : vehicle.Error);
        }

        Appointment taken;
        try
        {
            taken = Appointment.Book(
                Guid.NewGuid(),
                booking.RooftopId,
                booking.CustomerId,
                booking.VehicleId,
                booking.ScheduledFor,
                booking.Reason,
                _clock.UtcNow,
                booking.EstimatedHours,
                booking.AdvisorUserId ?? _currentUser.Id);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<AppointmentView>(Error.Validation("appointments.invalid", ex.Message));
        }

        _db.Appointments.Add(taken);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, WritePermission, AuditOutcome.Allowed,
                "Appointment", taken.Id.ToString(), booking.RooftopId.Value, "Booked", null, null),
            cancellationToken);

        return await DescribeAsync(taken, cancellationToken);
    }

    public async Task<Result<AppointmentView>> RescheduleAsync(
        Guid appointmentId,
        RescheduleRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var booking = await LoadAsync(appointmentId, tracked: true, cancellationToken);
        if (booking is null)
        {
            return Result.Failure<AppointmentView>(AppointmentErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, WritePermission, booking.RooftopId, cancellationToken))
        {
            return Result.Failure<AppointmentView>(AppointmentErrors.Forbidden);
        }

        try
        {
            booking.Reschedule(request.ScheduledFor, _clock.UtcNow, request.EstimatedHours);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<AppointmentView>(Error.Conflict("appointments.not_movable", ex.Message));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await DescribeAsync(booking, cancellationToken);
    }

    public async Task<Result<ArrivalResult>> ArriveAsync(
        Guid appointmentId,
        ArrivalRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var booking = await LoadAsync(appointmentId, tracked: true, cancellationToken);
        if (booking is null)
        {
            return Result.Failure<ArrivalResult>(AppointmentErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, WritePermission, booking.RooftopId, cancellationToken))
        {
            return Result.Failure<ArrivalResult>(AppointmentErrors.Forbidden);
        }

        // Refused before a job is opened, not after. Opening one and then finding
        // the booking already had a job would leave the second job behind.
        if (booking.RepairOrderId is not null || !booking.IsOpen)
        {
            return Result.Failure<ArrivalResult>(AppointmentErrors.AlreadyArrived);
        }

        // Both halves or neither. RepairOrderService shares this scoped TenantDb,
        // so its SaveChanges joins the transaction opened here rather than
        // committing a job on its own.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var opened = await _repairOrders.OpenAsync(
            new NewRepairOrder(
                booking.RooftopId,
                booking.CustomerId,
                booking.VehicleId,
                // What the customer said at the counter wins over what they said
                // on the phone a fortnight ago; the booking's reason is the
                // fallback, not the record.
                string.IsNullOrWhiteSpace(request.Complaint) ? booking.Reason : request.Complaint,
                request.Currency,
                request.OdometerReading,
                booking.AdvisorUserId),
            cancellationToken);

        if (opened.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<ArrivalResult>(opened.Error);
        }

        try
        {
            booking.Arrive(opened.Value.Id, _clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<ArrivalResult>(Error.Conflict("appointments.already_arrived", ex.Message));
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, WritePermission, AuditOutcome.Allowed,
                "Appointment", booking.Id.ToString(), booking.RooftopId.Value,
                $"Arrived as job {opened.Value.Number}", null, null),
            cancellationToken);

        var described = await DescribeAsync(booking, cancellationToken);
        return described.IsFailure
            ? Result.Failure<ArrivalResult>(described.Error)
            : Result.Success(new ArrivalResult(described.Value, opened.Value));
    }

    public async Task<Result<AppointmentView>> CloseAsync(
        Guid appointmentId,
        CloseAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var booking = await LoadAsync(appointmentId, tracked: true, cancellationToken);
        if (booking is null)
        {
            return Result.Failure<AppointmentView>(AppointmentErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, WritePermission, booking.RooftopId, cancellationToken))
        {
            return Result.Failure<AppointmentView>(AppointmentErrors.Forbidden);
        }

        try
        {
            if (request.Cancelled)
            {
                booking.Cancel(request.Note);
            }
            else
            {
                booking.MissedIt(request.Note);
            }
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<AppointmentView>(Error.Conflict("appointments.not_closable", ex.Message));
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, WritePermission, AuditOutcome.Allowed,
                "Appointment", booking.Id.ToString(), booking.RooftopId.Value,
                request.Cancelled ? "Cancelled" : "Did not arrive", null, null),
            cancellationToken);

        return await DescribeAsync(booking, cancellationToken);
    }

    /// <summary>
    /// What each day in the returned set is carrying. Grouped on the date the
    /// booking falls on, and counting only what is still expected.
    /// </summary>
    private static List<DiaryDay> LoadOf(List<Appointment> rows) =>
        rows.Where(a => AppointmentStatusRules.CountsTowardLoad(a.Status))
            .GroupBy(a => DateOnly.FromDateTime(a.ScheduledFor.UtcDateTime))
            .OrderBy(g => g.Key)
            .Select(g => new DiaryDay(
                g.Key,
                g.Count(),
                g.Sum(a => a.EstimatedHours ?? 0m),
                g.Count(a => a.EstimatedHours is null)))
            .ToList();

    private async Task<Appointment?> LoadAsync(
        Guid appointmentId,
        bool tracked,
        CancellationToken cancellationToken)
    {
        var query = _db.Appointments.AsQueryable();
        if (!tracked)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(a => a.Id == appointmentId, cancellationToken);
    }

    /// <summary>
    /// Customer names, car descriptions and job numbers for a page of bookings, in
    /// three queries rather than three per row, and always through the published
    /// contracts.
    /// </summary>
    private async Task<Result<DiaryLookup>> LookupAsync(
        List<Appointment> bookings,
        CancellationToken cancellationToken)
    {
        if (bookings.Count == 0)
        {
            return Result.Success(new DiaryLookup([], [], []));
        }

        var names = await _customers.GetManyAsync(
            bookings.Select(a => a.CustomerId).Distinct().ToList(), cancellationToken);

        if (names.IsFailure)
        {
            return Result.Failure<DiaryLookup>(names.Error);
        }

        var cars = await _vehicles.GetManyAsync(
            bookings.Select(a => a.VehicleId).Distinct().ToList(), cancellationToken);

        if (cars.IsFailure)
        {
            return Result.Failure<DiaryLookup>(cars.Error);
        }

        // Read straight from the table rather than through IRepairOrders: this is
        // the same capability and the same schema, and the alternative is a
        // rooftop-scoped list call per row to recover a string.
        var jobIds = bookings
            .Where(a => a.RepairOrderId is not null)
            .Select(a => a.RepairOrderId!.Value)
            .Distinct()
            .ToList();

        var numbers = jobIds.Count == 0
            ? []
            : await _db.RepairOrders
                .AsNoTracking()
                .Where(o => jobIds.Contains(o.Id))
                .Select(o => new JobNumber(o.Id, o.Number))
                .ToListAsync(cancellationToken);

        return Result.Success(new DiaryLookup(names.Value, cars.Value, numbers));
    }

    private sealed record JobNumber(Guid Id, string Number);

    private sealed record DiaryLookup(
        IReadOnlyList<CustomerSummary> Customers,
        IReadOnlyList<VehicleSummary> Vehicles,
        IReadOnlyList<JobNumber> Jobs)
    {
        public string CustomerName(Guid id)
        {
            var match = Customers.FirstOrDefault(c => c.Id == id);
            return match is null ? "(customer no longer on file)" : match.DisplayName;
        }

        public string Vehicle(Guid id)
        {
            var match = Vehicles.FirstOrDefault(v => v.Id == id);
            return match is null ? "(car no longer on file)" : match.DisplayName;
        }

        public string? JobNumberOf(Guid? id) =>
            id is null ? null : Jobs.FirstOrDefault(j => j.Id == id.Value)?.Number;
    }

    private static AppointmentView Describe(Appointment booking, DiaryLookup context) =>
        new(
            booking.Id,
            booking.RooftopId,
            booking.ScheduledFor,
            booking.EstimatedHours,
            booking.Status.ToString(),
            booking.CustomerId,
            context.CustomerName(booking.CustomerId),
            booking.VehicleId,
            context.Vehicle(booking.VehicleId),
            booking.Reason,
            booking.AdvisorUserId,
            booking.RepairOrderId,
            context.JobNumberOf(booking.RepairOrderId),
            booking.ArrivedAt,
            booking.Outcome,
            booking.IsOpen);

    private async Task<Result<AppointmentView>> DescribeAsync(
        Appointment booking,
        CancellationToken cancellationToken)
    {
        var context = await LookupAsync([booking], cancellationToken);

        return context.IsFailure
            ? Result.Failure<AppointmentView>(context.Error)
            : Result.Success(Describe(booking, context.Value));
    }
}

/// <summary>Stable error codes for the service diary (doc 06 §6).</summary>
internal static class AppointmentErrors
{
    public static Error Forbidden { get; } = Error.Forbidden(
        "appointments.forbidden",
        "You do not have access to this workshop's diary.");

    public static Error UnknownStatus { get; } = Error.Validation(
        "appointments.unknown_status",
        "A booking is Scheduled, Arrived, a NoShow, or Cancelled.");

    public static Error BackwardsRange { get; } = Error.Validation(
        "appointments.backwards_range",
        "The end of the range is before its start.");

    public static Error CustomerNotFound { get; } = Error.NotFound(
        "appointments.customer_not_found",
        "Record the customer before booking their car in.");

    public static Error VehicleNotFound { get; } = Error.NotFound(
        "appointments.vehicle_not_found",
        "Record the car before booking it in.");

    public static Error AlreadyArrived { get; } = Error.Conflict(
        "appointments.already_arrived",
        "This booking already has a job against it. Open that job rather than booking the car in twice.");
}
