// ILeads — what other capabilities may call to reach enquiries.
//
// Use:  Sales will turn a won lead into a deal through this.
// Edit: every read behind this interface is filtered to the caller's authorized
//       rooftops. An empty scope is a denial, never "unfiltered".

using DealerFOSS.Core;

namespace DealerFOSS.Leads;

/// <summary>
/// Enquiries being worked at a rooftop. Rooftop-owned and scoped: the customer is
/// shared across the organization, but the enquiry is local to the location
/// chasing it (doc 04 §1).
/// </summary>
public interface ILeads
{
    Task<Result<IReadOnlyList<LeadSummary>>> ListAsync(LeadQuery query, CancellationToken cancellationToken);

    Task<Result<LeadDetail>> GetAsync(Guid leadId, CancellationToken cancellationToken);

    Task<Result<LeadDetail>> CaptureAsync(NewLead lead, CancellationToken cancellationToken);

    Task<Result<LeadDetail>> ChangeStatusAsync(
        Guid leadId,
        LeadStatusChangeRequest change,
        CancellationToken cancellationToken);

    Task<Result<LeadDetail>> AssignAsync(Guid leadId, AssignLeadRequest assignment, CancellationToken cancellationToken);
}

/// <summary>One enquiry as a work list shows it.</summary>
public sealed record LeadSummary(
    Guid Id,
    RooftopId RooftopId,
    string Status,
    string Source,
    Guid CustomerId,
    string CustomerName,
    Guid? VehicleOfInterestId,
    Guid? AssignedToUserId,
    DateTimeOffset CapturedAt,
    int DaysOpen);

/// <summary>One enquiry in full, with everything that has happened to it.</summary>
public sealed record LeadDetail(
    Guid Id,
    RooftopId RooftopId,
    string Status,
    string Source,
    Guid CustomerId,
    string CustomerName,
    Guid? VehicleOfInterestId,
    string? VehicleOfInterest,
    Guid? AssignedToUserId,
    string? Enquiry,
    DateTimeOffset CapturedAt,
    DateTimeOffset? ClosedAt,
    bool IsOpen,
    IReadOnlyList<LeadHistoryEntry> History);

public sealed record LeadHistoryEntry(
    string? FromStatus,
    string ToStatus,
    DateTimeOffset OccurredAt,
    string? Note);

/// <summary>How a caller narrows a lead list.</summary>
public sealed record LeadQuery(
    RooftopId? RooftopId = null,
    string? Status = null,
    Guid? AssignedToUserId = null,
    Guid? CustomerId = null,
    bool OpenOnly = false,
    int Limit = 50);

/// <summary>What a caller supplies to capture an enquiry.</summary>
public sealed record NewLead(
    RooftopId RooftopId,
    Guid CustomerId,
    string Source,
    Guid? VehicleOfInterestId = null,
    Guid? AssignedToUserId = null,
    string? Enquiry = null);

/// <summary>What a caller supplies to move a lead on.</summary>
public sealed record LeadStatusChangeRequest(string Status, string? Note = null);

/// <summary>What a caller supplies to hand a lead to a salesperson, or take it back.</summary>
public sealed record AssignLeadRequest(Guid? AssignedToUserId);
