// IRepairOrders — what the rest of the application may call to reach service work.
//
// Use:  inject IRepairOrders. Nothing outside this folder touches RepairOrder,
//       ServiceLine, or the service schema (ADR-014).
// Edit: every read is rooftop-scoped inside the service, so a caller cannot widen
//       its own view by passing a different rooftop id — an unauthorized one is
//       refused rather than quietly ignored.

using DealerFOSS.Core;

namespace DealerFOSS.RepairOrders;

public interface IRepairOrders
{
    /// <summary>The work at a rooftop, newest first, capped.</summary>
    Task<Result<IReadOnlyList<RepairOrderSummary>>> ListAsync(
        RepairOrderQuery query,
        CancellationToken cancellationToken);

    Task<Result<RepairOrderDetail>> GetAsync(Guid repairOrderId, CancellationToken cancellationToken);

    /// <summary>Books a car in against a customer and their own vehicle.</summary>
    Task<Result<RepairOrderDetail>> OpenAsync(NewRepairOrder order, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a piece of work. Added before the job starts, it is what the customer
    /// asked for; added once work is under way, it needs their answer first.
    /// </summary>
    Task<Result<RepairOrderDetail>> AddLineAsync(
        Guid repairOrderId,
        NewServiceLine line,
        CancellationToken cancellationToken);

    Task<Result<RepairOrderDetail>> RemoveLineAsync(
        Guid repairOrderId,
        Guid lineId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records what the customer said about work found mid-job. Its own
    /// permission, because saying "they agreed to pay" is a different act from
    /// noticing the work.
    /// </summary>
    Task<Result<RepairOrderDetail>> AnswerLineAsync(
        Guid repairOrderId,
        Guid lineId,
        LineAnswerRequest answer,
        CancellationToken cancellationToken);

    Task<Result<RepairOrderDetail>> AssignTechnicianAsync(
        Guid repairOrderId,
        AssignTechnicianRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Moves the job on. Invoicing posts it to the ledger in the same transaction,
    /// so the bill and the books cannot disagree about whether it happened.
    /// </summary>
    Task<Result<RepairOrderDetail>> ChangeStatusAsync(
        Guid repairOrderId,
        RepairOrderStatusChangeRequest change,
        CancellationToken cancellationToken);
}

/// <summary>Which repair orders a list covers. Every filter narrows; none widens.</summary>
public sealed record RepairOrderQuery(
    RooftopId? RooftopId = null,
    string? Status = null,
    Guid? CustomerId = null,
    Guid? VehicleId = null,
    Guid? TechnicianUserId = null,
    bool OpenOnly = false,
    int Limit = 50);

/// <summary>Enough to run a workshop's day from one list.</summary>
public sealed record RepairOrderSummary(
    Guid Id,
    RooftopId RooftopId,
    string Number,
    string Status,
    Guid CustomerId,
    string CustomerName,
    Guid VehicleId,
    string Vehicle,
    string Complaint,
    decimal AmountDue,
    string Currency,
    Guid? AdvisorUserId,
    Guid? TechnicianUserId,

    /// <summary>
    /// How many pieces of work are waiting on the customer. The one number an
    /// advisor needs to see without opening anything — it is the list of phone
    /// calls they owe, and every one of them blocks an invoice.
    /// </summary>
    int LinesAwaitingAnswer,
    DateTimeOffset OpenedAt);

public sealed record RepairOrderDetail(
    Guid Id,
    RooftopId RooftopId,
    string Number,
    string Status,
    Guid CustomerId,
    string CustomerName,
    Guid VehicleId,
    string Vehicle,
    string Complaint,
    int? OdometerReading,
    string Currency,
    decimal LabourTotal,
    decimal PartsTotal,
    decimal SubletTotal,
    decimal AmountDue,
    Guid? AdvisorUserId,
    Guid? TechnicianUserId,

    /// <summary>
    /// When the car came in. On the summary already; it belongs here too, because
    /// "how long has this been sitting here" is the first thing anybody asks when
    /// they open a job.
    /// </summary>
    DateTimeOffset OpenedAt,
    DateTimeOffset? InvoicedAt,

    /// <summary>Whether the work may still be edited.</summary>
    bool LinesAreOpen,

    /// <summary>
    /// The statuses this job may move to next, so a screen offers the legal moves
    /// without keeping its own copy of the rules.
    /// </summary>
    IReadOnlyList<string> AvailableMoves,
    IReadOnlyList<ServiceLineView> Lines,
    IReadOnlyList<RepairOrderHistoryEntry> History);

public sealed record ServiceLineView(
    Guid Id,
    string Kind,
    string Description,
    decimal? Hours,
    decimal? Rate,
    decimal Amount,
    string Authorization,
    DateTimeOffset? AuthorizedAt,
    Guid? AuthorizedByUserId,
    string? AuthorizationNote);

public sealed record RepairOrderHistoryEntry(
    string? FromStatus,
    string ToStatus,
    DateTimeOffset OccurredAt,
    Guid? ChangedByUserId,
    string? Note,
    decimal AmountAtChange);

public sealed record NewRepairOrder(
    RooftopId RooftopId,
    Guid CustomerId,
    Guid VehicleId,
    string Complaint,
    string Currency = "USD",
    int? OdometerReading = null,
    Guid? AdvisorUserId = null);

public sealed record NewServiceLine(
    string Kind,
    string Description,
    decimal? Hours = null,
    decimal? Rate = null,
    decimal UnitAmount = 0m);

/// <summary>
/// What the customer said. <paramref name="Note"/> is how it was obtained —
/// "phoned 10:40, agreed" — which is the part that matters if it is ever
/// questioned.
/// </summary>
public sealed record LineAnswerRequest(bool Approved, string? Note = null);

public sealed record AssignTechnicianRequest(Guid? TechnicianUserId);

public sealed record RepairOrderStatusChangeRequest(string Status, string? Note = null);
