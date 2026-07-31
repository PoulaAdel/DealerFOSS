// LeadTests — the lead life-cycle rules, which are the part most likely to be
// broken by a well-meaning change.
//
// Use:  runs with the normal test suite; no infrastructure required.
// Edit: the reopen path matters most. A lost lead coming back is a real and
//       common event, and the alternative — capturing a second record — loses
//       the history of the first attempt.

using FluentAssertions;
using OpenDealer360.Core;
using OpenDealer360.Leads;

namespace OpenDealer360.UnitTests;

public sealed class LeadTests
{
    [Fact]
    public void A_captured_lead_starts_new_with_that_moment_recorded()
    {
        var lead = Capture();

        lead.Status.Should().Be(LeadStatus.New);
        lead.IsOpen.Should().BeTrue();
        lead.History.Should().ContainSingle().Which.ToStatus.Should().Be(LeadStatus.New);
        lead.History[0].FromStatus.Should().BeNull(
            because: "an arriving enquiry did not come from another status");
    }

    [Fact]
    public void A_lead_belongs_to_the_rooftop_that_took_the_enquiry()
    {
        var rooftop = RooftopId.New();

        Capture(rooftop).RooftopId.Should().Be(rooftop);
    }

    [Fact]
    public void A_lead_needs_a_customer()
    {
        var orphan = () => Lead.Capture(
            Guid.NewGuid(), RooftopId.New(), Guid.Empty, LeadSource.WalkIn, DateTimeOffset.UtcNow);

        orphan.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Every_move_is_added_to_the_history_with_what_was_said()
    {
        var lead = Capture();
        var now = DateTimeOffset.UtcNow;

        lead.ChangeStatus(LeadStatus.Working, now, note: "Left a voicemail.");
        lead.ChangeStatus(LeadStatus.Appointment, now.AddDays(1), note: "Coming in Saturday.");

        lead.Status.Should().Be(LeadStatus.Appointment);
        lead.History.Should().HaveCount(3);
        lead.History[1].Note.Should().Be("Left a voicemail.");
        lead.History[2].FromStatus.Should().Be(LeadStatus.Working);
    }

    [Fact]
    public void A_lead_cannot_jump_straight_from_new_to_won()
    {
        var lead = Capture();

        var jump = () => lead.ChangeStatus(LeadStatus.Won, DateTimeOffset.UtcNow);

        jump.Should().Throw<InvalidOperationException>()
            .WithMessage("*Working*", because: "the message must say what is possible instead");
    }

    [Fact]
    public void A_won_lead_is_finished()
    {
        var lead = Capture();
        lead.ChangeStatus(LeadStatus.Working, DateTimeOffset.UtcNow);
        lead.ChangeStatus(LeadStatus.Won, DateTimeOffset.UtcNow);

        var reopen = () => lead.ChangeStatus(LeadStatus.Working, DateTimeOffset.UtcNow);

        reopen.Should().Throw<InvalidOperationException>().WithMessage("*new enquiry*");
        lead.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void A_lost_lead_can_come_back_and_stops_counting_as_closed()
    {
        // They were not ready in March and walked in again in June. That is the
        // same enquiry continuing, not a new one.
        var lead = Capture();
        var march = DateTimeOffset.UtcNow.AddDays(-90);
        lead.ChangeStatus(LeadStatus.Working, march);
        lead.ChangeStatus(LeadStatus.Lost, march.AddDays(5), note: "Went quiet.");

        lead.IsOpen.Should().BeFalse();
        lead.ClosedAt.Should().NotBeNull();

        lead.ChangeStatus(LeadStatus.Working, DateTimeOffset.UtcNow, note: "Rang back.");

        lead.IsOpen.Should().BeTrue();
        lead.ClosedAt.Should().BeNull(because: "it is being worked again, so aging restarts");
        lead.History.Should().HaveCount(4, because: "the first attempt stays on the record");
    }

    [Fact]
    public void Closing_a_lead_records_when_it_closed()
    {
        var lead = Capture();
        var closedAt = DateTimeOffset.UtcNow;

        lead.ChangeStatus(LeadStatus.Lost, closedAt, note: "Bought elsewhere.");

        lead.ClosedAt.Should().Be(closedAt);
    }

    [Fact]
    public void Assigning_a_lead_is_not_a_status_change()
    {
        // Who owns it and how far along it is are different questions; conflating
        // them would put noise in the history and lose the real moves.
        var lead = Capture();
        var salesperson = Guid.NewGuid();

        lead.AssignTo(salesperson);

        lead.AssignedToUserId.Should().Be(salesperson);
        lead.Status.Should().Be(LeadStatus.New);
        lead.History.Should().ContainSingle();
    }

    [Fact]
    public void A_lead_can_be_taken_back_off_a_salesperson()
    {
        var lead = Capture();
        lead.AssignTo(Guid.NewGuid());

        lead.AssignTo(null);

        lead.AssignedToUserId.Should().BeNull();
    }

    [Fact]
    public void Most_enquiries_are_not_about_one_specific_car()
    {
        Capture().VehicleOfInterestId.Should().BeNull(
            because: "\"something around 20k\" is the normal enquiry, not a VIN");
    }

    private static Lead Capture(RooftopId? rooftop = null) =>
        Lead.Capture(
            Guid.NewGuid(),
            rooftop ?? RooftopId.New(),
            Guid.NewGuid(),
            LeadSource.WalkIn,
            DateTimeOffset.UtcNow);
}
