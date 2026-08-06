// RepairOrderService — running the workshop, and the control that makes it safe.
//
// Use:  through IRepairOrders.
// Edit: three things here are load-bearing.
//
//       The rooftop scope, as everywhere else: every read is filtered in the
//       query and every write authorizes the rooftop first.
//
//       Recording work and recording the customer's answer to it are separate
//       permissions. A technician finds the work; somebody holding
//       Service.Authorize says the customer agreed to pay for it. Unlike a deal
//       approval there is deliberately NO ban on the same person doing both — in
//       an independent workshop the advisor who spots it is usually the one who
//       picks up the phone, and forbidding that would stop real shops working.
//       The control is that saying "they agreed" is a distinct, permissioned,
//       timestamped act, not that two different people must perform it.
//
//       Invoicing posts to the ledger inside the same transaction as the status
//       change, so the bill and the books cannot disagree.

using Microsoft.EntityFrameworkCore;
using DealerFOSS.Accounting;
using DealerFOSS.Core;
using DealerFOSS.Customers;
using DealerFOSS.Identity;
using DealerFOSS.Data;
using DealerFOSS.Vehicles;

namespace DealerFOSS.RepairOrders;

public sealed class RepairOrderService(
    TenantDb db,
    IAccessDirectory access,
    ICustomers customers,
    IVehicles vehicles,
    IAccounting accounting,
    ICurrentUser currentUser,
    IAuditSink audit,
    IClock clock)
    : IRepairOrders
{
    private const string ReadPermission = Permissions.ServiceRead;
    private const string WritePermission = Permissions.ServiceWrite;
    private const string AuthorizePermission = Permissions.ServiceAuthorize;

    private const int MaxResults = 200;

    /// <summary>Where each rooftop's numbering starts, so the first job is RO-1001.</summary>
    private const int FirstNumber = 1000;

    private readonly TenantDb _db = db;
    private readonly IAccessDirectory _access = access;
    private readonly ICustomers _customers = customers;
    private readonly IVehicles _vehicles = vehicles;
    private readonly IAccounting _accounting = accounting;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditSink _audit = audit;
    private readonly IClock _clock = clock;

    public async Task<Result<IReadOnlyList<RepairOrderSummary>>> ListAsync(
        RepairOrderQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<IReadOnlyList<RepairOrderSummary>>(ServiceErrors.Forbidden);
        }

        if (query.RooftopId is { } requested && !scope.Covers(requested))
        {
            return Result.Failure<IReadOnlyList<RepairOrderSummary>>(ServiceErrors.Forbidden);
        }

        RepairOrderStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse(query.Status, ignoreCase: true, out RepairOrderStatus parsed))
            {
                return Result.Failure<IReadOnlyList<RepairOrderSummary>>(ServiceErrors.UnknownStatus);
            }

            status = parsed;
        }

        var take = Math.Clamp(query.Limit <= 0 ? 50 : query.Limit, 1, MaxResults);
        var orders = _db.RepairOrders.AsNoTracking();

        if (!scope.IsOrganizationWide)
        {
            var allowed = scope.Rooftops.ToList();
            orders = orders.Where(o => allowed.Contains(o.RooftopId));
        }

        if (query.RooftopId is { } only)
        {
            orders = orders.Where(o => o.RooftopId == only);
        }

        if (status is { } wanted)
        {
            orders = orders.Where(o => o.Status == wanted);
        }

        if (query.CustomerId is { } customer)
        {
            orders = orders.Where(o => o.CustomerId == customer);
        }

        if (query.VehicleId is { } vehicle)
        {
            orders = orders.Where(o => o.VehicleId == vehicle);
        }

        if (query.TechnicianUserId is { } technician)
        {
            orders = orders.Where(o => o.TechnicianUserId == technician);
        }

        if (query.OpenOnly)
        {
            orders = orders.Where(o =>
                o.Status != RepairOrderStatus.Invoiced && o.Status != RepairOrderStatus.Cancelled);
        }

        var rows = await orders
            .Include(o => o.Lines)
            .OrderByDescending(o => o.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        var context = await LookupAsync(rows, cancellationToken);
        if (context.IsFailure)
        {
            return Result.Failure<IReadOnlyList<RepairOrderSummary>>(context.Error);
        }

        return Result.Success<IReadOnlyList<RepairOrderSummary>>(
            rows.Select(o => new RepairOrderSummary(
                o.Id,
                o.RooftopId,
                o.Number,
                o.Status.ToString(),
                o.CustomerId,
                context.Value.CustomerName(o.CustomerId),
                o.VehicleId,
                context.Value.Vehicle(o.VehicleId),
                o.Complaint,
                o.AmountDue.Amount,
                o.Currency,
                o.AdvisorUserId,
                o.TechnicianUserId,
                o.AwaitingAnswer.Count,
                o.CreatedAt)).ToList());
    }

    public async Task<Result<RepairOrderDetail>> GetAsync(
        Guid repairOrderId,
        CancellationToken cancellationToken)
    {
        var order = await LoadAsync(repairOrderId, tracked: false, cancellationToken);

        // Unknown and unauthorized answer identically, so a caller cannot probe
        // for jobs belonging to a workshop they may not see.
        if (order is null)
        {
            return Result.Failure<RepairOrderDetail>(ServiceErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, ReadPermission, order.RooftopId, cancellationToken))
        {
            return Result.Failure<RepairOrderDetail>(ServiceErrors.Forbidden);
        }

        return await DescribeAsync(order, cancellationToken);
    }

    public async Task<Result<RepairOrderDetail>> OpenAsync(
        NewRepairOrder order,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, WritePermission, order.RooftopId, cancellationToken))
        {
            return Result.Failure<RepairOrderDetail>(ServiceErrors.Forbidden);
        }

        var customer = await _customers.GetAsync(order.CustomerId, cancellationToken);
        if (customer.IsFailure)
        {
            return Result.Failure<RepairOrderDetail>(
                customer.Error.Type == ErrorType.NotFound ? ServiceErrors.CustomerNotFound : customer.Error);
        }

        // The car is a vehicle identity, not a unit in stock — a customer's own
        // car is not on anybody's lot.
        var vehicle = await _vehicles.GetAsync(order.VehicleId, cancellationToken);
        if (vehicle.IsFailure)
        {
            return Result.Failure<RepairOrderDetail>(
                vehicle.Error.Type == ErrorType.NotFound ? ServiceErrors.VehicleNotFound : vehicle.Error);
        }

        RepairOrder opened;
        try
        {
            opened = RepairOrder.Open(
                Guid.NewGuid(),
                order.RooftopId,
                order.CustomerId,
                order.VehicleId,
                await NextNumberAsync(order.RooftopId, cancellationToken),
                order.Complaint,
                order.Currency,
                _clock.UtcNow,
                order.OdometerReading,
                order.AdvisorUserId ?? _currentUser.Id);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<RepairOrderDetail>(Error.Validation("service.invalid", ex.Message));
        }

        _db.RepairOrders.Add(opened);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two jobs booked in at the same instant can pick the same number.
            // The unique index catches it; asking the person to try again is
            // honest and costs one click, where a retry loop hides a collision
            // that should stay visible if it ever becomes frequent.
            return Result.Failure<RepairOrderDetail>(ServiceErrors.NumberTaken);
        }

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, WritePermission, AuditOutcome.Allowed,
                "RepairOrder", opened.Id.ToString(), order.RooftopId.Value, "Opened", null, null),
            cancellationToken);

        return await DescribeAsync(opened, cancellationToken);
    }

    public async Task<Result<RepairOrderDetail>> AddLineAsync(
        Guid repairOrderId,
        NewServiceLine line,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(line);

        var order = await LoadAsync(repairOrderId, tracked: true, cancellationToken);
        if (order is null)
        {
            return Result.Failure<RepairOrderDetail>(ServiceErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, WritePermission, order.RooftopId, cancellationToken))
        {
            return Result.Failure<RepairOrderDetail>(ServiceErrors.Forbidden);
        }

        if (!Enum.TryParse<ServiceLineKind>(line.Kind, ignoreCase: true, out var kind))
        {
            return Result.Failure<RepairOrderDetail>(ServiceErrors.UnknownLineKind);
        }

        try
        {
            order.AddLine(
                kind, line.Description, line.Hours, line.Rate, line.UnitAmount,
                _clock.UtcNow, _currentUser.Id);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<RepairOrderDetail>(Error.Validation("service.invalid_line", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<RepairOrderDetail>(Error.Conflict("service.lines_frozen", ex.Message));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await DescribeAsync(order, cancellationToken);
    }

    public async Task<Result<RepairOrderDetail>> RemoveLineAsync(
        Guid repairOrderId,
        Guid lineId,
        CancellationToken cancellationToken)
    {
        var order = await LoadAsync(repairOrderId, tracked: true, cancellationToken);
        if (order is null)
        {
            return Result.Failure<RepairOrderDetail>(ServiceErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, WritePermission, order.RooftopId, cancellationToken))
        {
            return Result.Failure<RepairOrderDetail>(ServiceErrors.Forbidden);
        }

        try
        {
            order.RemoveLine(lineId);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<RepairOrderDetail>(Error.Conflict("service.line_not_removable", ex.Message));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await DescribeAsync(order, cancellationToken);
    }

    public async Task<Result<RepairOrderDetail>> AnswerLineAsync(
        Guid repairOrderId,
        Guid lineId,
        LineAnswerRequest answer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(answer);

        var order = await LoadAsync(repairOrderId, tracked: true, cancellationToken);
        if (order is null)
        {
            return Result.Failure<RepairOrderDetail>(ServiceErrors.Forbidden);
        }

        // Recording that the customer agreed to pay is its own right. Somebody who
        // may write up work is not automatically somebody who may say it was
        // authorized.
        if (!await _access.IsAuthorizedAsync(_currentUser.Id, AuthorizePermission, order.RooftopId, cancellationToken))
        {
            await _audit.RecordAsync(
                AuditEntry.Denied(_currentUser.Id, AuthorizePermission, "RepairOrder", order.Id.ToString(),
                    order.RooftopId.Value, "Attempted to authorize additional work."),
                cancellationToken);

            return Result.Failure<RepairOrderDetail>(ServiceErrors.AuthorizationForbidden);
        }

        try
        {
            order.AnswerLine(lineId, answer.Approved, _clock.UtcNow, _currentUser.Id, answer.Note);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<RepairOrderDetail>(Error.Conflict("service.line_already_answered", ex.Message));
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, AuthorizePermission, AuditOutcome.Allowed,
                "RepairOrder", order.Id.ToString(), order.RooftopId.Value,
                answer.Approved ? "Customer authorized work" : "Customer declined work", null, null),
            cancellationToken);

        return await DescribeAsync(order, cancellationToken);
    }

    public async Task<Result<RepairOrderDetail>> AssignTechnicianAsync(
        Guid repairOrderId,
        AssignTechnicianRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var order = await LoadAsync(repairOrderId, tracked: true, cancellationToken);
        if (order is null)
        {
            return Result.Failure<RepairOrderDetail>(ServiceErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, WritePermission, order.RooftopId, cancellationToken))
        {
            return Result.Failure<RepairOrderDetail>(ServiceErrors.Forbidden);
        }

        // A technician must be able to see this workshop, or the job vanishes the
        // moment it is assigned.
        if (request.TechnicianUserId is { } technician
            && !await _access.IsAuthorizedAsync(technician, ReadPermission, order.RooftopId, cancellationToken))
        {
            return Result.Failure<RepairOrderDetail>(ServiceErrors.TechnicianNotHere);
        }

        try
        {
            order.AssignTechnician(request.TechnicianUserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<RepairOrderDetail>(Error.Conflict("service.cannot_reassign", ex.Message));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await DescribeAsync(order, cancellationToken);
    }

    public async Task<Result<RepairOrderDetail>> ChangeStatusAsync(
        Guid repairOrderId,
        RepairOrderStatusChangeRequest change,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);

        var order = await LoadAsync(repairOrderId, tracked: true, cancellationToken);
        if (order is null)
        {
            return Result.Failure<RepairOrderDetail>(ServiceErrors.Forbidden);
        }

        if (!Enum.TryParse(change.Status, ignoreCase: true, out RepairOrderStatus next))
        {
            return Result.Failure<RepairOrderDetail>(ServiceErrors.UnknownStatus);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, WritePermission, order.RooftopId, cancellationToken))
        {
            return Result.Failure<RepairOrderDetail>(ServiceErrors.Forbidden);
        }

        var from = order.Status;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            order.ChangeStatus(next, _clock.UtcNow, _currentUser.Id, change.Note);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            // Unanswered work is the one refusal worth its own code, because a
            // screen should send somebody to the phone rather than showing them a
            // generic conflict.
            var error = order.AwaitingAnswer.Count > 0 && next == RepairOrderStatus.Invoiced
                ? Error.Conflict("service.work_not_authorized", ex.Message)
                : Error.Conflict("service.status_not_allowed", ex.Message);

            return Result.Failure<RepairOrderDetail>(error);
        }

        // Invoicing is an accounting event. It posts inside the same transaction
        // as the status change, so the ledger and the job can never disagree about
        // whether the customer was billed.
        if (next == RepairOrderStatus.Invoiced)
        {
            var posted = await _accounting.PostServiceInvoiceAsync(
                BuildPosting(order), cancellationToken);

            if (posted.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<RepairOrderDetail>(posted.Error);
            }
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<RepairOrderDetail>(ServiceErrors.ChangedElsewhere);
        }

        await transaction.CommitAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, WritePermission, AuditOutcome.Allowed,
                "RepairOrder", order.Id.ToString(), order.RooftopId.Value,
                $"{from} to {next}", null, null),
            cancellationToken);

        return await DescribeAsync(order, cancellationToken);
    }

    /// <summary>
    /// Restates the job in the terms the ledger needs, so Accounting never has to
    /// know what a service line is and RepairOrders never has to know what an
    /// account is.
    /// </summary>
    private static ServiceInvoicePosting BuildPosting(RepairOrder order) =>
        new(
            order.RooftopId,
            order.Id.ToString(),
            order.Currency,
            Labour: order.LabourTotal.Amount,
            Parts: order.PartsTotal.Amount,
            Sublet: order.SubletTotal.Amount,
            AmountDue: order.AmountDue.Amount,
            Memo: $"Service invoice {order.Number}");

    /// <summary>
    /// The next job number for a workshop. Per rooftop, so two lots do not share a
    /// sequence and neither has to explain why its numbering skips.
    /// </summary>
    private async Task<string> NextNumberAsync(RooftopId rooftopId, CancellationToken cancellationToken)
    {
        var used = await _db.RepairOrders
            .AsNoTracking()
            .Where(o => o.RooftopId == rooftopId)
            .CountAsync(cancellationToken);

        return $"RO-{FirstNumber + used + 1}";
    }

    private async Task<RepairOrder?> LoadAsync(
        Guid repairOrderId,
        bool tracked,
        CancellationToken cancellationToken)
    {
        var query = _db.RepairOrders.Include(o => o.Lines).AsQueryable();
        if (!tracked)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(o => o.Id == repairOrderId, cancellationToken);
    }

    /// <summary>
    /// The customer names and car descriptions for a page of jobs, in two queries
    /// rather than two per row, and always through the published contracts.
    /// </summary>
    private async Task<Result<ServiceLookup>> LookupAsync(
        List<RepairOrder> orders,
        CancellationToken cancellationToken)
    {
        if (orders.Count == 0)
        {
            return Result.Success(new ServiceLookup([], []));
        }

        var names = await _customers.GetManyAsync(
            orders.Select(o => o.CustomerId).Distinct().ToList(), cancellationToken);

        if (names.IsFailure)
        {
            return Result.Failure<ServiceLookup>(names.Error);
        }

        var cars = await _vehicles.GetManyAsync(
            orders.Select(o => o.VehicleId).Distinct().ToList(), cancellationToken);

        if (cars.IsFailure)
        {
            return Result.Failure<ServiceLookup>(cars.Error);
        }

        return Result.Success(new ServiceLookup(names.Value, cars.Value));
    }

    private sealed record ServiceLookup(
        IReadOnlyList<CustomerSummary> Customers,
        IReadOnlyList<VehicleSummary> Vehicles)
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
    }

    private async Task<Result<RepairOrderDetail>> DescribeAsync(
        RepairOrder order,
        CancellationToken cancellationToken)
    {
        var context = await LookupAsync([order], cancellationToken);
        if (context.IsFailure)
        {
            return Result.Failure<RepairOrderDetail>(context.Error);
        }

        var history = await _db.RepairOrderHistory
            .AsNoTracking()
            .Where(h => h.RepairOrderId == order.Id)
            .OrderBy(h => h.OccurredAt)
            .ToListAsync(cancellationToken);

        // A job that has only just been opened has its first history row in memory
        // rather than in a separate query's results.
        if (history.Count == 0)
        {
            history = order.History.ToList();
        }

        return Result.Success(new RepairOrderDetail(
            order.Id,
            order.RooftopId,
            order.Number,
            order.Status.ToString(),
            order.CustomerId,
            context.Value.CustomerName(order.CustomerId),
            order.VehicleId,
            context.Value.Vehicle(order.VehicleId),
            order.Complaint,
            order.OdometerReading,
            order.Currency,
            order.LabourTotal.Amount,
            order.PartsTotal.Amount,
            order.SubletTotal.Amount,
            order.AmountDue.Amount,
            order.AdvisorUserId,
            order.TechnicianUserId,
            // The same field the summary reports as OpenedAt: an audit stamp, and
            // for a repair order "created" and "booked in" are the same moment.
            order.CreatedAt,
            order.InvoicedAt,
            order.LinesAreOpen,
            RepairOrderStatusRules.MovesFrom(order.Status).Select(s => s.ToString()).ToList(),
            order.Lines
                .Select(l => new ServiceLineView(
                    l.Id,
                    l.Kind.ToString(),
                    l.Description,
                    l.Hours,
                    l.Rate,
                    l.Amount,
                    l.Authorization.ToString(),
                    l.AuthorizedAt,
                    l.AuthorizedByUserId,
                    l.AuthorizationNote))
                .ToList(),
            history
                .OrderBy(h => h.OccurredAt)
                .Select(h => new RepairOrderHistoryEntry(
                    h.FromStatus?.ToString(), h.ToStatus.ToString(), h.OccurredAt,
                    h.ChangedByUserId, h.Note, h.AmountAtChange))
                .ToList()));
    }
}

/// <summary>Stable error codes for the RepairOrders capability (doc 06 §6).</summary>
internal static class ServiceErrors
{
    public static Error Forbidden { get; } = Error.Forbidden(
        "service.forbidden",
        "You do not have access to this workshop's jobs.");

    public static Error AuthorizationForbidden { get; } = Error.Forbidden(
        "service.authorization_forbidden",
        "Recording what the customer agreed to needs Service.Authorize. Ask an advisor.");

    public static Error UnknownStatus { get; } = Error.Validation(
        "service.unknown_status",
        "That is not a repair order status.");

    public static Error UnknownLineKind { get; } = Error.Validation(
        "service.unknown_line_kind",
        "A line is Labour, a Part, or Sublet work.");

    public static Error CustomerNotFound { get; } = Error.NotFound(
        "service.customer_not_found",
        "Record the customer before booking their car in.");

    public static Error VehicleNotFound { get; } = Error.NotFound(
        "service.vehicle_not_found",
        "Record the car before booking it in.");

    public static Error TechnicianNotHere { get; } = Error.Validation(
        "service.technician_not_here",
        "That person does not work at this location.");

    public static Error NumberTaken { get; } = Error.Conflict(
        "service.number_taken",
        "Another job took that number just now. Try again.");

    public static Error ChangedElsewhere { get; } = Error.Conflict(
        "service.changed_elsewhere",
        "Somebody else changed this job. Reload it and try again.");
}
