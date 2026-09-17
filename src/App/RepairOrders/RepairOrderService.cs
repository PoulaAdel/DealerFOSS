// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   RepairOrderService — running the workshop, and the control that makes it safe.
//
// Usage:
//   Through IRepairOrders.
//
// Coding Instructions:
//   Three things here are load-bearing.
//
//   The rooftop scope, as everywhere else: every read is filtered in the
//   query and every write authorizes the rooftop first.
//
//   Recording work and recording the customer's answer to it are separate
//   permissions. A technician finds the work; somebody holding
//   Service.Authorize says the customer agreed to pay for it. Unlike a deal
//   approval there is deliberately NO ban on the same person doing both — in
//   an independent workshop the advisor who spots it is usually the one who
//   picks up the phone, and forbidding that would stop real shops working.
//   The control is that saying "they agreed" is a distinct, permissioned,
//   timestamped act, not that two different people must perform it.
//
//   Invoicing posts to the ledger inside the same transaction as the status
//   change, so the bill and the books cannot disagree.

using Microsoft.EntityFrameworkCore;
using DealerFOSS.Accounting;
using DealerFOSS.Core;
using DealerFOSS.Customers;
using DealerFOSS.Identity;
using DealerFOSS.Inventory;
using DealerFOSS.Organization;
using DealerFOSS.Data;
using DealerFOSS.Parts;
using DealerFOSS.Receivables;
using DealerFOSS.Vehicles;

namespace DealerFOSS.RepairOrders;

public sealed class RepairOrderService(
    TenantDb db,
    IAccessDirectory access,
    ICustomers customers,
    IVehicles vehicles,
    IAccounting accounting,
    IReceivables receivables,
    IParts parts,
    IInventory inventory,
    IOrganization organization,
    IServiceCatalogue catalogue,
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
    private readonly IReceivables _receivables = receivables;
    private readonly IParts _parts = parts;
    private readonly IInventory _inventory = inventory;
    private readonly IOrganization _organization = organization;
    private readonly IServiceCatalogue _catalogue = catalogue;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditSink _audit = audit;
    private readonly IClock _clock = clock;

    public async Task<Result<Page<RepairOrderSummary>>> ListAsync(
        RepairOrderQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<Page<RepairOrderSummary>>(ServiceErrors.Forbidden);
        }

        if (query.RooftopId is { } requested && !scope.Covers(requested))
        {
            return Result.Failure<Page<RepairOrderSummary>>(ServiceErrors.Forbidden);
        }

        RepairOrderStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse(query.Status, ignoreCase: true, out RepairOrderStatus parsed))
            {
                return Result.Failure<Page<RepairOrderSummary>>(ServiceErrors.UnknownStatus);
            }

            status = parsed;
        }

        var take = Paging.Limit(query.Limit);
        var skip = Paging.Offset(query.Offset);
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

        // Counted over the same filters as the page, and before it is taken.
        var total = await orders.CountAsync(cancellationToken);

        var rows = await orders
            .Include(o => o.Lines)
            .OrderByDescending(o => o.CreatedAt)
            .ThenBy(o => o.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        var context = await LookupAsync(rows, cancellationToken);
        if (context.IsFailure)
        {
            return Result.Failure<Page<RepairOrderSummary>>(context.Error);
        }

        return Result.Success(new Page<RepairOrderSummary>(
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
                context.Value.Lot(o.RooftopId),
                o.AwaitingAnswer.Count,
                o.CreatedAt)).ToList(),
            total,
            skip,
            take));
    }

    public async Task<Result<LabourPerformance>> LabourAsync(
        LabourQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<LabourPerformance>(ServiceErrors.Forbidden);
        }

        if (query.RooftopId is { } requested && !scope.Covers(requested))
        {
            return Result.Failure<LabourPerformance>(ServiceErrors.Forbidden);
        }

        if (query.To < query.From)
        {
            return Result.Failure<LabourPerformance>(ServiceErrors.BackwardsPeriod);
        }

        // Inclusive of the last day: a manager asking for "this month" means the
        // 31st as well, and InvoicedAt carries a time.
        var from = new DateTimeOffset(query.From.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var to = new DateTimeOffset(query.To.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var orders = _db.RepairOrders
            .AsNoTracking()
            .Where(o => o.Status == RepairOrderStatus.Invoiced
                && o.InvoicedAt >= from && o.InvoicedAt < to);

        if (!scope.IsOrganizationWide)
        {
            var allowed = scope.Rooftops.ToList();
            orders = orders.Where(o => allowed.Contains(o.RooftopId));
        }

        if (query.RooftopId is { } only)
        {
            orders = orders.Where(o => o.RooftopId == only);
        }

        var rows = await orders.Include(o => o.Lines).ToListAsync(cancellationToken);

        // Flattened in memory rather than in SQL: Amount is a computed property on
        // the line (declined work is worth nothing, labour multiplies out), and it
        // is the same rule the invoice uses. Translating it into a second SQL
        // expression would be a second place for it to be wrong.
        var labour = rows
            .SelectMany(o => o.Lines
                .Where(l => l.Kind == ServiceLineKind.Labour
                    && l.Authorization != LineAuthorization.Declined)
                .Select(l => new
                {
                    o.TechnicianUserId,
                    l.PayType,
                    Hours = l.Hours ?? 0m,
                    l.Amount,
                }))
            .ToList();

        var hours = labour.Sum(l => l.Hours);
        var revenue = labour.Sum(l => l.Amount);

        // Time on the SAME JOBS, not in the same date window. A job clocked in
        // March and invoiced in April belongs to April here, with all of its
        // time — see LabourPerformance.HoursClocked for why mixing the two
        // windows would produce a ratio that looks precise and is not.
        var jobIds = rows.Select(o => o.Id).ToList();

        var clocked = await _db.TechnicianClockings
            .AsNoTracking()
            .Where(c => jobIds.Contains(c.RepairOrderId) && c.StoppedAt != null)
            .ToListAsync(cancellationToken);

        var clockedHours = clocked.Sum(c => c.Hours);

        // Attributed to the technician who CLOCKED it, not to the one assigned to
        // the job. Two people on one gearbox is ordinary, and crediting both
        // their hours to whoever happens to be named on the order would make
        // one of them look twice as slow as they are.
        var clockedBy = clocked
            .GroupBy(c => c.TechnicianUserId)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Hours));

        return Result.Success(new LabourPerformance(
            query.From,
            query.To,
            HoursSold: hours,
            LabourRevenue: revenue,
            EffectiveLabourRate: Realised(revenue, hours),
            HoursClocked: clockedHours,
            Productivity: Ratio(hours, clockedHours),
            ByTechnician: labour
                .GroupBy(l => l.TechnicianUserId)
                .Select(g =>
                {
                    var theirClocked = g.Key is { } who && clockedBy.TryGetValue(who, out var h) ? h : 0m;

                    return new TechnicianLabour(
                        g.Key,
                        g.Sum(l => l.Hours),
                        g.Sum(l => l.Amount),
                        Realised(g.Sum(l => l.Amount), g.Sum(l => l.Hours)),
                        theirClocked,
                        Ratio(g.Sum(l => l.Hours), theirClocked));
                })
                .OrderByDescending(t => t.Revenue)
                .ToList(),
            ByPayer: labour
                .GroupBy(l => l.PayType)
                .Select(g => new LabourByPayer(
                    g.Key.ToString(), g.Sum(l => l.Hours), g.Sum(l => l.Amount)))
                .OrderBy(p => p.PayType, StringComparer.Ordinal)
                .ToList(),
            // Productivity came off this list on 2026-09-16 when the clock
            // arrived. Efficiency stays: it is hours produced over hours
            // AVAILABLE, and nothing here knows who was rostered on.
            NotMeasured: [UnmeasurableLabourFigure.Efficiency]));
    }

    /// <summary>
    /// One figure over another, or null when the divisor is zero.
    /// </summary>
    /// <remarks>
    /// Null rather than zero, and the difference matters: a workshop that has not
    /// started clocking has not been unproductive, it has been unmeasured. Zero
    /// would put a damning number against a technician for a reason that has
    /// nothing to do with them.
    /// </remarks>
    private static decimal? Ratio(decimal top, decimal bottom) =>
        bottom == 0m ? null : Math.Round(top / bottom, 3, MidpointRounding.AwayFromZero);

    /// <summary>
    /// What an hour actually realised. Zero hours gives zero rather than a
    /// division by nothing — a workshop that sold no labour has no rate, and
    /// inventing one would put a number on an empty month.
    /// </summary>
    private static decimal Realised(decimal revenue, decimal hours) =>
        hours == 0m ? 0m : Math.Round(revenue / hours, 2, MidpointRounding.AwayFromZero);

    public async Task<Result<PayTypeReconciliation>> PayTypeReconciliationAsync(
        LabourQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<PayTypeReconciliation>(ServiceErrors.Forbidden);
        }

        if (query.RooftopId is { } requested && !scope.Covers(requested))
        {
            return Result.Failure<PayTypeReconciliation>(ServiceErrors.Forbidden);
        }

        if (query.To < query.From)
        {
            return Result.Failure<PayTypeReconciliation>(ServiceErrors.BackwardsPeriod);
        }

        // Inclusive of the last day, the same rule LabourAsync uses.
        var from = new DateTimeOffset(query.From.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var to = new DateTimeOffset(query.To.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var orders = _db.RepairOrders
            .AsNoTracking()
            .Where(o => o.Status == RepairOrderStatus.Invoiced
                && o.InvoicedAt >= from && o.InvoicedAt < to);

        if (!scope.IsOrganizationWide)
        {
            var allowed = scope.Rooftops.ToList();
            orders = orders.Where(o => allowed.Contains(o.RooftopId));
        }

        if (query.RooftopId is { } only)
        {
            orders = orders.Where(o => o.RooftopId == only);
        }

        var rows = await orders.Include(o => o.Lines).ToListAsync(cancellationToken);

        // Flattened in memory, the same reason LabourAsync does: Amount is a
        // computed property (declined work is worth nothing), and translating
        // that rule into SQL a second time is a second place for it to drift
        // from the invoice.
        var lines = rows
            .SelectMany(o => o.Lines
                .Where(l => l.Authorization != LineAuthorization.Declined)
                .Select(l => new { OrderId = o.Id, l.Kind, l.PayType, l.Amount, l.CostAmount }))
            .ToList();

        var byPayer = lines
            .GroupBy(l => l.PayType)
            .Select(g =>
            {
                var labourRevenue = g.Where(l => l.Kind == ServiceLineKind.Labour).Sum(l => l.Amount);
                var partsRevenue = g.Where(l => l.Kind == ServiceLineKind.Part).Sum(l => l.Amount);
                var subletRevenue = g.Where(l => l.Kind == ServiceLineKind.Sublet).Sum(l => l.Amount);
                var partsCost = g.Where(l => l.Kind == ServiceLineKind.Part).Sum(l => l.CostAmount ?? 0m);

                return new PayTypeBucket(
                    g.Key.ToString(),
                    labourRevenue,
                    partsRevenue,
                    subletRevenue,
                    labourRevenue + partsRevenue + subletRevenue,
                    partsCost,
                    partsRevenue - partsCost,
                    g.Count(),
                    g.Select(l => l.OrderId).Distinct().Count());
            })
            .OrderBy(p => p.PayType, StringComparer.Ordinal)
            .ToList();

        return Result.Success(new PayTypeReconciliation(
            query.From,
            query.To,
            byPayer.Sum(p => p.Revenue),
            byPayer));
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

        // Refused rather than defaulted. A typo in the pay type silently becoming
        // "CustomerPay" would bill somebody for warranty work.
        if (!Enum.TryParse<ServicePayType>(line.PayType, ignoreCase: true, out var payType))
        {
            return Result.Failure<RepairOrderDetail>(ServiceErrors.UnknownPayType);
        }

        // THE CATALOGUE FILLS THE BLANKS AND NEVER OVERRULES THE PERSON. What the
        // caller sent wins every time; the op code only supplies what was left
        // out. An advisor who types 2.5 hours against a 1.4-hour job has found a
        // seized bolt, and a system that quietly wrote 1.4 back over them would
        // be lying about the work and short-paying the technician for it.
        var chosen = await ResolveOpCodeAsync(line, kind, payType, order.RooftopId, cancellationToken);
        if (chosen.IsFailure)
        {
            return Result.Failure<RepairOrderDetail>(chosen.Error);
        }

        var filled = chosen.Value;

        try
        {
            order.AddLine(
                kind, filled.Description, filled.Hours, filled.Rate, line.UnitAmount,
                _clock.UtcNow, _currentUser.Id, line.PartId, line.PartQuantity, payType,
                filled.OpCodeId);
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

    public async Task<Result<RepairOrderDetail>> ClockOnAsync(
        Guid repairOrderId,
        ClockOnRequest request,
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

        // A job that is finished or cancelled is not somewhere time can be
        // spent. Allowed on Booked as well as InProgress: a technician starting
        // work IS how a job becomes in progress, and refusing here would make
        // them move the status first to record something already true.
        if (order.Status is RepairOrderStatus.Invoiced or RepairOrderStatus.Cancelled)
        {
            return Result.Failure<RepairOrderDetail>(ServiceErrors.JobIsClosedToTime);
        }

        var now = _clock.UtcNow;

        // Already on THIS job? Nothing to do, and saying so beats silently
        // opening a second entry that would double-count every hour.
        var here = await _db.TechnicianClockings.SingleOrDefaultAsync(
            c => c.TechnicianUserId == request.TechnicianUserId
                && c.RepairOrderId == order.Id
                && c.StoppedAt == null,
            cancellationToken);

        if (here is not null)
        {
            return Result.Failure<RepairOrderDetail>(ServiceErrors.AlreadyClockedOnHere);
        }

        // SWITCHING, not refusing. See IRepairOrders.ClockOnAsync: a technician
        // moves between jobs all morning, and a system that made them clock off
        // first is a system they stop using.
        var elsewhere = await _db.TechnicianClockings
            .SingleOrDefaultAsync(
                c => c.TechnicianUserId == request.TechnicianUserId && c.StoppedAt == null,
                cancellationToken);

        if (elsewhere is not null)
        {
            var movedTo = await _db.RepairOrders
                .AsNoTracking()
                .Where(o => o.Id == elsewhere.RepairOrderId)
                .Select(o => o.Number)
                .SingleOrDefaultAsync(cancellationToken);

            elsewhere.Stop(now, $"Switched to {order.Number} from {movedTo ?? "another job"}");
        }

        _db.TechnicianClockings.Add(TechnicianClocking.Start(
            Guid.NewGuid(), order.Id, order.RooftopId, request.TechnicianUserId, now));

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, WritePermission, AuditOutcome.Allowed,
                "RepairOrder", order.Id.ToString(), order.RooftopId.Value,
                $"Clocked {request.TechnicianUserId} on to {order.Number}", null, null),
            cancellationToken);

        return await DescribeAsync(order, cancellationToken);
    }

    public async Task<Result<RepairOrderDetail>> ClockOffAsync(
        Guid repairOrderId,
        ClockOffRequest request,
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

        var open = await _db.TechnicianClockings.SingleOrDefaultAsync(
            c => c.TechnicianUserId == request.TechnicianUserId
                && c.RepairOrderId == order.Id
                && c.StoppedAt == null,
            cancellationToken);

        if (open is null)
        {
            return Result.Failure<RepairOrderDetail>(ServiceErrors.NotClockedOn);
        }

        try
        {
            open.Stop(_clock.UtcNow);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
        {
            return Result.Failure<RepairOrderDetail>(
                Error.Validation("service.clocking_invalid", ex.Message));
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, WritePermission, AuditOutcome.Allowed,
                "RepairOrder", order.Id.ToString(), order.RooftopId.Value,
                $"Clocked {request.TechnicianUserId} off {order.Number} after {open.Hours}h", null, null),
            cancellationToken);

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
            // Stock comes off the shelf here, not when the part was written up.
            // A job can be built and cancelled; only invoicing is the moment the
            // part is definitely gone — and it is also the moment whose cost the
            // books should carry. IParts does not save; this method's
            // SaveChanges commits the stock movement with the invoice and the
            // ledger entry, so the three cannot disagree.
            var issued = await _parts.IssueAsync(
                order.Lines
                    .Where(l => l.DrawsFromStock && l.Authorization != LineAuthorization.Declined)
                    .Select(l => new PartIssue(l.PartId!.Value, l.PartQuantity!.Value))
                    .ToList(),
                order.RooftopId,
                cancellationToken);

            if (issued.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<RepairOrderDetail>(issued.Error);
            }

            // Frozen per line, so a later price change cannot rewrite it.
            foreach (var line in order.Lines.Where(l => l.DrawsFromStock))
            {
                var cost = issued.Value.Parts
                    .Where(p => p.PartId == line.PartId!.Value)
                    .Select(p => (decimal?)p.Cost)
                    .FirstOrDefault();

                if (cost is not null && line.CostAmount is null)
                {
                    line.RecordCost(cost.Value);
                }
            }

            // Internal work on a car we own belongs in that car's cost. On
            // anything else — a courtesy car, a director's vehicle, a customer's
            // car the dealership decided to cover — there is no unit to put it
            // on, and it stays a charge. Asking Inventory rather than guessing is
            // the difference between the two.
            var capitalised = 0m;
            if (order.InternalTotal.Amount > 0m)
            {
                var owned = await _inventory.FindOwnedAsync(
                    order.VehicleId, order.RooftopId, cancellationToken);

                if (owned.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure<RepairOrderDetail>(owned.Error);
                }

                if (owned.Value is not null)
                {
                    capitalised = order.InternalTotal.Amount;
                }
            }

            var posted = await _accounting.PostServiceInvoiceAsync(
                BuildPosting(order, issued.Value.TotalCost, capitalised), cancellationToken);

            if (posted.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<RepairOrderDetail>(posted.Error);
            }

            // Only the customer's share becomes a debt. Warranty is already a
            // receivable of its own kind, from the manufacturer, and internal work
            // is a charge to the dealership — neither is anybody's bill to pay, and
            // putting them here would have somebody chasing a customer for a
            // warranty claim.
            if (order.AmountDue.Amount > 0m)
            {
                var owed = await _receivables.OpenAsync(
                    new NewReceivable(
                        order.RooftopId,
                        order.CustomerId,
                        ReceivableSource.RepairOrder,
                        // The order ID, not its number: a job number is unique per
                        // rooftop and two lots legitimately both have an RO-1080.
                        // It is also what the ledger entry above is filed under, so
                        // the debt and the debit share a reference.
                        order.Id.ToString(),
                        order.AmountDue.Amount,
                        order.AmountDue.Currency,
                        _clock.UtcNow),
                    cancellationToken);

                if (owed.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure<RepairOrderDetail>(owed.Error);
                }
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
    private static ServiceInvoicePosting BuildPosting(
        RepairOrder order, decimal partsCost, decimal internalCapitalised) =>
        new(
            order.RooftopId,
            order.Id.ToString(),
            order.Currency,
            // What was sold, then who settles it. Both sides total the same work.
            Labour: order.LabourTotal.Amount,
            Parts: order.PartsTotal.Amount,
            Sublet: order.SubletTotal.Amount,
            AmountDue: order.AmountDue.Amount,
            Warranty: order.WarrantyTotal.Amount,
            Internal: order.InternalTotal.Amount,
            InternalCapitalised: internalCapitalised,
            // Zero when nothing on the job came off a shelf — a workshop selling
            // only labour has no parts cost, which is different from having an
            // unknown one.
            PartsCost: partsCost,
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
    /// <summary>
    /// What a line should actually say, once the catalogue has filled in
    /// whatever the caller left out.
    /// </summary>
    /// <remarks>
    /// Three sources, in this order, and the order is the whole design:
    ///
    ///   1. What the caller sent. Always wins.
    ///   2. The op code, when the line cites one — its description and its
    ///      standard hours.
    ///   3. The rooftop's labour rate for whoever is paying.
    ///
    /// A LINE WITH NO OP CODE IS UNCHANGED BY ANY OF THIS except for the rate,
    /// which is worth having on free-text labour too: the commonest reason a
    /// rate is wrong is that somebody typed it, and a lot that has set its
    /// retail rate has already answered the question.
    /// </remarks>
    private async Task<Result<FilledLine>> ResolveOpCodeAsync(
        NewServiceLine line,
        ServiceLineKind kind,
        ServicePayType payType,
        RooftopId rooftopId,
        CancellationToken cancellationToken)
    {
        var description = line.Description;
        var hours = line.Hours;
        var rate = line.Rate;
        Guid? opCodeId = null;

        if (line.OpCodeId is { } wanted)
        {
            var code = await _db.OpCodes
                .AsNoTracking()
                .SingleOrDefaultAsync(c => c.Id == wanted, cancellationToken);

            if (code is null)
            {
                return Result.Failure<FilledLine>(ServiceErrors.UnknownOpCode);
            }

            // A withdrawn code is refused on a NEW line and left alone on every
            // line that already cites it. That is the whole point of withdrawing
            // rather than deleting.
            if (!code.IsActive)
            {
                return Result.Failure<FilledLine>(ServiceErrors.OpCodeWithdrawn(code.Code));
            }

            opCodeId = code.Id;
            description = string.IsNullOrWhiteSpace(description) ? code.Description : description;
            hours ??= code.StandardHours;
        }

        // Only labour has a rate. Asking for one on a part line would return the
        // workshop's hourly figure and quietly multiply it by nothing.
        if (kind == ServiceLineKind.Labour && rate is null)
        {
            var found = await _catalogue.RateForAsync(rooftopId, payType, cancellationToken);

            // A lot that has not set its rates is not an error — it is a lot that
            // has not been set up yet, and the person can still type a rate. The
            // line's own validation is what refuses labour with no rate at all.
            if (found.IsSuccess && found.Value is { } applicable)
            {
                rate = applicable.AmountPerHour;
            }
        }

        return Result.Success(new FilledLine(description, hours, rate, opCodeId));
    }

    /// <summary>What the line will actually carry, after the catalogue is consulted.</summary>
    private sealed record FilledLine(string Description, decimal? Hours, decimal? Rate, Guid? OpCodeId);

    private async Task<Result<ServiceLookup>> LookupAsync(
        List<RepairOrder> orders,
        CancellationToken cancellationToken)
    {
        if (orders.Count == 0)
        {
            return Result.Success(new ServiceLookup([], [], new Dictionary<RooftopId, string>()));
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

        return Result.Success(new ServiceLookup(
            names.Value,
            cars.Value,
            await LotsAsync(orders.Select(o => o.RooftopId).Distinct().ToList(), cancellationToken)));
    }

    /// <summary>
    /// Which lot each job belongs to, by id.
    /// </summary>
    /// <remarks>
    /// A handful of calls, not one per row: a dealership has one to five
    /// rooftops and a page of fifty jobs comes from at most that many.
    ///
    /// <b>A failure here does NOT fail the list.</b> Every role that can read a
    /// job holds <c>Organization.Read</c> today, but that is a fact about the
    /// seeded roles rather than a rule, and a job list that goes blank because
    /// somebody's role was narrowed would be a far worse outcome than a job
    /// number without its lot beside it — which is exactly what the screen
    /// showed before 2026-09-15 anyway. Degrading to that is safe; refusing is
    /// not.
    /// </remarks>
    private async Task<Dictionary<RooftopId, string>> LotsAsync(
        List<RooftopId> rooftopIds,
        CancellationToken cancellationToken)
    {
        var lots = new Dictionary<RooftopId, string>();

        foreach (var id in rooftopIds)
        {
            var found = await _organization.GetRooftopAsync(id, cancellationToken);
            if (found.IsSuccess)
            {
                lots[id] = found.Value.Code;
            }
        }

        return lots;
    }

    private sealed record ServiceLookup(
        IReadOnlyList<CustomerSummary> Customers,
        IReadOnlyList<VehicleSummary> Vehicles,
        IReadOnlyDictionary<RooftopId, string> Lots)
    {
        /// <summary>
        /// The lot's code — "NAG-01" — or empty when it could not be read.
        /// Empty rather than a placeholder: the screen decides whether to show
        /// it at all, and "(unknown)" beside a job number is worse than nothing.
        /// </summary>
        public string Lot(RooftopId id) => Lots.TryGetValue(id, out var code) ? code : string.Empty;


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
            .ThenBy(h => h.Sequence)
            .ToListAsync(cancellationToken);

        var clockings = await _db.TechnicianClockings
            .AsNoTracking()
            .Where(c => c.RepairOrderId == order.Id)
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
            context.Value.Lot(order.RooftopId),
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
            order.WarrantyTotal.Amount,
            order.InternalTotal.Amount,
            order.WorkTotal.Amount,
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
                    l.PayType.ToString(),
                    l.Authorization.ToString(),
                    l.AuthorizedAt,
                    l.AuthorizedByUserId,
                    l.AuthorizationNote,
                    l.OpCodeId,
                    l.PartId,
                    l.PartQuantity,
                    l.CostAmount))
                .ToList(),
            history
                .OrderBy(h => h.OccurredAt)
                .ThenBy(h => h.Sequence)
                .Select(h => new RepairOrderHistoryEntry(
                    h.FromStatus?.ToString(), h.ToStatus.ToString(), h.OccurredAt,
                    h.ChangedByUserId, h.Note, h.AmountAtChange))
                .ToList(),
            clockings
                .OrderByDescending(c => c.StartedAt)
                .ThenBy(c => c.Id)
                .Select(c => new ClockingView(
                    c.Id, c.TechnicianUserId, c.StartedAt, c.StoppedAt,
                    c.Hours, c.IsOpen, c.StoppedBecause))
                .ToList(),
            // Closed entries only. An open one contributes zero until it stops —
            // see TechnicianClocking.Hours for why a figure that changes every
            // time somebody looks at it is worse than no figure.
            clockings.Sum(c => c.Hours)));
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

    public static Error BackwardsPeriod { get; } = Error.Validation(
        "service.backwards_period",
        "The end of the period cannot be before its start.");

    public static Error JobIsClosedToTime { get; } = Error.Conflict(
        "service.job_closed_to_time",
        "That job is finished. Time cannot be booked to it.");

    public static Error AlreadyClockedOnHere { get; } = Error.Conflict(
        "service.already_clocked_on",
        "That technician is already on the clock for this job.");

    public static Error NotClockedOn { get; } = Error.Conflict(
        "service.not_clocked_on",
        "That technician is not on the clock for this job.");

    public static Error UnknownOpCode { get; } = Error.Validation(
        "service.unknown_op_code",
        "That job is not in the catalogue.");

    public static Error OpCodeWithdrawn(string code) => Error.Validation(
        "service.op_code_withdrawn",
        $"{code} has been withdrawn and cannot go on a new job. " +
        "Jobs that already cite it are unaffected.");

    public static Error OpCodeAlreadyExists(string code) => Error.Conflict(
        "service.op_code_exists",
        $"{code} is already a job in the catalogue. Revise that one rather than adding a second.");

    public static Error UnknownPayType { get; } = Error.Validation(
        "service.unknown_pay_type",
        "Work is paid for by the customer, by the manufacturer under warranty, or by the dealership itself.");

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
