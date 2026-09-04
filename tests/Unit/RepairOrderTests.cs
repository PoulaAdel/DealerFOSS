// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   RepairOrderTests — the authorization rule and the arithmetic, which are the two
//   things a workshop gets complaints about.
//
// Usage:
//   Runs with the normal test suite; no infrastructure required.
//
// Coding Instructions:
//   The unanswered-work rule matters most. Billing a customer for work they
//   were never asked about is a consumer-protection problem, not a bug, and
//   it happens by accident far more often than by dishonesty.

using FluentAssertions;
using DealerFOSS.Core;
using DealerFOSS.RepairOrders;

namespace DealerFOSS.UnitTests;

public sealed class RepairOrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_job_is_booked_with_that_moment_recorded()
    {
        var order = Open();

        order.Status.Should().Be(RepairOrderStatus.Booked);
        order.LinesAreOpen.Should().BeTrue();
        order.History.Should().ContainSingle().Which.ToStatus.Should().Be(RepairOrderStatus.Booked);
    }

    [Fact]
    public void A_job_needs_a_customer_a_car_a_number_and_a_complaint()
    {
        var noCustomer = () => RepairOrder.Open(
            Guid.NewGuid(), RooftopId.New(), Guid.Empty, Guid.NewGuid(), "RO-1", "Noise", "USD", Now);
        var noCar = () => RepairOrder.Open(
            Guid.NewGuid(), RooftopId.New(), Guid.NewGuid(), Guid.Empty, "RO-1", "Noise", "USD", Now);
        var noNumber = () => RepairOrder.Open(
            Guid.NewGuid(), RooftopId.New(), Guid.NewGuid(), Guid.NewGuid(), " ", "Noise", "USD", Now);

        // A job with no complaint cannot be checked against what was done to the
        // car, which is the whole point of writing it down.
        var noComplaint = () => RepairOrder.Open(
            Guid.NewGuid(), RooftopId.New(), Guid.NewGuid(), Guid.NewGuid(), "RO-1", "  ", "USD", Now);

        noCustomer.Should().Throw<ArgumentException>();
        noCar.Should().Throw<ArgumentException>();
        noNumber.Should().Throw<ArgumentException>();
        noComplaint.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Labour_multiplies_hours_by_the_rate()
    {
        var order = Open();

        order.AddLine(ServiceLineKind.Labour, "Diagnose the noise", 1.5m, 120m, 0m, Now, null);

        order.LabourTotal.Amount.Should().Be(180m);
        order.AmountDue.Amount.Should().Be(180m);
    }

    [Fact]
    public void Labour_needs_hours_and_a_rate_and_a_part_needs_neither()
    {
        var order = Open();

        var labourWithoutHours = () =>
            order.AddLine(ServiceLineKind.Labour, "Fit the part", null, 120m, 0m, Now, null);

        labourWithoutHours.Should().Throw<ArgumentException>();

        var part = order.AddLine(ServiceLineKind.Part, "Oil filter", null, null, 24.50m, Now, null);

        part.Amount.Should().Be(24.50m);
        part.Hours.Should().BeNull(because: "hours on a part would be a number nobody could explain");
    }

    [Fact]
    public void Work_booked_in_is_authorized_and_work_found_later_is_not()
    {
        var order = Open();

        // Booked in for it: the customer asked for this.
        var asked = order.AddLine(ServiceLineKind.Labour, "Full service", 1.5m, 120m, 0m, Now, null);

        order.ChangeStatus(RepairOrderStatus.InProgress, Now);

        // Found once the wheels were off: nobody has asked yet.
        var found = order.AddLine(ServiceLineKind.Part, "Front discs", null, null, 284m, Now, null);

        asked.Authorization.Should().Be(LineAuthorization.Authorized);
        found.Authorization.Should().Be(LineAuthorization.Pending);
        order.AwaitingAnswer.Should().ContainSingle().Which.Id.Should().Be(found.Id);
    }

    [Fact]
    public void A_job_with_unanswered_work_cannot_be_invoiced()
    {
        var order = InProgressWithFoundWork(out var found);

        order.ChangeStatus(RepairOrderStatus.Completed, Now);

        var invoice = () => order.ChangeStatus(RepairOrderStatus.Invoiced, Now);

        invoice.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{found.Description}*",
                because: "the refusal has to name the call somebody still owes, not just refuse");

        order.Status.Should().Be(RepairOrderStatus.Completed);
    }

    [Fact]
    public void Declining_work_lets_the_invoice_through_and_keeps_the_line_at_nil()
    {
        var order = InProgressWithFoundWork(out var found);

        order.AnswerLine(found.Id, approved: false, Now, Guid.NewGuid(), "Phoned 10:40, will do it next time.");
        order.ChangeStatus(RepairOrderStatus.Completed, Now);
        order.ChangeStatus(RepairOrderStatus.Invoiced, Now);

        order.Status.Should().Be(RepairOrderStatus.Invoiced);
        order.AmountDue.Amount.Should().Be(180m, because: "declined work is not billed");

        // The record of having offered survives, which is the point of declining
        // rather than deleting.
        order.Lines.Should().Contain(l => l.Id == found.Id);
        order.Lines.Single(l => l.Id == found.Id).Authorization
            .Should().Be(LineAuthorization.Declined);
    }

    [Fact]
    public void Authorizing_work_puts_it_on_the_bill()
    {
        var order = InProgressWithFoundWork(out var found);
        var advisor = Guid.NewGuid();

        order.AnswerLine(found.Id, approved: true, Now, advisor, "Phoned 10:40, agreed.");

        order.AmountDue.Amount.Should().Be(464m);
        var line = order.Lines.Single(l => l.Id == found.Id);
        line.AuthorizedByUserId.Should().Be(advisor);
        line.AuthorizationNote.Should().Be("Phoned 10:40, agreed.");
    }

    [Fact]
    public void A_line_is_only_answered_once()
    {
        var order = InProgressWithFoundWork(out var found);

        order.AnswerLine(found.Id, approved: true, Now, null, null);
        var again = () => order.AnswerLine(found.Id, approved: false, Now.AddHours(1), null, null);

        // Overwriting would move the timestamp off the conversation that actually
        // happened.
        again.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_completed_job_freezes_its_work_until_it_is_sent_back()
    {
        var order = Open();
        order.AddLine(ServiceLineKind.Labour, "Full service", 1.5m, 120m, 0m, Now, null);
        order.ChangeStatus(RepairOrderStatus.InProgress, Now);
        order.ChangeStatus(RepairOrderStatus.Completed, Now);

        var addAnyway = () => order.AddLine(ServiceLineKind.Part, "Wiper blades", null, null, 18m, Now, null);
        addAnyway.Should().Throw<InvalidOperationException>();

        order.ChangeStatus(RepairOrderStatus.InProgress, Now, note: "Wipers were on the job card too.");
        order.AddLine(ServiceLineKind.Part, "Wiper blades", null, null, 18m, Now, null);

        order.AmountDue.Amount.Should().Be(198m);
    }

    [Fact]
    public void A_job_with_nothing_on_it_cannot_be_completed()
    {
        var order = Open();
        order.ChangeStatus(RepairOrderStatus.InProgress, Now);

        var complete = () => order.ChangeStatus(RepairOrderStatus.Completed, Now);

        complete.Should().Throw<InvalidOperationException>(
            because: "a customer handed a finished job that says nothing happened will ask why");
    }

    [Fact]
    public void An_invoiced_job_is_finished()
    {
        var order = Open();
        order.AddLine(ServiceLineKind.Labour, "Full service", 1m, 120m, 0m, Now, null);
        order.ChangeStatus(RepairOrderStatus.InProgress, Now);
        order.ChangeStatus(RepairOrderStatus.Completed, Now);
        order.ChangeStatus(RepairOrderStatus.Invoiced, Now);

        RepairOrderStatusRules.MovesFrom(RepairOrderStatus.Invoiced).Should().BeEmpty();

        var reopen = () => order.ChangeStatus(RepairOrderStatus.InProgress, Now);
        reopen.Should().Throw<InvalidOperationException>().WithMessage("*finished*");
    }

    [Fact]
    public void The_history_records_the_total_at_the_moment_of_each_move()
    {
        var order = Open();
        order.AddLine(ServiceLineKind.Labour, "Full service", 1m, 120m, 0m, Now, null);
        order.ChangeStatus(RepairOrderStatus.InProgress, Now);
        order.AddLine(ServiceLineKind.Part, "Filter", null, null, 30m, Now, null);
        order.AnswerLine(order.AwaitingAnswer[0].Id, approved: true, Now, null, null);
        order.ChangeStatus(RepairOrderStatus.Completed, Now);

        var moves = order.History.ToList();

        // In progress at 120, completed at 150: an invoice records what was
        // invoiced rather than whatever the lines say today.
        moves.Single(h => h.ToStatus == RepairOrderStatus.InProgress).AmountAtChange.Should().Be(120m);
        moves.Single(h => h.ToStatus == RepairOrderStatus.Completed).AmountAtChange.Should().Be(150m);
    }

    [Fact]
    public void Totals_are_split_by_what_kind_of_work_it_was()
    {
        var order = Open();
        order.AddLine(ServiceLineKind.Labour, "Full service", 1.5m, 120m, 0m, Now, null);
        order.AddLine(ServiceLineKind.Part, "Oil and filter", null, null, 68.40m, Now, null);
        order.AddLine(ServiceLineKind.Sublet, "Wheel alignment", null, null, 55m, Now, null);

        order.LabourTotal.Amount.Should().Be(180m);
        order.PartsTotal.Amount.Should().Be(68.40m);
        order.SubletTotal.Amount.Should().Be(55m);
        order.AmountDue.Amount.Should().Be(303.40m);
    }

    private static RepairOrder Open() => RepairOrder.Open(
        Guid.NewGuid(),
        RooftopId.New(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        "RO-1001",
        "Squealing from the front when braking.",
        "USD",
        Now,
        odometerReading: 48_210);

    /// <summary>A job under way with 180 of authorized labour and 284 found but unanswered.</summary>
    private static RepairOrder InProgressWithFoundWork(out ServiceLine found)
    {
        var order = Open();
        order.AddLine(ServiceLineKind.Labour, "Full service", 1.5m, 120m, 0m, Now, null);
        order.ChangeStatus(RepairOrderStatus.InProgress, Now);
        found = order.AddLine(ServiceLineKind.Part, "Front discs and pads", null, null, 284m, Now, null);
        return order;
    }
}
