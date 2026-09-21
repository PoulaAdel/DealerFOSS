// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   IRepairOrders — what the rest of the application may call to reach service work.
//
// Usage:
//   Inject IRepairOrders. Nothing outside this folder touches RepairOrder,
//   ServiceLine, or the service schema (ADR-014).
//
// Coding Instructions:
//   Every read is rooftop-scoped inside the service, so a caller cannot widen
//   its own view by passing a different rooftop id — an unauthorized one is
//   refused rather than quietly ignored.

using DealerFOSS.Core;

namespace DealerFOSS.RepairOrders;

public interface IRepairOrders
{
    /// <summary>The work at a rooftop, newest first, capped.</summary>
    Task<Result<Page<RepairOrderSummary>>> ListAsync(
        RepairOrderQuery query,
        CancellationToken cancellationToken);

    Task<Result<RepairOrderDetail>> GetAsync(Guid repairOrderId, CancellationToken cancellationToken);

    /// <summary>Books a car in against a customer and their own vehicle.</summary>
    Task<Result<RepairOrderDetail>> OpenAsync(NewRepairOrder order, CancellationToken cancellationToken);

    /// <summary>
    /// A job arriving from another DealerFOSS installation, as it was left,
    /// keeping its id.
    /// </summary>
    /// <remarks>
    /// <b>Nothing is posted, no parts leave the shelf and no receivable is
    /// raised.</b> Invoicing a job here does all three, because those things
    /// happen when work is billed. A job arriving in a records package was
    /// billed somewhere else and its money is inside the receiving dealership's
    /// opening balances. See ADR-027.
    ///
    /// Part lines arrive as their description and amount, with no link to this
    /// installation's parts catalogue. That is deliberate: the receiving
    /// dealership's shelf never held these parts, so taking them off it would be
    /// a stock movement that did not happen.
    ///
    /// The caller passes what the source system said the customer owed, and the
    /// record is refused if the lines written here do not reach it.
    /// </remarks>
    Task<Result<ImportOutcome>> ImportAsync(
        ImportedRepairOrder order,
        CancellationToken cancellationToken);

    /// <summary>
    /// Adds a piece of work. Added before the job starts, it is what the customer
    /// asked for; added once work is under way, it needs their answer first.
    /// </summary>
    Task<Result<RepairOrderDetail>> AddLineAsync(
        Guid repairOrderId,
        NewServiceLine line,
        CancellationToken cancellationToken);

    /// <summary>
    /// What the workshop sold, over a period, and what it realised per hour.
    ///
    /// <para>
    /// <b>Read the caveat on <see cref="LabourPerformance"/> before showing any of
    /// this to a service manager.</b> Two of the four numbers the trade runs on
    /// cannot be produced from what this system stores, and a report that quietly
    /// omits them invites somebody to assume they were fine.
    /// </para>
    /// </summary>
    Task<Result<LabourPerformance>> LabourAsync(LabourQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// What the workshop sold over a period, split by who pays — customer,
    /// warranty, or the dealership itself. The mix is the point: a shop where
    /// warranty has quietly become half the work is running a different
    /// business than it was last quarter, and no single revenue total shows
    /// that.
    /// </summary>
    Task<Result<PayTypeReconciliation>> PayTypeReconciliationAsync(
        LabourQuery query,
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
    /// Puts a technician on the clock against this job.
    /// </summary>
    /// <remarks>
    /// <b>If they were already clocked on somewhere else, that one is stopped
    /// and this one starts.</b> A technician cannot be on two jobs at once, and
    /// a system that refused the second clock-on is a system people stop using:
    /// they move between jobs all morning, and friction there means nobody
    /// clocks anything. The stopped entry records why.
    ///
    /// Several technicians on ONE job is ordinary — a gearbox out is two people
    /// — and nothing here limits that.
    /// </remarks>
    Task<Result<RepairOrderDetail>> ClockOnAsync(
        Guid repairOrderId,
        ClockOnRequest request,
        CancellationToken cancellationToken);

    /// <summary>Takes a technician off the clock on this job.</summary>
    Task<Result<RepairOrderDetail>> ClockOffAsync(
        Guid repairOrderId,
        ClockOffRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Moves the job on. Invoicing posts it to the ledger in the same transaction,
    /// so the bill and the books cannot disagree about whether it happened.
    /// </summary>
    Task<Result<RepairOrderDetail>> ChangeStatusAsync(
        Guid repairOrderId,
        RepairOrderStatusChangeRequest change,
        CancellationToken cancellationToken);

    /// <summary>
    /// Moves this order's warranty claim on — submitted, approved, denied, or
    /// recorded as paid. Internal tracking only; nothing here talks to a
    /// manufacturer's own system. Paid is the one move that posts to the
    /// ledger, since it is the one where cash actually arrives.
    /// </summary>
    Task<Result<RepairOrderDetail>> ChangeClaimStatusAsync(
        Guid repairOrderId,
        WarrantyClaimStatusChangeRequest change,
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
    int Limit = 50,
    int Offset = 0);

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
    /// Which lot this job belongs to -- "NAG-01".
    /// </summary>
    /// <remarks>
    /// Numbers restart per rooftop by design, so 80 of this dealership's 82
    /// numbers are used twice. The id was always here and a code is what a person
    /// reads. Empty when the rooftop could not be resolved, which the screen
    /// treats as "do not show it" rather than as an error.
    /// </remarks>
    string RooftopCode,

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

    /// <summary>
    /// Which lot this job belongs to. Always worth showing here, unlike in a
    /// list: one job read on its own carries no context to infer it from.
    /// </summary>
    string RooftopCode,
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

    /// <summary>What the customer owes — customer-pay lines only.</summary>
    decimal AmountDue,

    /// <summary>Owed by the manufacturer once a claim is accepted.</summary>
    decimal WarrantyTotal,

    /// <summary>Carried by the dealership itself, never billed out.</summary>
    decimal InternalTotal,

    /// <summary>Everything the job is worth, whoever settles it.</summary>
    decimal WorkTotal,
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
    IReadOnlyList<RepairOrderHistoryEntry> History,

    /// <summary>
    /// Who has been on this job and for how long, newest first.
    /// </summary>
    IReadOnlyList<ClockingView> Clockings,

    /// <summary>
    /// Hours actually spent, summed from the CLOSED clockings. What the job
    /// cost in time, as against LabourTotal which is what it was worth.
    /// </summary>
    decimal ClockedHours,

    /// <summary>
    /// This order's warranty claim, when it has one — meaning it has been
    /// invoiced with warranty-pay work on it. Null before invoicing, and null
    /// forever on a job with no warranty lines.
    /// </summary>
    WarrantyClaimView? WarrantyClaim);

/// <summary>
/// One repair order's warranty claim: what was billed, where it stands, and
/// what the manufacturer has done about it so far.
/// </summary>
public sealed record WarrantyClaimView(
    Guid Id,
    string Status,
    decimal Amount,

    /// <summary>What the manufacturer actually paid. Null until Status is Paid.</summary>
    decimal? AmountPaid,
    string Currency,

    /// <summary>The statuses this claim may move to next.</summary>
    IReadOnlyList<string> AvailableMoves,
    IReadOnlyList<WarrantyClaimHistoryEntry> History);

public sealed record WarrantyClaimHistoryEntry(
    string? FromStatus,
    string ToStatus,
    DateTimeOffset OccurredAt,
    Guid? ChangedByUserId,
    string? Note);

/// <summary>
/// What a caller supplies to move a warranty claim on.
/// </summary>
/// <param name="AmountPaid">
/// Required when <paramref name="Status"/> is Paid; ignored otherwise. May
/// differ from what was billed — see WarrantyClaim's own file header.
/// </param>
/// <param name="Note">
/// Required when <paramref name="Status"/> is Denied, where it is the reason.
/// Optional everywhere else.
/// </param>
public sealed record WarrantyClaimStatusChangeRequest(
    string Status,
    decimal? AmountPaid = null,
    string? Note = null);

/// <summary>A period, and optionally one workshop within it.</summary>
public sealed record LabourQuery(DateOnly From, DateOnly To, RooftopId? RooftopId = null);

/// <summary>
/// Labour sold over a period, whole and per technician.
/// </summary>
/// <remarks>
/// <para>
/// <b>Counted from invoiced jobs only.</b> Work in progress is not revenue, and a
/// report that counts it flatters the month and then contradicts itself when a
/// job is cancelled.
/// </para>
/// <para>
/// <b>Every pay type counts.</b> A technician who spent Tuesday on warranty work
/// sold those hours; the manufacturer is paying rather than the customer, which
/// changes who is billed and not whether the work happened. Broken out by payer
/// as well, because the mix is itself the thing a service manager watches.
/// </para>
/// <para>
/// <b>What this deliberately does NOT report</b>, named in
/// <see cref="NotMeasured"/> so a screen can say so rather than leave a gap:
/// technician <i>efficiency</i> (hours produced ÷ hours available, which the
/// trade benchmarks at 125%) and <i>productivity</i> (hours billed ÷ hours
/// clocked, benchmarked at 87.5%). Both need a denominator this system has never
/// recorded — there is no shift length and no time clock. Publishing a guess at
/// either would be worse than publishing neither, because both are used to judge
/// individual people.
/// </para>
/// </remarks>
public sealed record LabourPerformance(
    DateOnly From,
    DateOnly To,
    decimal HoursSold,
    decimal LabourRevenue,

    /// <summary>
    /// Revenue ÷ hours sold: what an hour actually realised, as against the
    /// posted rate. Zero when no hours were sold, which is a real answer and not
    /// a missing one.
    /// </summary>
    decimal EffectiveLabourRate,

    /// <summary>
    /// Hours actually spent on the work invoiced in this period, from the
    /// technician clock.
    /// </summary>
    /// <remarks>
    /// <b>Counted over the same JOBS as the hours sold, not over the same
    /// dates.</b> A job clocked in March and invoiced in April belongs to April
    /// here, with all of its time — which is the only way the two figures can be
    /// divided by each other and mean anything. Counting clockings by the date
    /// they stopped would mix two windows and produce a ratio that looks precise
    /// and is not.
    /// </remarks>
    decimal HoursClocked,

    /// <summary>
    /// Hours billed ÷ hours clocked. Null when nothing was clocked, which is a
    /// missing measurement rather than a productivity of zero — a workshop that
    /// has not started using the clock has not produced nothing.
    /// </summary>
    decimal? Productivity,

    IReadOnlyList<TechnicianLabour> ByTechnician,
    IReadOnlyList<LabourByPayer> ByPayer,

    /// <summary>Names from <see cref="UnmeasurableLabourFigure"/>.</summary>
    IReadOnlyList<string> NotMeasured);

/// <summary>
/// One technician's share. <c>TechnicianUserId</c> is null for work invoiced with
/// nobody assigned — kept rather than dropped, because hours nobody is credited
/// with are exactly what a service manager wants to see.
/// </summary>
public sealed record TechnicianLabour(
    Guid? TechnicianUserId,
    decimal HoursSold,
    decimal Revenue,
    decimal EffectiveLabourRate,

    /// <summary>Hours this technician clocked on the work invoiced here.</summary>
    decimal HoursClocked,

    /// <summary>Hours billed over hours clocked. Null when nothing was clocked.</summary>
    decimal? Productivity);

public sealed record LabourByPayer(string PayType, decimal HoursSold, decimal Revenue);

/// <summary>
/// Every kind of work invoiced over a period, rolled up by who pays.
/// </summary>
/// <remarks>
/// <b>Counted from invoiced jobs only</b>, the same rule as
/// <see cref="LabourPerformance"/>, and for the same reason: work in progress
/// is not revenue.
///
/// <b>Gross profit is reported for parts only.</b> A part's cost is frozen at
/// invoicing (<c>ServiceLine.CostAmount</c>); labour and sublet work carry no
/// cost basis this system has ever recorded, so a "gross" across all three
/// would quietly average a real figure against two invented ones. The parts
/// figure is honest; the other two kinds are left as revenue alone.
/// </remarks>
public sealed record PayTypeReconciliation(
    DateOnly From,
    DateOnly To,
    decimal TotalRevenue,
    IReadOnlyList<PayTypeBucket> ByPayer);

/// <summary>
/// One pay type's share: what it billed, by kind of work, and what its parts
/// cost the dealership.
/// </summary>
public sealed record PayTypeBucket(
    string PayType,
    decimal LabourRevenue,
    decimal PartsRevenue,
    decimal SubletRevenue,

    /// <summary>Labour, parts and sublet revenue added together.</summary>
    decimal Revenue,

    /// <summary>What the parts billed here cost the dealership, frozen at invoicing.</summary>
    decimal PartsCost,

    /// <summary>Parts revenue less parts cost. See the caveat on <see cref="PayTypeReconciliation"/>.</summary>
    decimal PartsGrossProfit,

    /// <summary>How many lines make up this row.</summary>
    int LineCount,

    /// <summary>How many distinct repair orders contributed to this row.</summary>
    int OrderCount);

/// <summary>
/// The figures the trade expects that this system cannot honestly produce, and
/// what each one would need. Constants rather than prose so a screen can decide
/// how to word it.
/// </summary>
public static class UnmeasurableLabourFigure
{
    /// <summary>Hours produced ÷ hours available. Needs a shift or roster.</summary>
    public const string Efficiency = "Efficiency";

    /// <summary>
    /// Hours billed ÷ hours clocked. MEASURED SINCE 2026-09-16, when the
    /// technician clock arrived; kept here so the name still resolves and so
    /// the reason it used to be unmeasurable stays on the record.
    /// </summary>
    public const string Productivity = "Productivity";
}

/// <summary>
/// A job as another installation left it. Everything the service invoice prints
/// is here, because the round trip is judged on whether the paperwork comes out
/// the same.
/// </summary>
/// <param name="AmountDue">
/// What the source system said the customer owed. Evidence, not data — compared
/// with the figure this side computes and never stored.
/// </param>
public sealed record ImportedRepairOrder(
    Guid Id,
    RooftopId RooftopId,
    Guid CustomerId,
    Guid VehicleId,
    string Number,
    string Complaint,
    string Status,
    string Currency,
    int? OdometerReading,
    DateTimeOffset OpenedAt,
    DateTimeOffset? InvoicedAt,
    IReadOnlyList<ImportedServiceLine> Lines,
    decimal AmountDue);

public sealed record ImportedServiceLine(
    string Kind,
    string Description,
    decimal? Hours,
    decimal? Rate,
    decimal Amount,
    string PayType,
    string Authorization);

public sealed record ServiceLineView(
    Guid Id,
    string Kind,
    string Description,
    decimal? Hours,
    decimal? Rate,
    decimal Amount,
    string PayType,
    string Authorization,
    DateTimeOffset? AuthorizedAt,
    Guid? AuthorizedByUserId,
    string? AuthorizationNote,

    /// <summary>The catalogued job sold, when this line is one. Labour only.</summary>
    Guid? OpCodeId,

    /// <summary>The catalogue part sold, when this line draws from stock.</summary>
    Guid? PartId,
    decimal? PartQuantity,

    /// <summary>
    /// What the parts on this line cost, frozen at invoicing. Null until the job
    /// is invoiced, and null forever on a line that sells no stock — those are
    /// different things and the screen should not conflate them.
    /// </summary>
    decimal? Cost);

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
    decimal UnitAmount = 0m,

    /// <summary>
    /// The catalogue part this line sells, when it is one. Leaving it null is a
    /// legitimate choice, not a shortcut — a one-off item bought for a single job
    /// never enters the catalogue, and it still has to be billable. What it does
    /// mean is that nothing comes off a shelf and the line carries no cost.
    /// </summary>
    Guid? PartId = null,
    decimal? PartQuantity = null,

    /// <summary>
    /// The catalogued job this line sells. When given, its description and
    /// standard hours fill in whatever the caller left blank -- never the other
    /// way round. Labour lines only.
    /// </summary>
    Guid? OpCodeId = null,

    /// <summary>
    /// Who pays: "CustomerPay", "Warranty" or "Internal". Defaults to the
    /// customer, because that is the overwhelming majority and because a caller
    /// that says nothing should not silently produce a warranty claim.
    /// </summary>
    string PayType = "CustomerPay");

/// <summary>
/// What the customer said. <paramref name="Note"/> is how it was obtained —
/// "phoned 10:40, agreed" — which is the part that matters if it is ever
/// questioned.
/// </summary>
public sealed record LineAnswerRequest(bool Approved, string? Note = null);

public sealed record AssignTechnicianRequest(Guid? TechnicianUserId);

/// <summary>
/// Who is going on the clock. Named rather than taken from the caller, because
/// a foreman clocking their team on is ordinary — the audit records who did it.
/// </summary>
public sealed record ClockOnRequest(Guid TechnicianUserId);

public sealed record ClockOffRequest(Guid TechnicianUserId);

/// <summary>One stretch of time on this job, as a screen reads it.</summary>
public sealed record ClockingView(
    Guid Id,
    Guid TechnicianUserId,
    DateTimeOffset StartedAt,
    DateTimeOffset? StoppedAt,

    /// <summary>Zero while it is still running. See TechnicianClocking.Hours.</summary>
    decimal Hours,
    bool IsOpen,
    string? StoppedBecause);

public sealed record RepairOrderStatusChangeRequest(string Status, string? Note = null);
