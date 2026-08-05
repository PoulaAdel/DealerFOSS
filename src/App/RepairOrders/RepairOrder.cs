// RepairOrder — one car in the workshop, the work done to it, and the bill.
//
// Use:  RepairOrder.Open(...), AddLine while the lines are open, Answer to record
//       what the customer said, then ChangeStatus to move it along.
// Edit: three rules here are not conveniences.
//
//       A job cannot be invoiced while any line is still Pending. That is the
//       control the capability exists to hold — billing work nobody agreed to pay
//       for is the complaint that ends up in front of a trading standards officer,
//       and it happens by accident far more often than by dishonesty.
//
//       The lines freeze the moment the job is Completed. Changing them means
//       sending it back to InProgress, which is a recorded move.
//
//       A job belongs to ONE rooftop and that is a permission boundary
//       (doc 04 §1). The customer and the car are shared across the organization;
//       the job is not.
//
//       The car here is a VEHICLE, not an inventory unit. A customer's own car is
//       not on anybody's lot, and modelling service against stock would make the
//       whole capability unusable the day after the warranty runs out.

using DealerFOSS.Core;

namespace DealerFOSS.RepairOrders;

public sealed class RepairOrder : AuditableEntity
{
    private readonly List<ServiceLine> _lines = [];
    private readonly List<RepairOrderStatusChange> _history = [];

    public Guid Id { get; private set; }

    /// <summary>The workshop doing the job. A permission boundary.</summary>
    public RooftopId RooftopId { get; private set; }

    public Guid CustomerId { get; private set; }

    /// <summary>The car itself — owned by the customer, not held in stock.</summary>
    public Guid VehicleId { get; private set; }

    /// <summary>Sequential per rooftop, and what everybody actually says out loud.</summary>
    public string Number { get; private set; } = string.Empty;

    /// <summary>ISO 4217. Every amount on the job is in this currency.</summary>
    public string Currency { get; private set; } = string.Empty;

    public RepairOrderStatus Status { get; private set; }

    /// <summary>What the customer said was wrong when they brought it in.</summary>
    public string Complaint { get; private set; } = string.Empty;

    /// <summary>Miles on the clock at booking. Service history is worthless without it.</summary>
    public int? OdometerReading { get; private set; }

    /// <summary>Who is looking after the customer.</summary>
    public Guid? AdvisorUserId { get; private set; }

    /// <summary>Who is doing the work.</summary>
    public Guid? TechnicianUserId { get; private set; }

    public DateTimeOffset? InvoicedAt { get; private set; }

    public IReadOnlyList<ServiceLine> Lines => _lines;

    public IReadOnlyList<RepairOrderStatusChange> History => _history;

    /// <summary>Labour, at hours times rate, for everything not declined.</summary>
    public Money LabourTotal => TotalOf(ServiceLineKind.Labour);

    public Money PartsTotal => TotalOf(ServiceLineKind.Part);

    public Money SubletTotal => TotalOf(ServiceLineKind.Sublet);

    /// <summary>What the customer owes: every line the customer did not decline.</summary>
    public Money AmountDue => new(_lines.Sum(l => l.Amount), Currency);

    public bool LinesAreOpen => RepairOrderStatusRules.LinesAreOpen(Status);

    /// <summary>
    /// Work found but not yet put to the customer. A screen shows this as the
    /// thing somebody has to go and do, and it is what blocks the invoice.
    /// </summary>
    public IReadOnlyList<ServiceLine> AwaitingAnswer =>
        _lines.Where(l => l.Authorization == LineAuthorization.Pending).ToList();

    private RepairOrder()
    {
    }

    /// <summary>
    /// Books a car in. It begins Booked with no work on it — the complaint is what
    /// the customer said, and the lines are what somebody decides to do about it.
    /// </summary>
    public static RepairOrder Open(
        Guid id,
        RooftopId rooftopId,
        Guid customerId,
        Guid vehicleId,
        string number,
        string complaint,
        string currency,
        DateTimeOffset openedAt,
        int? odometerReading = null,
        Guid? advisorUserId = null)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("A job needs a customer.", nameof(customerId));
        }

        if (vehicleId == Guid.Empty)
        {
            throw new ArgumentException("A job needs a car.", nameof(vehicleId));
        }

        if (string.IsNullOrWhiteSpace(number))
        {
            throw new ArgumentException("A job needs a number.", nameof(number));
        }

        if (string.IsNullOrWhiteSpace(complaint))
        {
            throw new ArgumentException(
                "Record what the customer said is wrong. A job with no complaint cannot be checked against what was done.",
                nameof(complaint));
        }

        if (odometerReading is < 0)
        {
            throw new ArgumentException("An odometer reading cannot be negative.", nameof(odometerReading));
        }

        // Constructing a Money proves the currency is a real ISO code before it
        // reaches the database.
        var currencyCheck = Money.Zero(currency);

        var order = new RepairOrder
        {
            Id = id,
            RooftopId = rooftopId,
            CustomerId = customerId,
            VehicleId = vehicleId,
            Number = number.Trim(),
            Complaint = complaint.Trim(),
            Currency = currencyCheck.Currency,
            Status = RepairOrderStatus.Booked,
            OdometerReading = odometerReading,
            AdvisorUserId = advisorUserId,
        };

        order._history.Add(new RepairOrderStatusChange(
            Guid.NewGuid(), id, null, RepairOrderStatus.Booked, openedAt, advisorUserId, null, 0m));

        return order;
    }

    /// <summary>
    /// Adds a piece of work. Anything added while the car is still Booked is what
    /// the customer came in for and is authorized on arrival; anything found once
    /// work has started has to be put to them, so it starts Pending.
    /// </summary>
    public ServiceLine AddLine(
        ServiceLineKind kind,
        string description,
        decimal? hours,
        decimal? rate,
        decimal unitAmount,
        DateTimeOffset addedAt,
        Guid? addedByUserId)
    {
        EnsureLinesAreOpen();

        var authorizedOnArrival = Status == RepairOrderStatus.Booked;

        var line = new ServiceLine(
            Guid.NewGuid(),
            Id,
            kind,
            description,
            hours,
            rate,
            unitAmount,
            authorizedOnArrival ? LineAuthorization.Authorized : LineAuthorization.Pending,
            authorizedOnArrival ? addedAt : null,
            authorizedOnArrival ? addedByUserId : null);

        _lines.Add(line);
        return line;
    }

    /// <summary>Takes a line off a job that has not been billed yet.</summary>
    public void RemoveLine(Guid lineId)
    {
        EnsureLinesAreOpen();

        var line = _lines.SingleOrDefault(l => l.Id == lineId)
            ?? throw new InvalidOperationException("That line is not on this job.");

        _lines.Remove(line);
    }

    /// <summary>
    /// Records the customer's answer to a piece of work found during the job.
    /// </summary>
    public void AnswerLine(
        Guid lineId,
        bool approved,
        DateTimeOffset answeredAt,
        Guid? answeredByUserId,
        string? note)
    {
        var line = _lines.SingleOrDefault(l => l.Id == lineId)
            ?? throw new InvalidOperationException("That line is not on this job.");

        line.Answer(approved, answeredAt, answeredByUserId, note);
    }

    /// <summary>Records who is doing the work.</summary>
    public void AssignTechnician(Guid? technicianUserId)
    {
        if (RepairOrderStatusRules.IsClosed(Status))
        {
            throw new InvalidOperationException($"A {Status} job cannot be reassigned.");
        }

        TechnicianUserId = technicianUserId;
    }

    /// <summary>
    /// Moves the job on and records the move together with the billable total at
    /// that moment, so an invoice records the number that was invoiced.
    /// </summary>
    public void ChangeStatus(
        RepairOrderStatus next,
        DateTimeOffset occurredAt,
        Guid? changedByUserId = null,
        string? note = null)
    {
        if (!RepairOrderStatusRules.CanMove(Status, next))
        {
            var options = RepairOrderStatusRules.MovesFrom(Status);
            throw new InvalidOperationException(
                options.Count == 0
                    ? $"A {Status} job is finished."
                    : $"A {Status} job cannot become {next}. It can become: {string.Join(", ", options)}.");
        }

        if (next == RepairOrderStatus.Completed)
        {
            EnsureThereIsWork();
        }

        if (next == RepairOrderStatus.Invoiced)
        {
            EnsureNothingIsUnanswered();
        }

        _history.Add(new RepairOrderStatusChange(
            Guid.NewGuid(), Id, Status, next, occurredAt, changedByUserId, note, AmountDue.Amount));

        Status = next;

        if (next == RepairOrderStatus.Invoiced)
        {
            InvoicedAt = occurredAt;
        }
    }

    private Money TotalOf(ServiceLineKind kind) =>
        new(_lines.Where(l => l.Kind == kind).Sum(l => l.Amount), Currency);

    private void EnsureLinesAreOpen()
    {
        if (!LinesAreOpen)
        {
            throw new InvalidOperationException(
                $"The work on a {Status} job is frozen. Send it back to InProgress to change it.");
        }
    }

    /// <summary>
    /// A job with nothing on it has not been done. Catching it here means a
    /// customer is never handed a completed job that says nothing happened.
    /// </summary>
    private void EnsureThereIsWork()
    {
        if (_lines.Count == 0)
        {
            throw new InvalidOperationException(
                "Record what was done before completing the job.");
        }
    }

    /// <summary>
    /// The rule the whole capability exists for. Work found mid-job that nobody
    /// has put to the customer cannot reach an invoice — not because the amount
    /// would be wrong, but because nobody agreed to pay it.
    /// </summary>
    /// <remarks>
    /// Checked on the entity rather than only in the service, so a background job
    /// or an import cannot route around it. Declining is a perfectly good answer:
    /// the line stays on the record at nil, which is what makes "we did offer"
    /// provable a year later.
    /// </remarks>
    private void EnsureNothingIsUnanswered()
    {
        var pending = AwaitingAnswer;
        if (pending.Count > 0)
        {
            throw new InvalidOperationException(
                $"{pending.Count} line(s) are still waiting on the customer: "
                + string.Join("; ", pending.Select(l => l.Description))
                + ". Record what they said before invoicing.");
        }
    }
}
