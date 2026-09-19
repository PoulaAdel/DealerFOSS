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
using System.Text.Json;
using DealerFOSS.Identity;
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
        //
        // The caller here is an unset CurrentUser, assembled by hand. Since
        // 2026-09-05 this arrangement cannot be reached from an UnattendedScope
        // at all — Get<T> would not compile for ICustomers — so what is left to
        // prove is ConnectorRuntime's OWN guard, which still matters: the
        // runtime is constructed directly in places the scope types do not
        // reach, and it must refuse rather than trust its caller to be set.
        await using var scope = await OpenScopeAsync();

        var runtime = new ConnectorRuntime(
            scope.Services.GetRequiredService<TenantDb>(),
            [new CustomerRecordSink(
                scope.Services.GetRequiredService<ICustomers>(), new FixedClock(Start))],
            new CurrentUser(),
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
        await using var scope = await OpenScopeAsync(DevelopmentSeeder.DevUsers.FirstRooftopOnly);

        var runtime = new ConnectorRuntime(
            scope.Services.GetRequiredService<TenantDb>(),
            [new CustomerRecordSink(
                scope.Services.GetRequiredService<ICustomers>(), new FixedClock(Start))],
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

        // State and county arrive as two fields and must stay two. Folding them
        // together loses what decides a US sales tax rate (ADR-024), and the
        // fixture sends different words for each so a swap cannot look right.
        customer.Address.AdministrativeArea.Should().Be("IL");
        customer.Address.County.Should().Be("Sangamon");

        customer.ContactPoints.Should().Contain(p => p.Value.Contains("example.invalid"));
    }

    // --- A record the provider no longer has (ADR-026) ------------------------
    //
    // The exit criterion in STATUS said "deletes are not modelled" and it was
    // right: there was no tombstone and no IsDeleted anywhere in Integrations,
    // so a record removed at the provider stayed ours and nobody could tell.
    //
    // What these guard is the SHAPE of the answer, not just its presence. A
    // delete marks the record; it never hides or removes one. A customer who
    // quietly vanished from search while an advisor was on the telephone to them
    // would be worse than never modelling deletes at all.

    [Fact]
    public async Task A_deleted_record_is_marked_and_still_listed()
    {
        var reference = $"{_prefix}-GONE";
        await ApplyAsync(Upsert(reference, "Okonkwo"));

        var marked = await ApplyAsync(Delete(reference));

        marked.Applied.Should().Be(1);

        var customer = await FindAsync(reference);
        customer.Should().NotBeNull();
        customer!.RemovedAtProviderOn.Should().NotBeNull(
            because: "the provider said it no longer has them, and that is worth recording");
        customer.IsArchived.Should().BeFalse(
            because: "ARCHIVING IS THE DEALERSHIP'S OWN DECISION. The provider is making a "
                + "statement about its own database, and conflating the two is how a customer "
                + "with three repair orders silently disappears from the screen somebody is "
                + "looking at");

        // The part that matters most: it is still there to be found.
        (await ListedReferencesAsync()).Should().Contain(reference,
            because: "a tombstone marks, it does not hide");
    }

    [Fact]
    public async Task Deleting_the_same_record_twice_changes_nothing_the_second_time()
    {
        // A held cursor replays the same window every night, so the second
        // delivery of a deletion is the normal case rather than the odd one.
        var reference = $"{_prefix}-TWICE";
        await ApplyAsync(Upsert(reference, "Replay"));
        await ApplyAsync(Delete(reference));

        var again = await ApplyAsync(Delete(reference));

        again.Applied.Should().Be(0);
        again.Unchanged.Should().Be(1,
            because: "\"3 applied, 497 unchanged\" is the shape of a feed that is working; "
                + "counting a replayed deletion as applied every night hides that");
    }

    [Fact]
    public async Task Deleting_a_record_we_never_had_is_not_a_rejection()
    {
        // Delta feeds routinely report deletions of records this dealership
        // never received. Quarantining them would bury the real refusals.
        var outcome = await ApplyAsync(Delete($"{_prefix}-NEVERMINE"));

        outcome.Rejected.Should().BeEmpty();
        outcome.Unchanged.Should().Be(1);
        (await FindAsync($"{_prefix}-NEVERMINE")).Should().BeNull();
    }

    [Fact]
    public async Task A_provider_serving_a_deleted_record_again_restores_it()
    {
        // Feeds restore records removed in error, or moved between systems and
        // put back. An undelete has to be as ordinary as the delete was.
        var reference = $"{_prefix}-BACK";
        await ApplyAsync(Upsert(reference, "Returned"));
        await ApplyAsync(Delete(reference));

        var restored = await ApplyAsync(Upsert(reference, "Returned"));

        restored.Applied.Should().Be(1);
        (await FindAsync(reference))!.RemovedAtProviderOn.Should().BeNull();
    }

    [Fact]
    public async Task A_deletion_never_removes_a_row()
    {
        var reference = $"{_prefix}-KEPT";
        await ApplyAsync(Upsert(reference, "Kept"));
        await ApplyAsync(Delete(reference));

        // Said plainly, because this is the promise ADR-026 makes and the one a
        // future "tidy up the deleted ones" change would break.
        (await StoredAsync()).Should().Contain(reference,
            because: "nothing the provider says can delete a dealership's own record");
    }

    // --- Reordered delivery ---------------------------------------------------
    //
    // The fourth of the four delivery tests named in STATUS's exit criteria, and
    // the one that had never been written. Two different properties live here
    // and conflating them is why it is easy to get wrong.

    [Fact]
    public async Task Independent_records_reach_the_same_state_in_any_order()
    {
        // Order between DIFFERENT records carries no meaning, so a provider that
        // serves a page in a different sequence must not produce a different
        // dealership.
        var forward = $"{_prefix}-A";
        var backward = $"{_prefix}-B";

        await ApplyAsync(Upsert(forward, "Alpha"), Upsert(backward, "Beta"));
        var first = await ListedReferencesAsync();

        var other = $"{_prefix}-C";
        var another = $"{_prefix}-D";
        await ApplyAsync(Upsert(another, "Delta"), Upsert(other, "Gamma"));

        var second = (await ListedReferencesAsync()).Except(first).Order().ToList();

        second.Should().Equal([other, another],
            because: "the same two records delivered in the opposite order are the same two "
                + "records");
    }

    [Fact]
    public async Task Order_within_one_record_is_the_providers_meaning_and_is_obeyed()
    {
        // Order between records ABOUT THE SAME THING is the opposite case: it is
        // the provider telling us what happened and in which sequence. "Created,
        // then deleted" and "deleted, then created" describe different days, and
        // a sink that sorted its batch would turn one into the other.
        var gone = $"{_prefix}-SEQ1";
        await ApplyAsync(Upsert(gone, "Gone"), Delete(gone));

        (await FindAsync(gone))!.RemovedAtProviderOn.Should().NotBeNull(
            because: "the last thing the provider said about this record is that it is gone");

        var here = $"{_prefix}-SEQ2";
        await ApplyAsync(Delete(here), Upsert(here, "Here"));

        (await FindAsync(here))!.RemovedAtProviderOn.Should().BeNull(
            because: "the same two records the other way round mean the opposite, and the sink "
                + "must not reorder them into agreement");
    }

    [Fact]
    public async Task A_record_repeated_inside_one_batch_is_applied_once()
    {
        // Idempotence has to hold WITHIN a batch, not only between runs — a
        // provider paging over a moving window serves the same row twice.
        var reference = $"{_prefix}-DUP";

        var outcome = await ApplyAsync(
            Upsert(reference, "Once"), Upsert(reference, "Twice"));

        outcome.Applied.Should().Be(1);
        outcome.Unchanged.Should().Be(1);
        (await StoredAsync()).Count(r => r == reference).Should().Be(1);
    }

    // --- Plumbing for the two blocks above ------------------------------------

    private static ProviderRecord Upsert(string reference, string lastName) =>
        new(reference, null, new Dictionary<string, string?>
        {
            [CustomerFields.Kind] = "Person",
            // The surname carries the reference so the LIST can be searched for
            // these rows: SearchAsync matches names, not external references.
            [CustomerFields.LastName] = lastName + reference,
            [CustomerFields.FirstName] = "Test",
        });

    /// <summary>
    /// A deletion carries no fields — the provider is saying the record is gone,
    /// not describing it. A sink that needed fields here could not accept the
    /// tombstones real delta feeds actually send.
    /// </summary>
    private static ProviderRecord Delete(string reference) =>
        new(reference, null, new Dictionary<string, string?>(), RecordAction.Delete);

    private async Task<ApplyOutcome> ApplyAsync(params ProviderRecord[] records)
    {
        await using var scope = await OpenScopeAsync();

        var sink = new CustomerRecordSink(
            scope.Services.GetRequiredService<ICustomers>(), new FixedClock(Start));

        var outcome = await sink.ApplyAsync(_rooftop, records, CancellationToken.None);
        outcome.IsSuccess.Should().BeTrue(because: outcome.IsFailure ? outcome.Error.Code : "");

        return outcome.Value;
    }

    private static async Task<Customer?> FindAsync(string reference)
    {
        await using var db = NewContext();

        return await db.Customers
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.ExternalReference == reference, CancellationToken.None);
    }

    /// <summary>
    /// What the customer LIST returns — the thing a tombstone must not remove a
    /// record from. Read through the service rather than the table, because the
    /// list query is where a filter would be added by somebody tidying up.
    /// </summary>
    private async Task<List<string>> ListedReferencesAsync()
    {
        await using var scope = await OpenScopeAsync();
        var customers = scope.Services.GetRequiredService<ICustomers>();

        var page = await customers.SearchAsync(_prefix, 200, 0, CancellationToken.None);

        page.IsSuccess.Should().BeTrue();

        var found = new List<string>();
        foreach (var row in page.Value.Rows)
        {
            var detail = await customers.GetAsync(row.Id, CancellationToken.None);
            if (detail.IsSuccess && detail.Value.ExternalReference is { } reference)
            {
                found.Add(reference);
            }
        }

        return found;
    }

    // --- Replaying what was held back -----------------------------------------
    //
    // "Quarantined records are inspectable and replayable" was half true: a
    // rejected record was kept with its payload and could be marked resolved,
    // and QuarantinedRecord.Resolve carried the comment "Does not replay it —
    // nothing replays yet". Marking a bad record dealt-with without re-running
    // it empties the queue without fixing anything.
    //
    // These hold the shape of the answer. Replay runs the REAL sink; a replay
    // that fails again leaves the row where it was with the new reason.

    [Fact]
    public async Task A_held_record_that_now_applies_is_applied_and_resolved()
    {
        var reference = $"{_prefix}-HELD";
        var id = await HoldAsync(reference, Payload(reference, lastName: "Mapped"));

        await using var scope = await OpenScopeAsync();
        var integrations = scope.Services.GetRequiredService<IIntegrations>();

        var replayed = await integrations.ReplayAsync(id, CancellationToken.None);

        replayed.IsSuccess.Should().BeTrue(because: replayed.IsFailure ? replayed.Error.Code : "");
        replayed.Value.Applied.Should().BeTrue();

        // The record actually went in. Without this the test would pass on a
        // replay that reported success and wrote nothing.
        (await FindAsync(reference)).Should().NotBeNull();

        var row = await HeldRowAsync(id);
        row.ResolvedAt.Should().NotBeNull();
        row.ResolutionNote.Should().Contain("applied");
    }

    [Fact]
    public async Task A_replay_that_is_refused_again_stays_in_the_queue()
    {
        // The case that matters most. Resolving on attempt rather than on
        // success would clear the list while the record was still wrong.
        var reference = $"{_prefix}-STILLBAD";
        var id = await HoldAsync(reference, Payload(reference, lastName: null));

        await using var scope = await OpenScopeAsync();
        var integrations = scope.Services.GetRequiredService<IIntegrations>();

        var replayed = await integrations.ReplayAsync(id, CancellationToken.None);

        replayed.IsSuccess.Should().BeTrue(because: "running it again is not itself a failure");
        replayed.Value.Applied.Should().BeFalse();
        replayed.Value.ReasonCode.Should().NotBeNullOrWhiteSpace();

        var row = await HeldRowAsync(id);
        row.ResolvedAt.Should().BeNull(because: "it still does not apply, so it is still held");
        row.ReplayAttempts.Should().Be(1, because: "the row remembers it has been tried");
        row.LastReplayedAt.Should().NotBeNull();

        (await FindAsync(reference)).Should().BeNull(
            because: "a refused replay must leave nothing behind");
    }

    [Fact]
    public async Task A_held_record_is_listed_without_its_payload()
    {
        // ADR-022: the payload is a customer's name, address and telephone
        // number exactly as a provider sent them. A review queue that lists
        // those is a personal-data export with a queue painted on it.
        var reference = $"{_prefix}-PRIVATE";
        var id = await HoldAsync(reference, Payload(reference, lastName: "Sensitive"));

        await using var scope = await OpenScopeAsync();
        var integrations = scope.Services.GetRequiredService<IIntegrations>();

        var listed = await integrations.QuarantineAsync(_rooftop, CancellationToken.None);

        listed.IsSuccess.Should().BeTrue();
        var entry = listed.Value.Should().ContainSingle(e => e.Id == id).Subject;

        entry.ExternalId.Should().Be(reference);
        entry.ReasonCode.Should().NotBeNullOrWhiteSpace();

        // The shape itself carries no payload field, so this is a statement
        // about the contract rather than about one response.
        typeof(QuarantineEntry).GetProperty("Payload").Should().BeNull(
            because: "the held fields must not be reachable through the list at all");
    }

    [Fact]
    public async Task A_record_already_dealt_with_cannot_be_replayed()
    {
        var reference = $"{_prefix}-DONE";
        var id = await HoldAsync(reference, Payload(reference, lastName: "Done"));

        await using var scope = await OpenScopeAsync();
        var integrations = scope.Services.GetRequiredService<IIntegrations>();

        await integrations.DismissAsync(id, "The provider sends this row by mistake.", CancellationToken.None);
        var again = await integrations.ReplayAsync(id, CancellationToken.None);

        again.IsFailure.Should().BeTrue();
        again.Error.Code.Should().Be("integration.already_resolved");
    }

    [Fact]
    public async Task Dismissing_without_a_reason_is_refused()
    {
        // "Resolved" with no note is how a queue gets cleared by somebody who
        // did not read it.
        var reference = $"{_prefix}-NOREASON";
        var id = await HoldAsync(reference, Payload(reference, lastName: "Whoever"));

        await using var scope = await OpenScopeAsync();
        var integrations = scope.Services.GetRequiredService<IIntegrations>();

        var refused = await integrations.DismissAsync(id, "   ", CancellationToken.None);

        refused.IsFailure.Should().BeTrue();
        refused.Error.Code.Should().Be("integration.dismissal_needs_a_reason");
        (await HeldRowAsync(id)).ResolvedAt.Should().BeNull();
    }

    [Fact]
    public async Task A_record_held_past_its_retention_cannot_be_replayed()
    {
        // The payload may still be on disk — nothing purges physically yet — but
        // it has stopped being ours to use. Replaying it would write a
        // customer's details back into the dealership after the day we said we
        // would stop holding them.
        var reference = $"{_prefix}-EXPIRED";
        var id = await HoldAsync(reference, Payload(reference, lastName: "Stale"));

        await using var scope = await OpenScopeAsync();

        var expired = new IntegrationService(
            scope.Services.GetRequiredService<TenantDb>(),
            [],
            [new CustomerRecordSink(
                scope.Services.GetRequiredService<ICustomers>(), new FixedClock(Start))],
            scope.Services.GetRequiredService<IAccessDirectory>(),
            scope.Services.GetRequiredService<ICurrentUser>(),
            scope.Services.GetRequiredService<IAuditSink>(),
            new FixedClock(Start + QuarantinedRecord.Retention + TimeSpan.FromDays(1)));

        var refused = await expired.ReplayAsync(id, CancellationToken.None);

        refused.IsFailure.Should().BeTrue();
        refused.Error.Code.Should().Be("integration.quarantine_expired");
        (await FindAsync(reference)).Should().BeNull();
    }

    [Fact]
    public async Task The_shipped_connector_says_how_far_it_has_been_proven()
    {
        // CertificationStatus had four levels and no reader. The fixture is a
        // real shipped connector that serves fabricated records and says so in
        // its own manifest; hiding it would make the screen claim the product
        // has no connectors, which is less honest than the truth.
        await using var scope = await OpenScopeAsync();
        var integrations = scope.Services.GetRequiredService<IIntegrations>();

        var listed = await integrations.ConnectorsAsync(CancellationToken.None);

        listed.IsSuccess.Should().BeTrue();
        var fixture = listed.Value.Should().ContainSingle(c => c.Provider == "Fixture").Subject;

        fixture.Certification.Should().Be("FixtureTested");
        fixture.KnownLimitations.Should().Contain(l => l.Contains("Never certify anything against this"),
            because: "the limitation is the most important sentence on the screen");
    }

    // --- Plumbing for replay --------------------------------------------------

    /// <summary>The same options the runtime serialises a held payload with.</summary>
    private static readonly JsonSerializerOptions PayloadFormat = new(JsonSerializerDefaults.Web);

    private static string Payload(string reference, string? lastName) =>
        JsonSerializer.Serialize(new Dictionary<string, string?>
        {
            [CustomerFields.Kind] = "Person",
            [CustomerFields.LastName] = lastName is null ? null : lastName + reference,
            [CustomerFields.FirstName] = "Held",
        }, PayloadFormat);

    /// <summary>
    /// A held row shaped exactly as the runtime writes one, so a replay test is
    /// exercising the real thing rather than a convenient stand-in.
    /// </summary>
    private async Task<Guid> HoldAsync(string reference, string payload)
    {
        await using var db = NewContext();

        var held = new QuarantinedRecord(
            Guid.NewGuid(),
            "Fixture",
            _rooftop,
            new ConnectorCapability(CustomerFields.Contract, CustomerFields.Version,
                SyncDirection.Read, FixtureConnector.HistoryWindow),
            reference,
            null,
            payload,
            Error.Validation("customer.missing_name", "A surname is required."),
            Start);

        db.Add(held);
        await db.SaveChangesAsync(CancellationToken.None);

        return held.Id;
    }

    private static async Task<QuarantinedRecord> HeldRowAsync(Guid id)
    {
        await using var db = NewContext();

        return await db.Set<QuarantinedRecord>()
            .AsNoTracking()
            .SingleAsync(q => q.Id == id, CancellationToken.None);
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
            [new CustomerRecordSink(
                scope.Services.GetRequiredService<ICustomers>(), new FixedClock(Start))],
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
    /// A tenant scope acting as a named person — the same shape the CSV import
    /// worker uses, which runs as the person who asked rather than as a system
    /// principal. Defaults to the organization-wide development user.
    /// </summary>
    private async Task<TenantScope> OpenScopeAsync(Guid? requestedBy = null)
    {
        var factory = _fixture.Services.GetRequiredService<ITenantScopeFactory>();

        var scope = await factory.OpenAsync(
            JobContext.RequestedBy(
                Tenant,
                requestedBy ?? DevelopmentSeeder.DevUsers.OrganizationWide,
                "connector sink test"),
            CancellationToken.None)
            ?? throw new InvalidOperationException($"The '{Tenant}' tenant did not resolve.");

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
