// IntegrationWindowTests — the cursor rules, which are the expensive ones to get wrong.
//
// Use:  runs with the normal test suite; no infrastructure required.
// Edit: the tests worth guarding hardest are the two that refuse to move the
//       cursor. A cursor advanced across a period the provider never served
//       produces no exception, no missing-record warning, and no failed run —
//       the hole is found by a reconciliation months later, when the provider
//       no longer has the data. There is no louder signal available, so the
//       refusal has to be the signal.

using FluentAssertions;
using DealerFOSS.Integrations;

namespace DealerFOSS.UnitTests;

public sealed class IntegrationWindowTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 12, 6, 0, 0, TimeSpan.Zero);

    private static DateRange Days(int from, int to) =>
        new(Now.AddDays(-from), Now.AddDays(-to));

    // --- Planning ---------------------------------------------------------

    [Fact]
    public void A_want_longer_than_the_chunk_limit_is_split_and_the_pieces_join_up()
    {
        var policy = new FetchWindowPolicy(true, TimeSpan.FromDays(183), null, TimeSpan.Zero);

        var plan = FetchWindow.Plan(policy, Days(400, 0), Now);

        plan.Slices.Should().HaveCount(3);

        // No gaps and no overlaps between the chunks: each one starts exactly
        // where the last ended. A one-day seam here loses a day of deals.
        for (var i = 1; i < plan.Slices.Count; i++)
        {
            plan.Slices[i].Range!.Value.Start.Should().Be(plan.Slices[i - 1].Range!.Value.End);
        }

        plan.Slices[0].Range!.Value.Start.Should().Be(Now.AddDays(-400));
        plan.Slices[^1].Range!.Value.End.Should().Be(Now);
    }

    [Fact]
    public void A_want_older_than_the_provider_allows_is_clamped_and_says_so()
    {
        var policy = new FetchWindowPolicy(true, null, TimeSpan.FromDays(30), TimeSpan.Zero);

        var plan = FetchWindow.Plan(policy, Days(365, 0), Now);

        plan.WasClamped.Should().BeTrue("the provider will not go back a year");
        plan.ClampedTo!.Value.Start.Should().Be(Now.AddDays(-30));

        // The want is kept alongside, so an operator can see what was not asked
        // for rather than only what was.
        plan.Wanted.Start.Should().Be(Now.AddDays(-365));
    }

    [Fact]
    public void An_endpoint_that_takes_no_dates_gets_one_request_and_admits_it_knows_nothing()
    {
        var plan = FetchWindow.Plan(
            new FetchWindowPolicy(false, null, null, TimeSpan.Zero), Days(7, 0), Now);

        plan.Slices.Should().ContainSingle();

        // Both nulls matter. A range here would be a fiction: the provider
        // decides what "recent" means and never tells us.
        plan.Slices[0].Range.Should().BeNull();
        plan.ClampedTo.Should().BeNull();
    }

    [Fact]
    public void A_settlement_delay_shifts_the_whole_window_back()
    {
        // This dealership posts its paperwork two days late, so a window ending
        // now returns nothing. Per dealership, not per provider — stores on the
        // same DMS post at different speeds.
        var policy = new FetchWindowPolicy(true, null, null, TimeSpan.FromDays(2));

        var plan = FetchWindow.Plan(policy, Days(5, 0), Now);

        plan.ClampedTo!.Value.End.Should().Be(Now.AddDays(-2));
        plan.ClampedTo!.Value.Start.Should().Be(Now.AddDays(-7));
    }

    [Fact]
    public void A_window_that_the_limits_leave_empty_produces_no_requests_rather_than_a_failure()
    {
        // Asking for a period entirely older than the provider's lookback is a
        // legitimate no-op, not an error. Treating it as a failure would mark a
        // healthy connector broken on its first backfill.
        var policy = new FetchWindowPolicy(true, null, TimeSpan.FromDays(30), TimeSpan.Zero);

        var plan = FetchWindow.Plan(policy, Days(365, 200), Now);

        plan.Slices.Should().BeEmpty();
    }

    // --- The cursor -------------------------------------------------------

    [Fact]
    public void A_provider_that_reports_what_it_served_moves_the_cursor_to_the_end_of_it()
    {
        var cursor = Now.AddDays(-10);

        var advanced = Cursor.Advance(cursor, new DateRange(cursor, Now.AddDays(-3)));

        advanced.IsSuccess.Should().BeTrue();
        advanced.Value.Should().Be(Now.AddDays(-3));
    }

    [Fact]
    public void A_provider_that_says_nothing_leaves_the_cursor_where_it_was()
    {
        var advanced = Cursor.Advance(Now.AddDays(-10), covered: null);

        advanced.IsFailure.Should().BeTrue();
        advanced.Error.Code.Should().Be("integration.coverage_unknown");
    }

    [Fact]
    public void A_provider_that_skipped_the_start_does_not_get_to_bury_the_gap()
    {
        // The cursor is at day -10 and the provider served from day -7. The
        // three days between were never fetched. Moving the cursor to the end
        // of what came back makes them unreachable, silently, forever.
        var advanced = Cursor.Advance(Now.AddDays(-10), new DateRange(Now.AddDays(-7), Now));

        advanced.IsFailure.Should().BeTrue();
        advanced.Error.Code.Should().Be("integration.coverage_gap");
    }

    [Fact]
    public void A_provider_that_served_less_than_asked_moves_the_cursor_less_far()
    {
        // Serving half is fine — the rest is asked for again next run. What is
        // not fine is pretending the whole window arrived.
        var cursor = Now.AddDays(-10);

        var advanced = Cursor.Advance(cursor, new DateRange(cursor, Now.AddDays(-8)));

        advanced.IsSuccess.Should().BeTrue();
        advanced.Value.Should().Be(Now.AddDays(-8));
        advanced.Value.Should().BeBefore(Now);
    }

    [Fact]
    public void A_stale_answer_arriving_late_never_moves_the_cursor_backwards()
    {
        var cursor = Now.AddDays(-2);

        var advanced = Cursor.Advance(cursor, new DateRange(Now.AddDays(-9), Now.AddDays(-5)));

        advanced.IsSuccess.Should().BeTrue();
        advanced.Value.Should().Be(cursor, "a re-delivered old page must not undo progress");
    }
}
