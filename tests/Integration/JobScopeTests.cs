// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   JobScopeTests — a background job's scope arrives already knowing which
//   dealership it is in and who it is running as.
//
//   The gap these close: until 2026-09-05 a worker opened a tenant scope with a
//   string and was trusted to set the caller afterwards. ImportWorker did, in
//   the middle of its own method, AFTER it had already written the row claiming
//   the job — so the claim was attributed to nobody, and a job that failed
//   before the Set line was recorded as the system's doing while one that failed
//   after it was recorded as a person's. The same event, two answers, depending
//   on how far it got.
//
// Usage:
//   dotnet test DealerFOSS.slnx -c Release
//
// Coding Instructions:
//   These are about WHEN identity exists, not whether it can be read. The first
//   ones assert the state of a freshly opened scope with nothing done to it —
//   resist the urge to "arrange" anything before the assertion, because the
//   arrangement is what used to be the bug.
//
//   One goes through a real service and reads the stamp back with SQL, because
//   a holder that reports the right answer while the row records the wrong one
//   is the failure that matters.
//
//   The last two are reflection over shape rather than behaviour, and that is
//   deliberate: the guarantee they protect is a COMPILE error, and a call that
//   does not compile cannot be written down in a test. They assert the two
//   things that produce it — Get<T> constrained to IUnattendedSafe, and no
//   IServiceProvider anywhere on UnattendedScope to route around it.

using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using DealerFOSS.App;
using DealerFOSS.Core;
using DealerFOSS.Customers;
using DealerFOSS.Data;
using DealerFOSS.Tenancy;
using Xunit;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class JobScopeTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";

    private readonly HostFixture _fixture = fixture;

    private ITenantScopeFactory Factory =>
        _fixture.Services.GetRequiredService<ITenantScopeFactory>();

    [Fact]
    public async Task A_scope_opened_for_a_person_is_already_running_as_them()
    {
        var requester = DevelopmentSeeder.DevUsers.OrganizationWide;

        await using var scope = await Factory.OpenAsync(
            JobContext.RequestedBy(Tenant, requester, "job scope test"),
            CancellationToken.None);

        scope.Should().NotBeNull();

        // Nothing has been done to this scope. That is the point: the worker
        // never gets a window in which the tenant is known and the caller is not.
        var caller = scope!.Services.GetRequiredService<ICurrentUser>();
        caller.IsAuthenticated.Should().BeTrue();
        caller.Id.Should().Be(requester);

        scope.Services.GetRequiredService<ITenantContext>().Current.Key.Should().Be(Tenant);
        scope.Job.Reason.Should().Be("job scope test");
    }

    [Fact]
    public async Task A_scope_nobody_asked_for_knows_its_dealership_and_nothing_else()
    {
        await using var scope = await Factory.OpenUnattendedAsync(
            UnattendedJob.For(Tenant, "job scope test sweep"),
            CancellationToken.None);

        scope.Should().NotBeNull();

        // It still knows its dealership, which is the whole reason it exists,
        // and the context it reaches addresses that dealership's database.
        scope!.Tenant.Key.Should().Be(Tenant);
        scope.Get<TenantDb>().Should().NotBeNull();
        scope.Job.Reason.Should().Be("job scope test sweep");

        // And it cannot ask for anything else. `scope.Get<ICustomers>()` is not
        // written here because it DOES NOT COMPILE — Get<T> is constrained to
        // IUnattendedSafe, and a capability that authorizes against a person
        // must never carry that marker. BoundaryTests guards the constraint.
    }

    [Fact]
    public async Task A_suspended_dealership_refuses_both_kinds_of_scope()
    {
        await using var attended = await Factory.OpenAsync(
            JobContext.RequestedBy(
                "no-such-dealership", DevelopmentSeeder.DevUsers.OrganizationWide, "job scope test"),
            CancellationToken.None);

        await using var sweep = await Factory.OpenUnattendedAsync(
            UnattendedJob.For("no-such-dealership", "job scope test sweep"),
            CancellationToken.None);

        attended.Should().BeNull(
            because: "queued work for a dealership out of service waits; it does not run somewhere else");
        sweep.Should().BeNull(because: "and neither does a sweep");
    }

    [Fact]
    public async Task A_row_written_by_a_job_carries_the_person_the_job_runs_as()
    {
        var requester = DevelopmentSeeder.DevUsers.OrganizationWide;

        await using var scope = await OpenAsync(
            JobContext.RequestedBy(Tenant, requester, "job scope test"));

        var created = await AddCustomerAsync(scope, $"Person-{Guid.NewGuid():N}"[..20]);

        created.IsSuccess.Should().BeTrue(
            because: $"the write must succeed before its attribution means anything: "
                + created.Error?.Message);

        (await CreatedByAsync(created.Value.Id)).Should().Be(requester.ToString(),
            because: "a background job's rows carry whoever asked for the job, "
                + "and nobody had to remember to say so");
    }

    [Fact]
    public void A_permission_checked_capability_is_not_reachable_from_an_unattended_scope()
    {
        // The compile error itself cannot be written down here, so this asserts
        // the constraint that produces it: Get<T> only accepts IUnattendedSafe,
        // and ICustomers does not carry the marker. Mark ICustomers and this
        // fails — which is the moment somebody would be about to let a sweep
        // write customer records with nobody to authorize it.
        var get = typeof(UnattendedScope).GetMethod(nameof(UnattendedScope.Get))!;
        var constraints = get.GetGenericArguments()[0].GetGenericParameterConstraints();

        constraints.Should().Contain(typeof(IUnattendedSafe),
            because: "the constraint is the whole mechanism; widening it removes the guarantee silently");

        typeof(IUnattendedSafe).IsAssignableFrom(typeof(ICustomers)).Should().BeFalse(
            because: "a capability that reads ICurrentUser.Id must never be reachable with no caller");

        typeof(IUnattendedSafe).IsAssignableFrom(typeof(TenantDb)).Should().BeTrue(
            because: "the dispatcher has to be able to claim a job, and that write is honestly the system's");
    }

    [Fact]
    public async Task An_unattended_scope_hands_out_no_service_provider_to_route_around_it()
    {
        // The escape that would undo everything: one property returning
        // IServiceProvider and Get<T>'s constraint means nothing.
        await using var scope = await Factory.OpenUnattendedAsync(
            UnattendedJob.For(Tenant, "job scope test sweep"),
            CancellationToken.None);

        scope.Should().NotBeNull();

        typeof(UnattendedScope).GetProperties()
            .Should().NotContain(p => typeof(IServiceProvider).IsAssignableFrom(p.PropertyType));
        typeof(UnattendedScope).GetMethods()
            .Should().NotContain(m => typeof(IServiceProvider).IsAssignableFrom(m.ReturnType));
    }

    private async Task<TenantScope> OpenAsync(JobContext job) =>
        await Factory.OpenAsync(job, CancellationToken.None)
            ?? throw new InvalidOperationException($"The '{job.TenantKey}' tenant did not resolve.");

    private static Task<Result<CustomerDetail>> AddCustomerAsync(TenantScope scope, string surname) =>
        scope.Services.GetRequiredService<ICustomers>().AddAsync(
            new NewCustomer(
                Kind: "Person",
                FirstName: "Jobscope",
                LastName: surname,
                HomeRooftopId: FirstRooftop(scope),
                Email: null,
                Phone: null,
                Address: null),
            CancellationToken.None);

    private static RooftopId FirstRooftop(TenantScope scope)
    {
        var db = scope.Services.GetRequiredService<DealerFOSS.Data.TenantDb>();

        return db.Rooftops.OrderBy(r => r.Name).Select(r => r.Id).First();
    }

    private static async Task<string?> CreatedByAsync(Guid customerId)
    {
        await using var connection = new SqlConnection(HostFixture.TenantConnectionString(Tenant));
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CreatedBy FROM customers.Customers WHERE Id = @id";
        command.Parameters.AddWithValue("@id", customerId);

        var value = await command.ExecuteScalarAsync();
        return value == DBNull.Value ? null : value?.ToString();
    }
}
