// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ILeads — what other capabilities may call to reach enquiries.
//
// Usage:
//   Sales will turn a won lead into a deal through this.
//
// Coding Instructions:
//   Every read behind this interface is filtered to the caller's authorized
//   rooftops. An empty scope is a denial, never "unfiltered".

using DealerFOSS.Core;

namespace DealerFOSS.Leads;

/// <summary>
/// Enquiries being worked at a rooftop. Rooftop-owned and scoped: the customer is
/// shared across the organization, but the enquiry is local to the location
/// chasing it (doc 04 §1).
/// </summary>
public interface ILeads
{
    /// <summary>One page of enquiries, in the order the caller asked for, with a total.</summary>
    Task<Result<Page<LeadSummary>>> ListAsync(LeadQuery query, CancellationToken cancellationToken);

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

    /// <summary>
    /// What the car is, resolved through <c>IVehicles</c> in one query for the
    /// whole page. Null when the enquiry names no particular car — which is
    /// ordinary, and different from a car that could not be read.
    /// </summary>
    string? VehicleOfInterest,
    Guid? AssignedToUserId,

    /// <summary>
    /// Who is chasing it, by name. Null when nobody has picked it up. Resolved
    /// through the staff directory's name-only lookup, which needs no permission
    /// — showing a colleague's name is not reading the staff directory.
    /// </summary>
    string? AssignedTo,
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

    /// <summary>Who is chasing it, by name. Null when nobody has picked it up.</summary>
    string? AssignedTo,
    string? Enquiry,
    DateTimeOffset CapturedAt,
    DateTimeOffset? ClosedAt,
    bool IsOpen,

    /// <summary>
    /// The statuses this lead may move to, from <see cref="LeadStatusRules"/>.
    ///
    /// Sent so a screen offers exactly what the domain allows instead of keeping
    /// its own copy of the transition table — two copies would drift, and the
    /// browser's would be the wrong one. Whether *this caller* may make the move
    /// is still the server's answer on the way in.
    /// </summary>
    IReadOnlyList<string> AvailableMoves,
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
    int Limit = 50,

    /// <summary>
    /// How many to skip. What makes the rows past the first page reachable at
    /// all — before 2026-09-11 there was no way to see them and the screen said
    /// so, which is honest and still a dead end.
    /// </summary>
    int Offset = 0,

    /// <summary>
    /// Which end of the list matters. See <see cref="LeadOrder"/>: getting this
    /// wrong is not a presentation detail, it decides which rows are returned at
    /// all once there are more than fit on a page.
    /// </summary>
    LeadOrder Order = LeadOrder.Newest);

/// <summary>
/// Which enquiries a page should contain when there are more than fit.
/// </summary>
/// <remarks>
/// <para>
/// This exists because of a real defect rather than a preference. The panel
/// headed "Nobody is chasing these" took the fifty NEWEST enquiries and then
/// displayed them longest-waiting first, so the list whose entire purpose is to
/// surface neglect was populated by recency and dropped exactly the rows it was
/// for. Measured against the running application on 2026-09-10 with 52 open
/// enquiries: the two longest-waiting customers, at 95 and 93 days, were not
/// returned at all — and taking one new enquiry pushed the 95-day customer off
/// the screen.
/// </para>
/// <para>
/// Sorting the page after it arrives cannot fix that. The order has to be part
/// of the query.
/// </para>
/// </remarks>
public enum LeadOrder
{
    /// <summary>Most recently captured first. What a work list wants.</summary>
    Newest = 0,

    /// <summary>
    /// Longest waiting first. What a chase list wants, and the only ordering
    /// under which "nobody is chasing these" means anything.
    /// </summary>
    LongestWaiting = 1,
}

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
