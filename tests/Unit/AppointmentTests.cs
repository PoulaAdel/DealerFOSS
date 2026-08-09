// AppointmentTests — the promise, and the one rule that makes a diary reconcile.
//
// Use:  runs with the normal test suite; no infrastructure required.
// Edit: the rule worth guarding hardest is that a booking becomes a job exactly
//       once. A workshop whose diary can produce two jobs for one arrival will
//       bill one visit twice, and the mistake is invisible until a customer
//       complains — which is the definition of a control worth a test.
//
//       The three endings are guarded separately on purpose. Collapsing NoShow
//       into Cancelled would still pass a test that only checked "it closed".

using FluentAssertions;
using DealerFOSS.Core;
using DealerFOSS.RepairOrders;

namespace DealerFOSS.UnitTests;

public sealed class AppointmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Tuesday = new(2026, 8, 11, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_booking_is_scheduled_and_has_no_job_behind_it()
    {
        var booking = Book();

        booking.Status.Should().Be(AppointmentStatus.Scheduled);
        booking.IsOpen.Should().BeTrue();

        // The job is created when the car is actually there. A workshop that
        // opens jobs for cars that never arrive cannot say what it did today.
        booking.RepairOrderId.Should().BeNull();
        booking.ArrivedAt.Should().BeNull();
    }

    [Fact]
    public void A_booking_needs_a_customer_a_car_and_a_reason()
    {
        var noCustomer = () => Appointment.Book(
            Guid.NewGuid(), RooftopId.New(), Guid.Empty, Guid.NewGuid(), Tuesday, "Service", Now);
        var noCar = () => Appointment.Book(
            Guid.NewGuid(), RooftopId.New(), Guid.NewGuid(), Guid.Empty, Tuesday, "Service", Now);
        var noReason = () => Appointment.Book(
            Guid.NewGuid(), RooftopId.New(), Guid.NewGuid(), Guid.NewGuid(), Tuesday, "  ", Now);

        noCustomer.Should().Throw<ArgumentException>();
        noCar.Should().Throw<ArgumentException>();
        noReason.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_booking_in_the_past_is_refused_because_it_is_always_a_typo()
    {
        var yesterday = () => Appointment.Book(
            Guid.NewGuid(), RooftopId.New(), Guid.NewGuid(), Guid.NewGuid(),
            Now.AddDays(-1), "Service", Now);

        // Recording work that already happened is what a repair order is for, and
        // the refusal says so rather than just rejecting the date.
        yesterday.Should().Throw<ArgumentException>()
            .WithMessage("*already passed*");
    }

    [Fact]
    public void Arriving_records_the_job_the_car_became()
    {
        var booking = Book();
        var jobId = Guid.NewGuid();

        booking.Arrive(jobId, Tuesday);

        booking.Status.Should().Be(AppointmentStatus.Arrived);
        booking.RepairOrderId.Should().Be(jobId);
        booking.ArrivedAt.Should().Be(Tuesday);
        booking.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void A_car_that_arrives_once_produces_one_job()
    {
        // The reconciliation invariant behind roadmap I5: "Appointment -> RO
        // visibility reconciles to its source". A second job against one arrival
        // is a customer billed twice for one visit.
        var booking = Book();
        var first = Guid.NewGuid();
        booking.Arrive(first, Tuesday);

        var again = () => booking.Arrive(Guid.NewGuid(), Tuesday.AddMinutes(5));

        // The refusal names the job that already exists, because whoever reads it
        // is standing at a counter and needs to know which job to open.
        again.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{first}*");

        booking.RepairOrderId.Should().Be(first);
    }

    [Fact]
    public void A_no_show_and_a_cancellation_are_different_facts()
    {
        var silent = Book();
        var rang = Book();

        silent.MissedIt("No answer on either number.");
        rang.Cancel("Customer rang, car sold.");

        // One is silence and the other is the customer telling you. A diary that
        // collapses them cannot tell a manager who to ring the day before.
        silent.Status.Should().Be(AppointmentStatus.NoShow);
        rang.Status.Should().Be(AppointmentStatus.Cancelled);
        silent.Outcome.Should().Be("No answer on either number.");
    }

    [Fact]
    public void A_closed_booking_cannot_be_arrived_moved_or_closed_again()
    {
        var cancelled = Book();
        cancelled.Cancel(null);

        var arrive = () => cancelled.Arrive(Guid.NewGuid(), Tuesday);
        var move = () => cancelled.Reschedule(Tuesday.AddDays(1), Now, null);
        var closeAgain = () => cancelled.MissedIt(null);

        arrive.Should().Throw<InvalidOperationException>();
        move.Should().Throw<InvalidOperationException>();
        closeAgain.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_booking_can_be_moved_and_re_estimated_while_it_is_still_a_booking()
    {
        var booking = Book(estimatedHours: 1m);

        booking.Reschedule(Tuesday.AddDays(2), Now, 3.5m);

        booking.ScheduledFor.Should().Be(Tuesday.AddDays(2));
        booking.EstimatedHours.Should().Be(3.5m);
    }

    [Fact]
    public void Moving_a_booking_into_the_past_is_refused_too()
    {
        var booking = Book();

        var backwards = () => booking.Reschedule(Now.AddHours(-1), Now, null);

        backwards.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void An_estimate_is_optional_but_never_negative()
    {
        var unestimated = Book(estimatedHours: null);
        var negative = () => Appointment.Book(
            Guid.NewGuid(), RooftopId.New(), Guid.NewGuid(), Guid.NewGuid(),
            Tuesday, "Service", Now, estimatedHours: -1m);

        // Nobody estimated is honest and different from estimating zero, so the
        // column stays nullable rather than defaulting.
        unestimated.EstimatedHours.Should().BeNull();
        negative.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Only_a_booking_still_expected_counts_against_the_day()
    {
        // Once a car arrives its hours belong to the job. Counting both would show
        // a workshop as twice as busy as it is, on the one screen whose whole
        // purpose is answering "can I fit this in".
        AppointmentStatusRules.CountsTowardLoad(AppointmentStatus.Scheduled).Should().BeTrue();
        AppointmentStatusRules.CountsTowardLoad(AppointmentStatus.Arrived).Should().BeFalse();
        AppointmentStatusRules.CountsTowardLoad(AppointmentStatus.NoShow).Should().BeFalse();
        AppointmentStatusRules.CountsTowardLoad(AppointmentStatus.Cancelled).Should().BeFalse();
    }

    private static Appointment Book(decimal? estimatedHours = 1.5m) => Appointment.Book(
        Guid.NewGuid(),
        RooftopId.New(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Tuesday,
        "Service and MOT",
        Now,
        estimatedHours);
}
