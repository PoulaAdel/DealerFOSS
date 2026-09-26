// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   SyncScheduleTests — a feed reads itself with nobody at a keyboard, and the
//   trigger is not a way around the permission system.
//
//   The second half is the one that matters. An unattended trigger is the
//   easiest place in an application to accidentally build a privileged path:
//   there is no browser to refuse it, so a sweep that writes records itself
//   always succeeds and therefore always looks like it is working. What it
//   actually is is a second route into every record that ignores permissions,
//   reachable on a timer.
//
// Usage:
//   dotnet test DealerFOSS.slnx -c Release
//
// Coding Instructions:
//   THESE DRIVE A WORKER INSTANCE OF THEIR OWN, AT A FIXED FUTURE TIME, and
//   both halves of that are deliberate. ScheduleWorker is a registered hosted
//   service, so the real one is running inside this test host on the real clock.
//   A schedule armed as due now would be raced by it, and the test would pass or
//   fail depending on which instance got there first. Arming for a time in 2027
//   makes the ambient worker blind to the row, and a FixedClock at that moment
//   makes this test the only thing that can fire it.
//
//   THE SETTINGS GO IN THROUGH ConnectorSettings.Protect, not as raw JSON. The
//   fixture connector declares a required ApiSecret, so a schedule that stored
//   it in the clear would be the first place in this codebase to do that, and a
//   test writing the stored form by hand would hide it.
//
//   Do not replace the worker with a direct ConnectorRuntime call. The whole
//   subject here is the two-scope arrangement between them — which scope is
//   unattended, which one carries a person, and where the grant is re-read. A
//   test that called the runtime would prove the runtime works, which
//   CustomerRecordSinkTests already does, and would say nothing about the
//   trigger.

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DealerFOSS.App;
using DealerFOSS.Core;
using DealerFOSS.Data;
using DealerFOSS.Integrations;
using DealerFOSS.Integrations.Connectors.Fixture;
using DealerFOSS.Tenancy;
using Xunit;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class SyncScheduleTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";

    /// <summary>
    /// Far enough ahead that the hosted ScheduleWorker, running on the real
    /// clock, never sees these rows as due. See the header.
    /// </summary>
    private static readonly DateTimeOffset Due = new(2027, 3, 4, 2, 0, 0, TimeSpan.Zero);

    private readonly HostFixture _fixture = fixture;

    /// <summary>Unique per test, so runs in the same suite cannot see each other.</summary>
    private readonly RooftopId _rooftop = new(Guid.NewGuid());

    /// <summary>
    /// Also unique per test: an external reference identifies exactly one
    /// customer across the whole organization and the database enforces it, so
    /// two tests both importing "FIX-0001" would see each other's rows.
    /// </summary>
    private readonly string _prefix = $"S{Guid.NewGuid():N}"[..12];

    private static readonly Dictionary<string, string?> Plaintext = new(StringComparer.Ordinal)
    {
        ["SubscriptionId"] = "sub-123",
        ["DepartmentId"] = "dept-fi",
        ["ApiSecret"] = "not-a-real-secret",
    };

    // --- The loop closes ----------------------------------------------------

    [Fact]
    public async Task A_feed_reads_itself_with_nobody_at_a_keyboard()
    {
        // The whole point of the milestone. Nobody calls the runtime, nobody
        // clicks anything: a row says this feed should be read, and a worker
        // reads it.
        var id = await ArmAsync(DevelopmentSeeder.DevUsers.OrganizationWide);

        var fired = await FireOnePassAsync();

        fired.Should().BeTrue(because: "a schedule was due and nothing else was going to run it");

        var schedule = await RowAsync(id);
        schedule.LastRunId.Should().NotBeNull(because: "the run it started is named on the schedule");
        schedule.LastOutcome.Should().Be(nameof(RunOutcome.Succeeded));
        schedule.State.Should().Be(ScheduleState.Armed, "a run that worked changes nothing about arming");

        var run = await RunForAsync(id);

        // Applied PLUS unchanged, not applied alone, and the reason is worth
        // writing down. The connector this fires is the one registered in DI,
        // which fabricates "FIX-0001" and "FIX-0002" — and an external reference
        // identifies exactly one customer across the whole organization, under a
        // unique index. So whichever test in this suite fires a Customers
        // schedule first creates those two, and every later one correctly reports
        // them Unchanged. Asserting "2 applied" would pass or fail on test
        // ordering. What this milestone actually claims is that the trigger drove
        // the runtime through the real sink into the real capability, and
        // applied-or-unchanged is exactly that claim.
        (run.RecordsApplied + run.RecordsUnchanged).Should().Be(2,
            because: "the provider served two records and the sink accounted for both");

        run.RecordsQuarantined.Should().Be(0);

        (await ReferencesAsync()).Should().BeEquivalentTo(
            ["FIX-0001", "FIX-0002"],
            because: "the records a nobody-at-the-keyboard run brought in are really there");
    }

    [Fact]
    public async Task The_run_it_starts_is_attributed_to_the_person_who_armed_it()
    {
        // The answerable half. Work that happens overnight still has a name on
        // it, and the name is a real user rather than "system" — which is what a
        // sweep writing records directly would have produced.
        var id = await ArmAsync(DevelopmentSeeder.DevUsers.OrganizationWide);

        await FireOnePassAsync();

        var run = await RunForAsync(id);

        run.CreatedBy.Should().Be(
            DevelopmentSeeder.DevUsers.OrganizationWide.ToString(),
            because: "a scheduled run carries the authority of whoever armed it, not the system's");
    }

    [Fact]
    public async Task A_schedule_does_not_fire_twice_for_one_due_time()
    {
        // Two application instances share one database, so the claim is a
        // conditional update rather than a read followed by a write. Proven
        // sequentially at one fixed instant rather than with two threads: the
        // property is that the due time moved before the run, and a race would
        // test the scheduler of whichever machine happened to run the suite.
        var id = await ArmAsync(DevelopmentSeeder.DevUsers.OrganizationWide, intervalMinutes: 60);

        var first = await FireOnePassAsync();
        var second = await FireOnePassAsync();

        first.Should().BeTrue();
        second.Should().BeFalse(because: "the claim moved the due time an hour out before running");

        (await RowAsync(id)).NextRunAt.Should().Be(Due.AddHours(1));

        (await RunCountAsync(id)).Should().Be(1, "one due time is one run");
    }

    // --- The trigger is not a way around the permission system ---------------

    /// <summary>
    /// The acceptance test for this milestone, and the reason the design is
    /// shaped the way it is.
    ///
    /// <para>
    /// <c>Migration.Import</c> is held organization-wide by design, so the
    /// rooftop-scoped advisor does not hold it anywhere. A schedule naming them
    /// is exactly what an installation looks like some months after somebody
    /// changed roles: the row still carries their name, the provider is still
    /// serving perfect records, and the feed would keep writing on the authority
    /// of a grant that no longer exists.
    /// </para>
    /// <para>
    /// It must write nothing, and it must say why rather than failing quietly
    /// every quarter-hour.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_scheduled_run_cannot_reach_a_capability_its_authority_may_not_use()
    {
        var id = await ArmAsync(DevelopmentSeeder.DevUsers.FirstRooftopOnly);

        await FireOnePassAsync();

        (await StoredAsync()).Should().BeEmpty(
            because: "no customer may exist that this authority could not have created by hand");

        var schedule = await RowAsync(id);

        schedule.State.Should().Be(ScheduleState.Suspended,
            because: "a feed that cannot honestly run stops, rather than failing every interval");

        schedule.SuspendedReason.Should().Contain("no longer has permission",
            because: "a feed that has stopped is a question somebody will ask");

        (await RunCountAsync(id)).Should().Be(0,
            because: "refusing before the provider is called spends nobody's rate limit");
    }

    [Fact]
    public async Task A_suspended_feed_does_not_come_back_on_its_own()
    {
        // Suspension has to be sticky. A row that suspended and then re-armed
        // itself on the next tick would be a feed failing silently in a loop,
        // which is the failure mode the state exists to prevent.
        var id = await ArmAsync(DevelopmentSeeder.DevUsers.FirstRooftopOnly);

        await FireOnePassAsync();
        var second = await FireOnePassAsync();

        second.Should().BeFalse();
        (await RowAsync(id)).NextRunAt.Should().BeNull("a suspended row is not due at any time");
    }

    [Fact]
    public async Task A_feed_that_cannot_start_suspends_instead_of_failing_every_interval()
    {
        // Deals is declared by the fixture connector and has no IRecordSink, so
        // the runtime refuses it as Misconfigured before calling the provider.
        // That does not improve by being retried in an hour.
        var id = await ArmAsync(
            DevelopmentSeeder.DevUsers.OrganizationWide, contract: "Deals", version: 1);

        await FireOnePassAsync();

        var schedule = await RowAsync(id);

        schedule.LastOutcome.Should().Be(nameof(RunOutcome.Misconfigured));
        schedule.State.Should().Be(ScheduleState.Suspended);
        schedule.SuspendedReason.Should().Contain("integration.no_sink");
    }

    // --- Settings, and the credential in them --------------------------------

    /// <summary>
    /// Which values are protected is decided by the manifest, never by the shape
    /// of the key's name.
    ///
    /// <para>
    /// The tempting rule is "protect anything called secret, password or token".
    /// It is the rule that misses <c>ApiKey</c>, and it misses it silently: the
    /// value stores and reads back perfectly, so nothing ever fails and nobody
    /// finds out until the table is read by somebody who should not have it.
    /// </para>
    /// <para>
    /// Asserted with a protector that records what it was asked to protect,
    /// because the claim is about which values reach it — not about what the
    /// ciphertext looks like.
    /// </para>
    /// </summary>
    [Fact]
    public void Only_a_setting_the_manifest_calls_secret_reaches_the_protector()
    {
        var manifest = new FixtureConnector().Manifest;
        var recorder = new RecordingProtector();

        var stored = ConnectorSettings.Protect(manifest, Plaintext, recorder);

        stored.IsSuccess.Should().BeTrue();

        recorder.Protected.Should().BeEquivalentTo(
            ["not-a-real-secret"],
            because: "ApiSecret is the one setting the fixture manifest declares as a Secret");

        stored.Value.Should().Contain("sub-123",
            because: "a setting that is not a credential is stored as it is, and stays readable");
    }

    /// <summary>
    /// With a real protector configured, a stored credential is not readable.
    ///
    /// <para>
    /// Constructed here rather than resolved, and that is the honest shape. This
    /// test host runs in Development, where <c>ISecretProtector</c> is
    /// deliberately <c>DevSecretProtector</c> — a no-op that returns its input,
    /// so on a development box the credential genuinely does sit in the table in
    /// the clear. That is a property of the environment, stated in that class's
    /// own header, and <c>Program.cs</c> refuses to start with it anywhere else.
    /// What this asserts is the half that belongs to this code: the value goes
    /// through the seam, so a deployment with real keys stores a ciphertext and
    /// reads the credential back intact.
    /// </para>
    /// </summary>
    [Fact]
    public void A_credential_stored_under_a_real_protector_is_not_readable()
    {
        var manifest = new FixtureConnector().Manifest;

        // A key for this test only. Never a configured one — a test that needed
        // the deployment's key would be a test nobody could run.
        var real = new EnvelopeSecretProtector(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["test"] = Convert.ToBase64String(new byte[32]),
            },
            "test");

        var stored = ConnectorSettings.Protect(manifest, Plaintext, real);
        stored.IsSuccess.Should().BeTrue();

        stored.Value.Should().NotContain("not-a-real-secret",
            because: "ISecretProtector names connector credentials as one of the two things it is for");

        var revealed = ConnectorSettings.Reveal(manifest, stored.Value, real);

        revealed["ApiSecret"].Should().Be("not-a-real-secret",
            because: "a credential nothing can read back is a feed that cannot run");
    }

    [Fact]
    public void A_secret_is_never_described_back_to_a_reader()
    {
        // Not even in its protected form. A ciphertext looks safe to hand out and
        // is not: it travels to the browser and into logs, and whoever later
        // obtains the key gets every one of them.
        var manifest = new FixtureConnector().Manifest;
        var protector = _fixture.Services.GetRequiredService<ISecretProtector>();

        var stored = ConnectorSettings.Protect(manifest, Plaintext, protector);
        stored.IsSuccess.Should().BeTrue();

        var described = ConnectorSettings.Describe(manifest, stored.Value);

        var secret = described.Single(s => s.Name == "ApiSecret");
        secret.Value.Should().BeNull("a reader learns that it is set, and nothing else");
        secret.IsSet.Should().BeTrue();

        described.Single(s => s.Name == "SubscriptionId").Value.Should().Be("sub-123");
    }

    [Fact]
    public void Changing_an_interval_does_not_wipe_the_credential()
    {
        // The consequence of never returning a secret: a screen editing a
        // schedule has nothing to send back for one. If an absent secret meant
        // "clear it", every interval change would break the feed.
        var manifest = new FixtureConnector().Manifest;
        var protector = _fixture.Services.GetRequiredService<ISecretProtector>();

        var stored = ConnectorSettings.Protect(manifest, Plaintext, protector).Value;

        var merged = ConnectorSettings.Merge(
            manifest,
            stored,
            new Dictionary<string, string?>(StringComparer.Ordinal) { ["DepartmentId"] = "dept-service" },
            protector);

        merged.IsSuccess.Should().BeTrue();

        var revealed = ConnectorSettings.Reveal(manifest, merged.Value, protector);

        revealed["ApiSecret"].Should().Be("not-a-real-secret", "it was kept, not cleared");
        revealed["DepartmentId"].Should().Be("dept-service", "and the change landed");
    }

    // --- Plumbing -----------------------------------------------------------

    /// <summary>
    /// Store a schedule due at <see cref="Due"/>, owned by the given person.
    /// </summary>
    /// <remarks>
    /// Goes through <c>ConnectorSchedule.Arm</c> and
    /// <c>ConnectorSettings.Protect</c> — the real factory and the real
    /// protection — rather than through <c>IIntegrations.ArmAsync</c>, because
    /// that path checks the caller's own grant and so cannot create the row the
    /// permission test needs: one naming somebody who does not hold
    /// <c>Migration.Import</c>, which is what a schedule outliving its grant
    /// looks like.
    /// </remarks>
    private async Task<Guid> ArmAsync(
        Guid armedBy,
        string contract = CustomerFields.Contract,
        int version = CustomerFields.Version,
        int intervalMinutes = 15)
    {
        var connector = new FixtureConnector(FixtureBehaviour.Wellbehaved, 2, _prefix);
        var capability = connector.Manifest.Capability(contract, version)!;
        var protector = _fixture.Services.GetRequiredService<ISecretProtector>();

        var stored = ConnectorSettings.Protect(connector.Manifest, Plaintext, protector);
        stored.IsSuccess.Should().BeTrue();

        var schedule = ConnectorSchedule.Arm(
            Guid.NewGuid(),
            connector.Manifest.Provider,
            _rooftop,
            capability,
            stored.Value,
            armedBy,
            intervalMinutes,
            Due);

        schedule.IsSuccess.Should().BeTrue();

        await using var db = NewContext();
        db.Add(schedule.Value);
        await db.SaveChangesAsync(CancellationToken.None);

        return schedule.Value.Id;
    }

    /// <summary>
    /// One pass of a worker of our own, at the moment the schedule is due.
    /// Returns whether it fired anything.
    /// </summary>
    private Task<bool> FireOnePassAsync()
    {
        var worker = new ScheduleWorker(
            _fixture.Services.GetRequiredService<IServiceScopeFactory>(),
            _fixture.Services.GetRequiredService<ITenantScopeFactory>(),
            new FixedClock(Due),
            _fixture.Services.GetRequiredService<ILogger<ScheduleWorker>>());

        return worker.RunOnePassAsync(CancellationToken.None);
    }

    private static async Task<ConnectorSchedule> RowAsync(Guid id)
    {
        await using var db = NewContext();

        return await db.Set<ConnectorSchedule>()
            .AsNoTracking()
            .SingleAsync(s => s.Id == id, CancellationToken.None);
    }

    private static async Task<ConnectorRun> RunForAsync(Guid scheduleId)
    {
        var schedule = await RowAsync(scheduleId);

        await using var db = NewContext();

        return await db.Set<ConnectorRun>()
            .AsNoTracking()
            .SingleAsync(r => r.Id == schedule.LastRunId, CancellationToken.None);
    }

    /// <summary>
    /// Runs recorded against this test's rooftop. Counted rather than read off
    /// the schedule, because the question is how many times the provider was
    /// actually called.
    /// </summary>
    private static async Task<int> RunCountAsync(Guid scheduleId)
    {
        var schedule = await RowAsync(scheduleId);

        await using var db = NewContext();

        return await db.Set<ConnectorRun>()
            .AsNoTracking()
            .CountAsync(
                r => r.RooftopId == schedule.RooftopId && r.Contract == schedule.Contract,
                CancellationToken.None);
    }

    private async Task<List<string>> StoredAsync()
    {
        await using var db = NewContext();

        return await db.Customers
            .AsNoTracking()
            .Where(c => c.HomeRooftopId == _rooftop && c.ExternalReference != null)
            .OrderBy(c => c.ExternalReference)
            .Select(c => c.ExternalReference!)
            .ToListAsync(CancellationToken.None);
    }

    /// <summary>
    /// The fixture's two fabricated references, wherever in this organization
    /// they were created. See the headline test for why the rooftop is not part
    /// of the question.
    /// </summary>
    private static async Task<List<string>> ReferencesAsync()
    {
        await using var db = NewContext();

        return await db.Customers
            .AsNoTracking()
            .Where(c => c.ExternalReference == "FIX-0001" || c.ExternalReference == "FIX-0002")
            .Select(c => c.ExternalReference!)
            .ToListAsync(CancellationToken.None);
    }

    /// <summary>
    /// An <see cref="ISecretProtector"/> that keeps a list of what it was asked
    /// to protect, so a test can assert which values reached it rather than
    /// guessing from the output.
    /// </summary>
    private sealed class RecordingProtector : ISecretProtector
    {
        private readonly List<string> _protected = [];

        public IReadOnlyList<string> Protected => _protected;

        public string Protect(string plaintext)
        {
            _protected.Add(plaintext);
            return $"protected:{plaintext}";
        }

        public string Unprotect(string protectedValue) =>
            protectedValue.StartsWith("protected:", StringComparison.Ordinal)
                ? protectedValue["protected:".Length..]
                : protectedValue;
    }

    private static TenantDb NewContext() =>
        new(
            new DbContextOptionsBuilder<TenantDb>()
                .UseSqlServer(HostFixture.TenantConnectionString(Tenant))
                .Options,
            new FixedClock(Due),
            new CurrentUser());

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }
}
