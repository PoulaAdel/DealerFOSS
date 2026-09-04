// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   CustomerRecordSinkTests — the first capability that can actually receive a
//   record from a connector, proven end to end against real SQL.
//
// Usage:
//   Runs with the integration suite.
//
// Coding Instructions:
//   The test that matters most is the double-delivery one. The cursor
//   refuses to advance whenever a provider will not account for its window,
//   so the same records arrive again tomorrow BY DESIGN — routinely, not
//   exceptionally. If the sink is not idempotent that safety feature becomes
//   a duplicate-customer generator, and it does so quietly.
//
//   These run through the real ConnectorRuntime inside a real tenant scope,
//   as a real seeded user. Calling ApplyAsync directly with a stub would
//   prove the mapping works and say nothing about whether the runtime finds
//   the sink, whether the permission check passes, whether the transaction
//   holds, or whether the counts reach the run record — which is the only
//   place an operator would ever look.

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DealerFOSS.App;
using DealerFOSS.Core;
using DealerFOSS.Customers;
using DealerFOSS.Data;
using DealerFOSS.Integrations;
using DealerFOSS.Integrations.Connectors.Fixture;
using DealerFOSS.Tenancy;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class CustomerRecordSinkTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";

    private static readonly DateTimeOffset Start = new(2026, 8, 14, 6, 0, 0, TimeSpan.Zero);

    private readonly HostFixture _fixture = fixture;

    /// <summary>Unique per test, so runs in the same suite cannot see each other.</summary>
    private readonly RooftopId _rooftop = new(Guid.NewGuid());

    /// <summary>
    /// Also unique per test. An external reference identifies exactly one
    /// customer across the whole dealer organization and the database enforces
    /// it, so two tests both importing "FIX-0001" would see each other's rows
    /// and the second would report them unchanged.
    /// </summary>
    private readonly string _prefix = $"T{Guid.NewGuid():N}"[..12];

    private static readonly Dictionary<string, string?> Settings = new(StringComparer.Ordinal)
    {
        ["SubscriptionId"] = "sub-123",
        ["DepartmentId"] = "dept-fi",
        ["ApiSecret"] = "not-a-real-secret",
    };

    private static ConnectorCapability Customers =>
        new FixtureConnector().Manifest.Capability(CustomerFields.Contract, CustomerFields.Version)!;

    // --- The loop closes ----------------------------------------------------

    [Fact]
    public async Task A_run_now_reaches_a_capability_and_writes_real_customers()
    {
        // Before a sink existed this reported Misconfigured and never called the
        // provider — the honest state of an edge with nowhere to deliver.
        var run = await RunAsync(recordCount: 2);

        run.Outcome.Should().Be(RunOutcome.Succeeded);
        run.RecordsApplied.Should().Be(2);
        run.RecordsQuarantined.Should().Be(0);

        (await StoredAsync()).Should().HaveCount(2);
    }

    [Fact]
    public async Task The_same_batch_delivered_twice_produces_one_set_of_customers()
    {
        var first = await RunAsync(recordCount: 2);

        // A day later, so there is a fresh window to ask for. The provider hands
        // back the same records — which is what overlap means, and overlap is
        // deliberate: it is the only reason late-posted paperwork ever arrives.
        var second = await RunAsync(recordCount: 2, at: Start.AddDays(1));

        first.RecordsApplied.Should().Be(2);
        first.RecordsUnchanged.Should().Be(0);

        // Second time nothing is new, and the run says so rather than reporting
        // two more applied. "500 applied" every night is exactly what a stuck
        // feed looks like when nobody counts the unchanged ones.
        second.RecordsApplied.Should().Be(0);
        second.RecordsUnchanged.Should().Be(2);

        (await StoredAsync()).Should().HaveCount(2, "the second delivery must not duplicate");
    }

    [Fact]
    public async Task A_provider_that_will_not_account_for_its_window_re_reads_safely()
    {
        // The two halves of the design meeting. The cursor refuses to move, so
        // the same window arrives again — and the sink absorbs it without
        // duplicating. Neither half is any use without the other.
        const FixtureBehaviour Silent = FixtureBehaviour.SilentAboutCoverage;

        var first = await RunAsync(recordCount: 2, behaviour: Silent);
        var second = await RunAsync(recordCount: 2, behaviour: Silent, at: Start.AddDays(1));

        first.CursorHeld.Should().BeTrue();
        second.CursorHeld.Should().BeTrue();

        first.RecordsApplied.Should().Be(2);
        second.RecordsApplied.Should().Be(0);
        second.RecordsUnchanged.Should().Be(2);

        (await StoredAsync()).Should().HaveCount(2);
    }

    // --- A bad record does not take the good ones with it -------------------

    [Fact]
    public async Task An_unusable_record_is_quarantined_and_the_rest_still_land()
    {
        // The fixture makes every third record nameless, so three is one bad and
        // two good.
        var run = await RunAsync(recordCount: 3);

        run.Outcome.Should().Be(RunOutcome.SucceededWithQuarantine);
        run.RecordsApplied.Should().Be(2, "one bad record must not fail the other two");
        run.RecordsQuarantined.Should().Be(1);

        await using var db = NewContext();
        var held = await db.QuarantinedRecords
            .Where(q => q.RooftopId == _rooftop)
            .SingleAsync(CancellationToken.None);

        held.ReasonCode.Should().Be("customers.missing_name");
        held.ExternalId.Should().Be($"{_prefix}-0003");

        // The provider's own values, kept so somebody can see what actually
        // arrived rather than our interpretation of it.
        held.Payload.Should().Contain(CustomerFields.Email);
    }

    [Fact]
    public async Task A_bad_record_is_still_refused_on_the_next_run_rather_than_invented()
    {
        await RunAsync(recordCount: 3);
        var second = await RunAsync(recordCount: 3, at: Start.AddDays(1));

        second.RecordsApplied.Should().Be(0);
        second.RecordsUnchanged.Should().Be(2);
        second.RecordsQuarantined.Should().Be(1);

        (await StoredAsync()).Should().HaveCount(2);
    }

    // --- Identity -----------------------------------------------------------

    [Fact]
    public async Task A_run_with_nobody_behind_it_is_refused_before_the_provider_is_called()
    {
        // An integration writes real dealership records. One that could do so
        // with no name attached would be the only path into this application
        // that leaves nothing on the audit trail.
        await using var scope = await OpenScopeAsync(signIn: false);

        var runtime = new ConnectorRuntime(
            scope.Services.GetRequiredService<TenantDb>(),
            [new CustomerRecordSink(scope.Services.GetRequiredService<ICustomers>())],
            scope.Services.GetRequiredService<ICurrentUser>(),
            new FixedClock(Start));

        var run = await runtime.RunAsync(
            new FixtureConnector(FixtureBehaviour.Wellbehaved, 2, _prefix),
            _rooftop,
            Customers,
            Settings,
            TimeSpan.FromDays(7),
            CancellationToken.None);

        run.Outcome.Should().Be(RunOutcome.Misconfigured);
        run.FailureCode.Should().Be("integration.no_run_as_user");
        (await StoredAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// A background job carries the permissions of whoever asked for it, and
    /// cannot exceed them.
    ///
    /// <para>
    /// This is the property that makes running work outside a request safe at
    /// all. The tempting shortcut is for background work to run privileged —
    /// there is no browser to refuse, and it always succeeds, which looks like
    /// it is working. What it actually does is create a second way into every
    /// record that ignores the permission system: ask for a job as somebody with
    /// almost no access, and have it done with all of it.
    /// </para>
    /// <para>
    /// So the run is made as the rooftop-scoped advisor, who may read stock and
    /// may not write customers. The provider behaves perfectly and the records
    /// are fine. It must still write nothing.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_run_asked_for_by_somebody_without_the_permission_writes_nothing()
    {
        await using var scope = await OpenScopeAsync(signIn: false);
        scope.Services.GetRequiredService<ICurrentUser>()
            .Set(DevelopmentSeeder.DevUsers.FirstRooftopOnly);

        var runtime = new ConnectorRuntime(
            scope.Services.GetRequiredService<TenantDb>(),
            [new CustomerRecordSink(scope.Services.GetRequiredService<ICustomers>())],
            scope.Services.GetRequiredService<ICurrentUser>(),
            new FixedClock(Start));

        var run = await runtime.RunAsync(
            new FixtureConnector(FixtureBehaviour.Wellbehaved, 2, _prefix),
            _rooftop,
            Customers,
            Settings,
            TimeSpan.FromDays(7),
            CancellationToken.None);

        run.RecordsApplied.Should().Be(0,
            because: "a job cannot do what the person who asked for it may not do");

        (await StoredAsync()).Should().BeEmpty(
            because: "no customer may exist that this caller could not have created by hand");
    }

    // --- Mapping ------------------------------------------------------------

    [Fact]
    public async Task Contract_fields_arrive_as_a_usable_customer()
    {
        await RunAsync(recordCount: 1);

        await using var scope = await OpenScopeAsync();
        var customers = scope.Services.GetRequiredService<ICustomers>();

        var found = await customers.FindByExternalReferenceAsync($"{_prefix}-0001", CancellationToken.None);
        found.IsSuccess.Should().BeTrue();

        var customer = found.Value!;
        customer.FirstName.Should().Be("Sam");
        customer.LastName.Should().Be($"{_prefix}0001");
        customer.Kind.Should().Be("Person");
        customer.HomeRooftopId.Should().Be(_rooftop, "records arrive for a named dealership");

        customer.Address.Should().NotBeNull();
        customer.Address!.Line1.Should().Be("1 Fixture Way");
        customer.Address.City.Should().Be("Testburg");

        customer.ContactPoints.Should().Contain(p => p.Value.Contains("example.invalid"));
    }

    // --- Plumbing -----------------------------------------------------------

    private async Task<ConnectorRun> RunAsync(
        int recordCount,
        FixtureBehaviour behaviour = FixtureBehaviour.Wellbehaved,
        DateTimeOffset? at = null)
    {
        // A real scope, so the sink gets the real CustomerService with real
        // permission checks. A stubbed ICustomers would prove nothing about
        // whether an integration is actually allowed to write.
        await using var scope = await OpenScopeAsync();

        var runtime = new ConnectorRuntime(
            scope.Services.GetRequiredService<TenantDb>(),
            [new CustomerRecordSink(scope.Services.GetRequiredService<ICustomers>())],
            scope.Services.GetRequiredService<ICurrentUser>(),
            new FixedClock(at ?? Start));

        return await runtime.RunAsync(
            new FixtureConnector(behaviour, recordCount, _prefix),
            _rooftop,
            Customers,
            Settings,
            TimeSpan.FromDays(7),
            CancellationToken.None);
    }

    /// <summary>
    /// A tenant scope acting as the organization-wide development user — the
    /// same shape the CSV import worker uses, which runs as the person who asked
    /// rather than as a system principal.
    /// </summary>
    private async Task<TenantScope> OpenScopeAsync(bool signIn = true)
    {
        var factory = _fixture.Services.GetRequiredService<ITenantScopeFactory>();
        var scope = await factory.OpenAsync(Tenant, CancellationToken.None)
            ?? throw new InvalidOperationException($"The '{Tenant}' tenant did not resolve.");

        if (signIn)
        {
            scope.Services.GetRequiredService<ICurrentUser>().Set(DevelopmentSeeder.DevUsers.OrganizationWide);
        }

        return scope;
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

    private static TenantDb NewContext() =>
        new(
            new DbContextOptionsBuilder<TenantDb>()
                .UseSqlServer(HostFixture.TenantConnectionString(Tenant))
                .Options,
            new FixedClock(Start),
            new CurrentUser());

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }
}
