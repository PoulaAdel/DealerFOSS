// LeadService — working the enquiry list, and the rooftop scope applied to it.
//
// Use:  through ILeads.
// Edit: this is where the rooftop boundary is enforced. Every read is filtered to
//       the caller's authorized rooftops in the query, and every write authorizes
//       the specific rooftop first. An unauthorized lead and an unknown one return
//       the same failure, so a response cannot be used to find out what another
//       location is working. Removing either check fails LeadTests.
//
//       Customers and vehicles are reached only through ICustomers and IVehicles —
//       never their tables, and never their entity types. That is what makes the
//       capability boundary real inside a single project (ADR-017).

using Microsoft.EntityFrameworkCore;
using DealerFOSS.Core;
using DealerFOSS.Customers;
using DealerFOSS.Data;
using DealerFOSS.Identity;
using DealerFOSS.Vehicles;

namespace DealerFOSS.Leads;

public sealed class LeadService(
    TenantDb db,
    IAccessDirectory access,
    ICustomers customers,
    IVehicles vehicles,
    ICurrentUser currentUser,
    IAuditSink audit,
    IClock clock)
    : ILeads
{
    private const string ReadPermission = Permissions.LeadsRead;
    private const string ManagePermission = Permissions.LeadsManage;

    /// <summary>Caps how many rows a single list can return, however it is called.</summary>
    private const int MaxResults = 200;

    private readonly TenantDb _db = db;
    private readonly IAccessDirectory _access = access;
    private readonly ICustomers _customers = customers;
    private readonly IVehicles _vehicles = vehicles;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditSink _audit = audit;
    private readonly IClock _clock = clock;

    public async Task<Result<IReadOnlyList<LeadSummary>>> ListAsync(
        LeadQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<IReadOnlyList<LeadSummary>>(LeadErrors.Forbidden);
        }

        // Asking for one rooftop is answered with the same refusal whether the
        // caller may not see it or it does not exist.
        if (query.RooftopId is { } requested && !scope.Covers(requested))
        {
            return Result.Failure<IReadOnlyList<LeadSummary>>(LeadErrors.Forbidden);
        }

        LeadStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse(query.Status, ignoreCase: true, out LeadStatus parsed))
            {
                return Result.Failure<IReadOnlyList<LeadSummary>>(LeadErrors.UnknownStatus);
            }

            status = parsed;
        }

        var take = Math.Clamp(query.Limit <= 0 ? 50 : query.Limit, 1, MaxResults);
        var leads = _db.Leads.AsNoTracking();

        // Applied to the query, not the results: another rooftop's rows must never
        // be read, let alone returned.
        if (!scope.IsOrganizationWide)
        {
            var allowed = scope.Rooftops.ToList();
            leads = leads.Where(l => allowed.Contains(l.RooftopId));
        }

        if (query.RooftopId is { } only)
        {
            leads = leads.Where(l => l.RooftopId == only);
        }

        if (status is { } wanted)
        {
            leads = leads.Where(l => l.Status == wanted);
        }

        if (query.AssignedToUserId is { } assignee)
        {
            leads = leads.Where(l => l.AssignedToUserId == assignee);
        }

        if (query.CustomerId is { } customer)
        {
            leads = leads.Where(l => l.CustomerId == customer);
        }

        if (query.OpenOnly)
        {
            leads = leads.Where(l => l.Status != LeadStatus.Won && l.Status != LeadStatus.Lost);
        }

        var rows = await leads
            .OrderByDescending(l => l.CapturedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        // Customer names come back in one query rather than one per row, and are
        // never kept as a stale copy on the lead itself.
        var names = await _customers.GetManyAsync(
            rows.Select(l => l.CustomerId).Distinct().ToList(), cancellationToken);

        if (names.IsFailure)
        {
            return Result.Failure<IReadOnlyList<LeadSummary>>(names.Error);
        }

        // The same reasoning as the customer names above: one query for the page,
        // and never a copy of the car's description kept on the lead.
        var cars = await _vehicles.GetManyAsync(
            rows.Where(l => l.VehicleOfInterestId is not null)
                .Select(l => l.VehicleOfInterestId!.Value)
                .Distinct()
                .ToList(),
            cancellationToken);

        if (cars.IsFailure)
        {
            return Result.Failure<IReadOnlyList<LeadSummary>>(cars.Error);
        }

        var now = _clock.UtcNow;

        return Result.Success<IReadOnlyList<LeadSummary>>(
            rows.Select(l => new LeadSummary(
                l.Id,
                l.RooftopId,
                l.Status.ToString(),
                l.Source.ToString(),
                l.CustomerId,
                NameFor(names.Value, l.CustomerId),
                l.VehicleOfInterestId,
                CarFor(cars.Value, l.VehicleOfInterestId),
                l.AssignedToUserId,
                l.CapturedAt,
                DaysOpen(l, now))).ToList());
    }

    public async Task<Result<LeadDetail>> GetAsync(Guid leadId, CancellationToken cancellationToken)
    {
        var lead = await _db.Leads
            .AsNoTracking()
            .SingleOrDefaultAsync(l => l.Id == leadId, cancellationToken);

        // Unknown and unauthorized answer identically, so a caller cannot probe
        // for enquiries belonging to a rooftop they may not see.
        if (lead is null)
        {
            return Result.Failure<LeadDetail>(LeadErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, ReadPermission, lead.RooftopId, cancellationToken))
        {
            return Result.Failure<LeadDetail>(LeadErrors.Forbidden);
        }

        return await DescribeAsync(lead, cancellationToken);
    }

    public async Task<Result<LeadDetail>> CaptureAsync(NewLead lead, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lead);

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, ManagePermission, lead.RooftopId, cancellationToken))
        {
            return Result.Failure<LeadDetail>(LeadErrors.Forbidden);
        }

        if (!Enum.TryParse<LeadSource>(lead.Source, ignoreCase: true, out var source))
        {
            return Result.Failure<LeadDetail>(LeadErrors.UnknownSource);
        }

        // The customer must exist and be readable — an enquiry attached to nobody
        // is a lost sale waiting to happen. Reached through the contract, never
        // the customer tables.
        var customer = await _customers.GetAsync(lead.CustomerId, cancellationToken);
        if (customer.IsFailure)
        {
            return Result.Failure<LeadDetail>(
                customer.Error.Type == ErrorType.NotFound ? LeadErrors.CustomerNotFound : customer.Error);
        }

        if (lead.VehicleOfInterestId is { } vehicleId)
        {
            var vehicle = await _vehicles.GetAsync(vehicleId, cancellationToken);
            if (vehicle.IsFailure)
            {
                return Result.Failure<LeadDetail>(
                    vehicle.Error.Type == ErrorType.NotFound ? LeadErrors.VehicleNotFound : vehicle.Error);
            }
        }

        Lead captured;
        try
        {
            captured = Lead.Capture(
                Guid.NewGuid(),
                lead.RooftopId,
                lead.CustomerId,
                source,
                _clock.UtcNow,
                lead.VehicleOfInterestId,
                lead.AssignedToUserId,
                lead.Enquiry,
                _currentUser.Id);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<LeadDetail>(Error.Validation("leads.invalid", ex.Message));
        }

        _db.Leads.Add(captured);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, ManagePermission, AuditOutcome.Allowed,
                "Lead", captured.Id.ToString(), lead.RooftopId.Value, "Captured", null, null),
            cancellationToken);

        return await DescribeAsync(captured, cancellationToken);
    }

    public async Task<Result<LeadDetail>> ChangeStatusAsync(
        Guid leadId,
        LeadStatusChangeRequest change,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);

        var lead = await _db.Leads.SingleOrDefaultAsync(l => l.Id == leadId, cancellationToken);
        if (lead is null)
        {
            return Result.Failure<LeadDetail>(LeadErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, ManagePermission, lead.RooftopId, cancellationToken))
        {
            return Result.Failure<LeadDetail>(LeadErrors.Forbidden);
        }

        if (!Enum.TryParse(change.Status, ignoreCase: true, out LeadStatus next))
        {
            return Result.Failure<LeadDetail>(LeadErrors.UnknownStatus);
        }

        var from = lead.Status;
        try
        {
            lead.ChangeStatus(next, _clock.UtcNow, _currentUser.Id, change.Note);
        }
        catch (InvalidOperationException ex)
        {
            // A refused move is an ordinary business outcome, not a bug: the
            // message names the moves that are available instead.
            return Result.Failure<LeadDetail>(Error.Conflict("leads.status_not_allowed", ex.Message));
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<LeadDetail>(LeadErrors.ChangedElsewhere);
        }

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, ManagePermission, AuditOutcome.Allowed,
                "Lead", lead.Id.ToString(), lead.RooftopId.Value, $"{from} to {next}", null, null),
            cancellationToken);

        return await DescribeAsync(lead, cancellationToken);
    }

    public async Task<Result<LeadDetail>> AssignAsync(
        Guid leadId,
        AssignLeadRequest assignment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        var lead = await _db.Leads.SingleOrDefaultAsync(l => l.Id == leadId, cancellationToken);
        if (lead is null)
        {
            return Result.Failure<LeadDetail>(LeadErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, ManagePermission, lead.RooftopId, cancellationToken))
        {
            return Result.Failure<LeadDetail>(LeadErrors.Forbidden);
        }

        lead.AssignTo(assignment.AssignedToUserId);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, ManagePermission, AuditOutcome.Allowed,
                "Lead", lead.Id.ToString(), lead.RooftopId.Value, "Reassigned", null, null),
            cancellationToken);

        return await DescribeAsync(lead, cancellationToken);
    }

    private async Task<Result<LeadDetail>> DescribeAsync(Lead lead, CancellationToken cancellationToken)
    {
        var names = await _customers.GetManyAsync([lead.CustomerId], cancellationToken);
        if (names.IsFailure)
        {
            return Result.Failure<LeadDetail>(names.Error);
        }

        string? vehicleName = null;
        if (lead.VehicleOfInterestId is { } vehicleId)
        {
            var vehicle = await _vehicles.GetAsync(vehicleId, cancellationToken);
            if (vehicle.IsSuccess)
            {
                vehicleName = vehicle.Value.DisplayName;
            }
        }

        var history = await _db.LeadHistory
            .AsNoTracking()
            .Where(h => h.LeadId == lead.Id)
            .OrderBy(h => h.OccurredAt)
            .ToListAsync(cancellationToken);

        // A freshly captured lead has its first history row in memory, not yet in
        // a separate query's results.
        if (history.Count == 0)
        {
            history = lead.History.ToList();
        }

        return Result.Success(new LeadDetail(
            lead.Id,
            lead.RooftopId,
            lead.Status.ToString(),
            lead.Source.ToString(),
            lead.CustomerId,
            NameFor(names.Value, lead.CustomerId),
            lead.VehicleOfInterestId,
            vehicleName,
            lead.AssignedToUserId,
            lead.Enquiry,
            lead.CapturedAt,
            lead.ClosedAt,
            lead.IsOpen,
            LeadStatusRules.MovesFrom(lead.Status).Select(s => s.ToString()).ToList(),
            history
                .OrderBy(h => h.OccurredAt)
                .Select(h => new LeadHistoryEntry(
                    h.FromStatus?.ToString(), h.ToStatus.ToString(), h.OccurredAt, h.Note))
                .ToList()));
    }

    private static string? CarFor(IReadOnlyList<VehicleSummary> cars, Guid? vehicleId) =>
        vehicleId is null ? null : cars.FirstOrDefault(v => v.Id == vehicleId.Value)?.DisplayName;

    private static string NameFor(IReadOnlyList<CustomerSummary> names, Guid customerId)
    {
        var match = names.FirstOrDefault(c => c.Id == customerId);
        return match is null ? "(customer no longer on file)" : match.DisplayName;
    }

    /// <summary>
    /// How long the enquiry has been alive. A closed lead stops counting at the
    /// day it closed, so an aging report is not dominated by old lost leads.
    /// </summary>
    private static int DaysOpen(Lead lead, DateTimeOffset now)
    {
        var until = lead.ClosedAt ?? now;
        var days = (int)(until - lead.CapturedAt).TotalDays;
        return days < 0 ? 0 : days;
    }
}

/// <summary>Stable error codes for the Leads capability (doc 06 §6).</summary>
internal static class LeadErrors
{
    public static Error Forbidden { get; } = Error.Forbidden(
        "leads.forbidden",
        "You do not have access to this rooftop's enquiries.");

    public static Error UnknownStatus { get; } = Error.Validation(
        "leads.unknown_status",
        "That is not a lead status.");

    public static Error UnknownSource { get; } = Error.Validation(
        "leads.unknown_source",
        "That is not a lead source.");

    public static Error CustomerNotFound { get; } = Error.NotFound(
        "leads.customer_not_found",
        "Record the customer before capturing their enquiry.");

    public static Error VehicleNotFound { get; } = Error.NotFound(
        "leads.vehicle_not_found",
        "That vehicle is not on file.");

    public static Error ChangedElsewhere { get; } = Error.Conflict(
        "leads.changed_elsewhere",
        "Somebody else changed this lead. Reload it and try again.");
}
