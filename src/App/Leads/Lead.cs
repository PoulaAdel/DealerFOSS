// Lead — somebody who might buy a car, and the record of chasing them.
//
// Use:  Lead.Capture(...), then ChangeStatus as the conversation progresses.
// Edit: a lead belongs to ONE rooftop, and that is a permission boundary
//       (doc 04 §1). A salesperson at one location must not see another
//       location's enquiries — the branch manager's numbers depend on it, and so
//       does the customer's experience of not being called by two salespeople.
//       The customer themselves is organization-shared; only the enquiry is
//       local. Do not "helpfully" widen the rooftop filter.

using OpenDealer360.Core;

namespace OpenDealer360.Leads;

public sealed class Lead : AuditableEntity
{
    private readonly List<LeadStatusChange> _history = [];

    public Guid Id { get; private set; }

    /// <summary>The rooftop working this enquiry. A permission boundary.</summary>
    public RooftopId RooftopId { get; private set; }

    /// <summary>
    /// The customer enquiring. Organization-shared, so this is a plain reference —
    /// the same person may have an open lead at two rooftops, which is a real
    /// situation the dealership needs to be able to see rather than prevent.
    /// </summary>
    public Guid CustomerId { get; private set; }

    /// <summary>
    /// The vehicle they asked about, when they asked about a specific one.
    /// Null for "looking for something around £20,000" — which is most of them.
    /// </summary>
    public Guid? VehicleOfInterestId { get; private set; }

    public LeadSource Source { get; private set; }

    public LeadStatus Status { get; private set; }

    /// <summary>The salesperson chasing it. Null means nobody has picked it up.</summary>
    public Guid? AssignedToUserId { get; private set; }

    /// <summary>What they actually asked for, in their words where possible.</summary>
    public string? Enquiry { get; private set; }

    public DateTimeOffset CapturedAt { get; private set; }

    /// <summary>When it stopped being worked. Null while it is still open.</summary>
    public DateTimeOffset? ClosedAt { get; private set; }

    public bool IsOpen => !LeadStatusRules.IsClosed(Status);

    public IReadOnlyList<LeadStatusChange> History => _history;

    private Lead()
    {
    }

    /// <summary>
    /// Records a new enquiry. It starts as <see cref="LeadStatus.New"/> even when
    /// the salesperson is standing in front of the customer — the move to Working
    /// is what says somebody has taken responsibility, and that moment is worth
    /// having on the record.
    /// </summary>
    public static Lead Capture(
        Guid id,
        RooftopId rooftopId,
        Guid customerId,
        LeadSource source,
        DateTimeOffset capturedAt,
        Guid? vehicleOfInterestId = null,
        Guid? assignedToUserId = null,
        string? enquiry = null,
        Guid? capturedByUserId = null)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("A lead needs a customer.", nameof(customerId));
        }

        var lead = new Lead
        {
            Id = id,
            RooftopId = rooftopId,
            CustomerId = customerId,
            VehicleOfInterestId = vehicleOfInterestId,
            Source = source,
            Status = LeadStatus.New,
            AssignedToUserId = assignedToUserId,
            Enquiry = Blank(enquiry),
            CapturedAt = capturedAt,
        };

        lead._history.Add(new LeadStatusChange(
            Guid.NewGuid(), id, null, LeadStatus.New, capturedAt, capturedByUserId, Blank(enquiry)));

        return lead;
    }

    /// <summary>
    /// Moves the lead on and records why. Refuses a move the life cycle does not
    /// allow, so a lead cannot leave a closed state it is not meant to leave, or
    /// change to the status it is already in.
    /// </summary>
    public void ChangeStatus(
        LeadStatus next,
        DateTimeOffset occurredAt,
        Guid? changedByUserId = null,
        string? note = null)
    {
        if (!LeadStatusRules.CanMove(Status, next))
        {
            var options = LeadStatusRules.MovesFrom(Status);
            throw new InvalidOperationException(
                options.Count == 0
                    ? $"A {Status} lead is finished. Capture a new enquiry instead."
                    : $"A {Status} lead cannot become {next}. It can become: {string.Join(", ", options)}.");
        }

        _history.Add(new LeadStatusChange(
            Guid.NewGuid(), Id, Status, next, occurredAt, changedByUserId, note));

        Status = next;

        // Reopening a lost lead clears the closing date: it is open again, and
        // aging should count from now rather than from the original enquiry.
        if (LeadStatusRules.IsClosed(next))
        {
            ClosedAt = occurredAt;
        }
        else
        {
            ClosedAt = null;
        }
    }

    /// <summary>
    /// Hands the lead to a salesperson, or takes it back off them when null.
    /// Deliberately not a status change: who owns it and how far along it is are
    /// different questions.
    /// </summary>
    public void AssignTo(Guid? userId) => AssignedToUserId = userId;

    public void SetVehicleOfInterest(Guid? vehicleId) => VehicleOfInterestId = vehicleId;

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
