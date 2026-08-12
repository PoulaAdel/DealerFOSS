// ConnectorConformanceTests — the doc 05 §7 list, run against the fixture.
//
// Use:  runs with the normal test suite. When a real connector lands, it takes
//       the same theory data — that is the point of driving these from
//       IConnector rather than from a concrete class.
// Edit: a passing suite here means "fixture-tested" and nothing more (doc 05
//       §3). It is not sandbox evidence and it is certainly not a production
//       promise; only a real vendor run changes a certification label.

using FluentAssertions;
using DealerFOSS.Integrations;
using DealerFOSS.Integrations.Connectors.Fixture;

namespace DealerFOSS.UnitTests;

public sealed class ConnectorConformanceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 12, 6, 0, 0, TimeSpan.Zero);

    private static Dictionary<string, string?> ValidSettings() => new(StringComparer.Ordinal)
    {
        ["SubscriptionId"] = "sub-123",
        ["DepartmentId"] = "dept-fi",
        ["ApiSecret"] = "not-a-real-secret",
    };

    // --- Manifest accuracy ------------------------------------------------

    [Fact]
    public void A_connector_declares_what_it_supports_and_admits_what_it_does_not()
    {
        var manifest = new FixtureConnector().Manifest;

        manifest.Capability("Deals", 1).Should().NotBeNull();
        manifest.Capability("Deals", 2).Should().BeNull("version 2 is not offered and must not be implied");
        manifest.Capability("Finance", 1).Should().BeNull();

        manifest.Certification.Should().Be(CertificationStatus.FixtureTested);
        manifest.KnownLimitations.Should().NotBeEmpty("a connector that claims no limitations has not looked");
    }

    [Fact]
    public void Every_capability_declares_the_window_its_endpoint_actually_imposes()
    {
        var manifest = new FixtureConnector().Manifest;

        foreach (var capability in manifest.Capabilities)
        {
            capability.Window.Should().NotBeNull(
                "window arithmetic is connector data — a missing policy means the runtime would guess");
        }

        // The two shapes a real provider mixes within one integration.
        manifest.Capability("Deals", 1)!.Window.MaximumChunk.Should().NotBeNull();
        manifest.Capability("Service", 1)!.Window.AcceptsDates.Should().BeFalse();
    }

    // --- Settings validation ----------------------------------------------

    [Fact]
    public void Valid_settings_are_accepted()
    {
        new FixtureConnector().Manifest.ValidateSettings(ValidSettings()).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void A_missing_required_setting_fails_loudly_and_names_itself()
    {
        var settings = ValidSettings();
        settings.Remove("DepartmentId");

        var result = new FixtureConnector().Manifest.ValidateSettings(settings);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("integration.setting_missing");
        result.Error.Message.Should().Contain("DepartmentId");
    }

    [Fact]
    public void A_required_setting_left_blank_is_the_same_failure_as_a_missing_one()
    {
        // This is the one that skips a dealership silently every night: a blank
        // spreadsheet cell parsed with no default, thrown, caught by the
        // per-store handler, and the store produces nothing for months.
        var settings = ValidSettings();
        settings["DepartmentId"] = "   ";

        var result = new FixtureConnector().Manifest.ValidateSettings(settings);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("integration.setting_missing");
    }

    [Fact]
    public void A_setting_the_connector_does_not_have_is_refused_rather_than_ignored()
    {
        // A misspelled key that quietly does nothing is the same failure as a
        // missing one, found much later and much more expensively.
        var settings = ValidSettings();
        settings["SubscribtionId"] = "sub-123";

        var result = new FixtureConnector().Manifest.ValidateSettings(settings);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("integration.setting_unknown");
        result.Error.Message.Should().Contain("SubscribtionId");
    }

    [Theory]
    [InlineData("SettlementDays", "two", "a whole number")]
    [InlineData("UseSandbox", "yes", "true or false")]
    public void An_optional_setting_of_the_wrong_shape_is_refused_when_it_is_saved(
        string name, string value, string expected)
    {
        var settings = ValidSettings();
        settings[name] = value;

        var result = new FixtureConnector().Manifest.ValidateSettings(settings);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("integration.setting_invalid");
        result.Error.Message.Should().Contain(expected);
    }

    [Fact]
    public void An_optional_setting_left_out_entirely_is_fine()
    {
        new FixtureConnector().Manifest.ValidateSettings(ValidSettings()).IsSuccess.Should().BeTrue();
    }

    // --- Cursor safety, end to end ----------------------------------------

    [Theory]
    [InlineData(FixtureBehaviour.SilentAboutCoverage, "integration.coverage_unknown")]
    [InlineData(FixtureBehaviour.ClampsTheStart, "integration.coverage_gap")]
    public async Task A_provider_that_will_not_account_for_the_window_does_not_move_the_cursor(
        FixtureBehaviour behaviour, string expectedCode)
    {
        var connector = new FixtureConnector(behaviour);
        var capability = connector.Manifest.Capability("Deals", 1)!;
        var wanted = new DateRange(Now.AddDays(-30), Now);
        var plan = FetchWindow.Plan(capability.Window, wanted, Now);

        var outcome = await connector.FetchAsync(
            capability, ValidSettings(), plan.Slices[0], CancellationToken.None);

        outcome.IsSuccess.Should().BeTrue("the records did arrive — this is not a failed fetch");
        outcome.Value.Records.Should().NotBeEmpty();

        // ...and yet the cursor stays put. Records arriving and the window being
        // accounted for are two different things, and conflating them is what
        // leaves a hole nobody finds.
        var advanced = Cursor.Advance(wanted.Start, outcome.Value.Covered);
        advanced.IsFailure.Should().BeTrue();
        advanced.Error.Code.Should().Be(expectedCode);
    }

    [Fact]
    public async Task A_well_behaved_provider_moves_the_cursor_to_what_it_served()
    {
        var connector = new FixtureConnector();
        var capability = connector.Manifest.Capability("Deals", 1)!;
        var wanted = new DateRange(Now.AddDays(-30), Now);
        var plan = FetchWindow.Plan(capability.Window, wanted, Now);

        var outcome = await connector.FetchAsync(
            capability, ValidSettings(), plan.Slices[0], CancellationToken.None);

        var advanced = Cursor.Advance(wanted.Start, outcome.Value.Covered);

        advanced.IsSuccess.Should().BeTrue();
        advanced.Value.Should().Be(Now);
    }

    [Fact]
    public async Task A_dateless_endpoint_returns_records_and_still_cannot_move_a_cursor()
    {
        var connector = new FixtureConnector();
        var capability = connector.Manifest.Capability("Service", 1)!;
        var plan = FetchWindow.Plan(capability.Window, new DateRange(Now.AddDays(-7), Now), Now);

        var outcome = await connector.FetchAsync(
            capability, ValidSettings(), plan.Slices[0], CancellationToken.None);

        outcome.Value.Records.Should().NotBeEmpty();
        outcome.Value.Covered.Should().BeNull("the provider decided what 'recent' meant and did not say");
    }

    // --- Poll deadline ----------------------------------------------------

    [Fact]
    public void A_poll_budget_is_spent_by_time_and_not_by_attempts()
    {
        var timing = new ProviderTiming(TimeSpan.FromMinutes(30), RetryBudget: 5, TimeSpan.FromSeconds(2));
        var budget = PollBudget.Start(timing, Now);

        // Ten polls at a provider-suggested ten seconds. An attempt-capped
        // implementation would have given up at five and called a healthy job
        // a timeout.
        var now = Now;
        for (var i = 0; i < 10; i++)
        {
            var (decision, wait) = budget.Next(now, TimeSpan.FromSeconds(10));
            decision.Should().Be(PollDecision.WaitAndRetry);
            now += wait;
        }

        budget.Attempts.Should().Be(10);
    }

    [Fact]
    public void A_long_provider_hint_is_honoured_until_it_would_outrun_the_deadline()
    {
        var timing = new ProviderTiming(TimeSpan.FromMinutes(20), RetryBudget: 5, TimeSpan.FromSeconds(2));
        var budget = PollBudget.Start(timing, Now);

        // The provider asks for ten minutes; it knows something we do not.
        budget.Next(Now, TimeSpan.FromMinutes(10)).Decision.Should().Be(PollDecision.WaitAndRetry);

        // Asking for another ten would land exactly on the deadline, so waiting
        // only delays the report.
        var (decision, _) = budget.Next(Now.AddMinutes(10), TimeSpan.FromMinutes(10));
        decision.Should().Be(PollDecision.StillRunningAtProvider);
    }

    [Fact]
    public void Passing_the_deadline_reports_a_state_rather_than_a_failure()
    {
        var timing = new ProviderTiming(TimeSpan.FromMinutes(5), RetryBudget: 5, TimeSpan.FromSeconds(2));
        var budget = PollBudget.Start(timing, Now);

        var (decision, wait) = budget.Next(Now.AddMinutes(6), TimeSpan.FromSeconds(10));

        // The job is still running at the provider. The correct next action is
        // to ask again on the next run, not to start the whole pull over.
        decision.Should().Be(PollDecision.StillRunningAtProvider);
        wait.Should().Be(TimeSpan.Zero);
    }
}
