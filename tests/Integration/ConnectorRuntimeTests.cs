// ConnectorRuntimeTests — the cursor rules against a real database.
//
// Use:  runs with the integration suite; needs SQL, like the rest of it.
// Edit: these exist because the unit tests prove the ARITHMETIC and prove
//       nothing about what is written down. A cursor rule that is correct in
//       memory and lost on save is worth nothing: the whole point of refusing to
//       advance is that tomorrow's run re-reads the same window, and "tomorrow"
//       means a different process against the same row.
//
//       Each test therefore builds a fresh TenantDb per run — the same context
//       reused would let a passing test rely on the change tracker rather than
//       on anything having reached SQL.

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using DealerFOSS.Core;
using DealerFOSS.Data;
using DealerFOSS.Integrations;
using DealerFOSS.Integrations.Connectors.Fixture;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class ConnectorRuntimeTests
{
    private const string Tenant = "northgroup";

    private static readonly DateTimeOffset Start = new(2026, 8, 12, 6, 0, 0, TimeSpan.Zero);

    // A rooftop that exists in the seeded organization is not required: nothing
    // here has a foreign key to one, deliberately, so a feed's history survives
    // a location being closed.
    private readonly RooftopId _rooftop = new(Guid.NewGuid());

    private static readonly Dictionary<string, string?> Settings = new(StringComparer.Ordinal)
    {
        ["SubscriptionId"] = "sub-123",
        ["DepartmentId"] = "dept-fi",
        ["ApiSecret"] = "not-a-real-secret",
    };

    private static ConnectorCapability Deals => new FixtureConnector().Manifest.Capability("Deals", 1)!;

    private static ConnectorCapability Service => new FixtureConnector().Manifest.Capability("Service", 1)!;

    // --- The cursor, across process boundaries ----------------------------

    [Fact]
    public async Task A_provider_that_says_nothing_leaves_a_cursor_a_later_run_can_still_see()
    {
        var connector = new FixtureConnector(FixtureBehaviour.SilentAboutCoverage);

        var first = await RunAsync(connector, Deals, Start);

        first.Outcome.Should().Be(RunOutcome.Succeeded, "the records did arrive");
        first.RecordsApplied.Should().Be(2);
        first.CursorHeld.Should().BeTrue();
        first.CursorHeldReason.Should().Be("integration.coverage_unknown");

        // A different context entirely — nothing in memory carries over.
        var stored = await ReadCursorAsync();
        stored!.Position.Should().Be(Start - TimeSpan.FromDays(7), "it must not have moved");
        stored.HeldBecause.Should().Be("integration.coverage_unknown");
        stored.ConsecutiveHolds.Should().Be(1);
    }

    [Fact]
    public async Task Consecutive_holds_are_counted_so_a_store_falling_behind_is_visible()
    {
        var connector = new FixtureConnector(FixtureBehaviour.SilentAboutCoverage);

        await RunAsync(connector, Deals, Start);
        await RunAsync(connector, Deals, Start.AddDays(1));
        await RunAsync(connector, Deals, Start.AddDays(2));

        var stored = await ReadCursorAsync();

        // Three nights of nothing. One hold is ordinary; the count is what turns
        // a normal event into a reportable one.
        stored!.ConsecutiveHolds.Should().Be(3);
        stored.AdvancedAt.Should().BeNull("it has never moved at all");
    }

    [Fact]
    public async Task A_held_window_is_asked_for_again_next_run_rather_than_skipped()
    {
        var silent = new RecordingConnector(new FixtureConnector(FixtureBehaviour.SilentAboutCoverage));

        await RunAsync(silent, Deals, Start);
        var firstAsk = silent.Requested.Single();

        await RunAsync(silent, Deals, Start.AddHours(1));
        var secondAsk = silent.Requested[^1];

        // The second run starts from exactly where the first did. That
        // re-reading is the entire payoff of refusing to advance, and it only
        // works because the position was written down.
        secondAsk.Start.Should().Be(firstAsk.Start);
        silent.Requested.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_provider_that_skips_the_start_does_not_get_the_cursor_moved_past_the_hole()
    {
        var connector = new FixtureConnector(FixtureBehaviour.ClampsTheStart);

        var run = await RunAsync(connector, Deals, Start);

        run.CursorHeld.Should().BeTrue();
        run.CursorHeldReason.Should().Be("integration.coverage_gap");

        var stored = await ReadCursorAsync();
        stored!.Position.Should().Be(Start - TimeSpan.FromDays(7));
    }

    [Fact]
    public async Task A_good_run_after_a_bad_one_moves_the_cursor_and_clears_the_hold()
    {
        await RunAsync(new FixtureConnector(FixtureBehaviour.SilentAboutCoverage), Deals, Start);

        var later = Start.AddDays(1);
        var run = await RunAsync(new FixtureConnector(), Deals, later);

        run.CursorHeld.Should().BeFalse();
        run.CoveredTo.Should().Be(later);

        var stored = await ReadCursorAsync();
        stored!.Position.Should().Be(later);
        stored.AdvancedAt.Should().Be(later);
        stored.HeldBecause.Should().BeNull("the feed is healthy again and must not read as stuck");
        stored.ConsecutiveHolds.Should().Be(0);
    }

    [Fact]
    public async Task An_endpoint_that_takes_no_dates_keeps_no_cursor_at_all()
    {
        // There is no position to hold: the provider decides what "recent" means
        // and never says. A cursor row here would be permanently "held", which
        // would read as a fault rather than as the normal shape of a delta feed.
        var run = await RunAsync(new FixtureConnector(), Service, Start);

        run.Outcome.Should().Be(RunOutcome.Succeeded);
        run.CursorHeld.Should().BeFalse();
        run.RecordsApplied.Should().Be(2);

        (await ReadCursorAsync("Service")).Should().BeNull();
    }

    [Fact]
    public async Task Two_cursors_for_one_feed_are_refused_by_the_database()
    {
        // Not a rule the runtime remembers — a constraint. Two rows would let two
        // runs each advance their own copy, and the feed would read as up to date
        // while skipping whatever the other had passed.
        await RunAsync(new FixtureConnector(), Deals, Start);

        await using var db = NewContext(Start);
        db.ConnectorCursors.Add(new ConnectorCursor(
            Guid.NewGuid(), "Fixture", _rooftop, "Deals", 1, Start));

        var duplicate = async () => await db.SaveChangesAsync(CancellationToken.None);

        await duplicate.Should().ThrowAsync<DbUpdateException>();
    }

    // --- Run history -------------------------------------------------------

    [Fact]
    public async Task A_run_that_never_finished_leaves_the_evidence_that_it_started()
    {
        // The run row is saved before any work, so a process killed mid-fetch is
        // distinguishable from a night that never ran. A throwing sink stands in
        // for the crash.
        await using var db = NewContext(Start);
        var runtime = new ConnectorRuntime(db, [new ThrowingSink()], new FixedClock(Start));

        var crash = async () => await runtime.RunAsync(
            new FixtureConnector(), _rooftop, Deals, Settings, TimeSpan.FromDays(7), CancellationToken.None);

        await crash.Should().ThrowAsync<InvalidOperationException>();

        await using var fresh = NewContext(Start);
        var orphan = await fresh.ConnectorRuns
            .SingleAsync(r => r.RooftopId == _rooftop, CancellationToken.None);

        orphan.FinishedAt.Should().BeNull();
        orphan.Outcome.Should().Be(RunOutcome.Running, "nothing ever got to say how it ended");
    }

    [Fact]
    public async Task Recent_runs_come_back_newest_first_so_a_failing_week_is_one_query()
    {
        await RunAsync(new FixtureConnector(FixtureBehaviour.SilentAboutCoverage), Deals, Start);
        await RunAsync(new FixtureConnector(), Deals, Start.AddDays(1));

        await using var db = NewContext(Start.AddDays(2));
        var runtime = new ConnectorRuntime(db, [new CountingSink()], new FixedClock(Start.AddDays(2)));

        var history = await runtime.RecentRunsAsync(_rooftop, 10, CancellationToken.None);

        history.Should().HaveCount(2);
        history[0].StartedAt.Should().BeAfter(history[1].StartedAt);
        history[1].CursorHeld.Should().BeTrue();
    }

    // --- Configuration refusals -------------------------------------------

    [Fact]
    public async Task A_blank_required_setting_fails_that_dealership_without_calling_the_provider()
    {
        var connector = new RecordingConnector(new FixtureConnector());
        var settings = new Dictionary<string, string?>(Settings) { ["DepartmentId"] = "   " };

        await using var db = NewContext(Start);
        var runtime = new ConnectorRuntime(db, [new CountingSink()], new FixedClock(Start));

        var run = await runtime.RunAsync(
            connector, _rooftop, Deals, settings, TimeSpan.FromDays(7), CancellationToken.None);

        run.Outcome.Should().Be(RunOutcome.Misconfigured);
        run.FailureCode.Should().Be("integration.setting_missing");

        // And it is written down, because a store failing this way every night
        // is exactly the thing that goes unnoticed for months.
        run.FinishedAt.Should().NotBeNull();
        connector.Requested.Should().BeEmpty("nothing should have been asked of the provider");
    }

    [Fact]
    public async Task A_capability_nothing_can_apply_is_refused_before_the_provider_is_called()
    {
        // Spending a provider's rate limit to throw the answer away is worse than
        // failing: it looks like a working integration.
        var connector = new RecordingConnector(new FixtureConnector());

        await using var db = NewContext(Start);
        var runtime = new ConnectorRuntime(db, [], new FixedClock(Start));

        var run = await runtime.RunAsync(
            connector, _rooftop, Deals, Settings, TimeSpan.FromDays(7), CancellationToken.None);

        run.Outcome.Should().Be(RunOutcome.Misconfigured);
        run.FailureCode.Should().Be("integration.no_sink");
        connector.Requested.Should().BeEmpty();
    }

    // --- Quarantine --------------------------------------------------------

    [Fact]
    public async Task A_rejected_record_is_kept_with_the_payload_that_caused_it()
    {
        await using var db = NewContext(Start);
        var runtime = new ConnectorRuntime(db, [new RejectingSink()], new FixedClock(Start));

        var run = await runtime.RunAsync(
            new FixtureConnector(), _rooftop, Deals, Settings, TimeSpan.FromDays(7), CancellationToken.None);

        run.Outcome.Should().Be(RunOutcome.SucceededWithQuarantine);
        run.RecordsQuarantined.Should().Be(2);
        run.RecordsApplied.Should().Be(0);

        await using var fresh = NewContext(Start);
        var held = await fresh.QuarantinedRecords
            .Where(q => q.RooftopId == _rooftop)
            .ToListAsync(CancellationToken.None);

        held.Should().HaveCount(2);
        held[0].ReasonCode.Should().Be("integration.columns_misaligned");

        // The provider's own values, not our interpretation of them — the whole
        // reason a quarantine row is worth more than a log line.
        held[0].Payload.Should().Contain("frontGross").And.Contain("1250.00");
        held[0].ExpiresAt.Should().Be(Start + QuarantinedRecord.Retention);
    }

    [Fact]
    public async Task A_quarantined_record_stops_being_listed_once_its_retention_runs_out()
    {
        await using var db = NewContext(Start);
        var runtime = new ConnectorRuntime(db, [new RejectingSink()], new FixedClock(Start));

        await runtime.RunAsync(
            new FixtureConnector(), _rooftop, Deals, Settings, TimeSpan.FromDays(7), CancellationToken.None);

        var open = await runtime.OpenQuarantineAsync(_rooftop, CancellationToken.None);
        open.Should().HaveCount(2);

        // ADR-022: the expiry means something from the day it is written, not
        // from the day somebody builds a purge job.
        var afterRetention = Start + QuarantinedRecord.Retention + TimeSpan.FromDays(1);
        await using var later = NewContext(afterRetention);
        var expired = new ConnectorRuntime(later, [new CountingSink()], new FixedClock(afterRetention));

        (await expired.OpenQuarantineAsync(_rooftop, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_resolved_record_leaves_the_queue_but_stays_on_the_record()
    {
        await using var db = NewContext(Start);
        var runtime = new ConnectorRuntime(db, [new RejectingSink()], new FixedClock(Start));

        await runtime.RunAsync(
            new FixtureConnector(), _rooftop, Deals, Settings, TimeSpan.FromDays(7), CancellationToken.None);

        await using var fixing = NewContext(Start);
        var all = await fixing.QuarantinedRecords
            .Where(q => q.RooftopId == _rooftop)
            .ToListAsync(CancellationToken.None);

        all[0].Resolve(Start.AddHours(2), "Mapping corrected in 1.1.");
        await fixing.SaveChangesAsync(CancellationToken.None);

        await using var after = NewContext(Start.AddHours(3));
        var runtime2 = new ConnectorRuntime(after, [new CountingSink()], new FixedClock(Start.AddHours(3)));

        (await runtime2.OpenQuarantineAsync(_rooftop, CancellationToken.None)).Should().HaveCount(1);
        (await after.QuarantinedRecords.CountAsync(q => q.RooftopId == _rooftop, CancellationToken.None))
            .Should().Be(2, "resolving is not deleting");
    }

    // --- Plumbing ----------------------------------------------------------

    private async Task<ConnectorRun> RunAsync(IConnector connector, ConnectorCapability capability, DateTimeOffset now)
    {
        await using var db = NewContext(now);
        var runtime = new ConnectorRuntime(db, [new CountingSink(capability.Contract)], new FixedClock(now));

        return await runtime.RunAsync(
            connector, _rooftop, capability, Settings, TimeSpan.FromDays(7), CancellationToken.None);
    }

    private async Task<ConnectorCursor?> ReadCursorAsync(string contract = "Deals")
    {
        await using var db = NewContext(Start);

        return await db.ConnectorCursors
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.RooftopId == _rooftop && c.Contract == contract,
                CancellationToken.None);
    }

    private static TenantDb NewContext(DateTimeOffset now) =>
        new(
            new DbContextOptionsBuilder<TenantDb>()
                .UseSqlServer(HostFixture.TenantConnectionString(Tenant))
                .Options,
            new FixedClock(now));

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    /// <summary>Remembers what the runtime actually asked the provider for.</summary>
    private sealed class RecordingConnector(IConnector inner) : IConnector
    {
        private readonly List<DateRange> _requested = [];

        public IReadOnlyList<DateRange> Requested => _requested;

        public ConnectorManifest Manifest => inner.Manifest;

        public Task<Result<FetchOutcome>> FetchAsync(
            ConnectorCapability capability,
            IReadOnlyDictionary<string, string?> settings,
            FetchSlice slice,
            CancellationToken cancellationToken)
        {
            if (slice.Range is { } range)
            {
                _requested.Add(range);
            }

            return inner.FetchAsync(capability, settings, slice, cancellationToken);
        }
    }

    private sealed class CountingSink(string contract = "Deals") : IRecordSink
    {
        public string Contract => contract;

        public int Version => 1;

        public Task<Result<ApplyOutcome>> ApplyAsync(
            RooftopId rooftopId,
            IReadOnlyList<ProviderRecord> records,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(new ApplyOutcome(records.Count, [], [])));
    }

    private sealed class RejectingSink : IRecordSink
    {
        public string Contract => "Deals";

        public int Version => 1;

        public Task<Result<ApplyOutcome>> ApplyAsync(
            RooftopId rooftopId,
            IReadOnlyList<ProviderRecord> records,
            CancellationToken cancellationToken)
        {
            var rejected = records
                .Select(r => new RejectedRecord(r, IntegrationErrors.ColumnsMisaligned))
                .ToList();

            return Task.FromResult(Result.Success(new ApplyOutcome(0, rejected, [])));
        }
    }

    private sealed class ThrowingSink : IRecordSink
    {
        public string Contract => "Deals";

        public int Version => 1;

        public Task<Result<ApplyOutcome>> ApplyAsync(
            RooftopId rooftopId,
            IReadOnlyList<ProviderRecord> records,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The database went away mid-apply.");
    }
}
