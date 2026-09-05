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
//   two assert the state of a freshly opened scope with nothing done to it —
//   resist the urge to "arrange" anything before the assertion, because the
//   arrangement is what used to be the bug.
//
//   The last two go through a real service rather than stopping at the holder,
//   because a holder that reports the right answer while the row records the
//   wrong one is the failure that matters. One reads the stamp back with SQL;
//   the other asserts that an unattended job is refused at the permission check,
//   which is the intended shape and not a limitation to route around.

using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using DealerFOSS.App;
using DealerFOSS.Core;
using DealerFOSS.Customers;
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
    public async Task A_scope_nobody_asked_for_has_no_caller_and_says_so_loudly()
    {
        await using var scope = await Factory.OpenAsync(
            JobContext.Unattended(Tenant, "job scope test sweep"),
            CancellationToken.None);

        scope.Should().NotBeNull();

        var caller = scope!.Services.GetRequiredService<ICurrentUser>();
        caller.IsAuthenticated.Should().BeFalse();

        // Throwing is the correct outcome for a sweep that wanders into a
        // permission check. Returning Guid.Empty would let it pass one.
        var read = () => caller.Id;
        read.Should().Throw<InvalidOperationException>();

        // It still knows its dealership, which is the whole reason it exists.
        scope.Services.GetRequiredService<ITenantContext>().Current.Key.Should().Be(Tenant);
        scope.Job.IsUnattended.Should().BeTrue();
    }

    [Fact]
    public async Task A_suspended_dealership_refuses_the_scope_rather_than_defaulting()
    {
        await using var scope = await Factory.OpenAsync(
            JobContext.Unattended("no-such-dealership", "job scope test sweep"),
            CancellationToken.None);

        scope.Should().BeNull(
            because: "queued work for a dealership out of service waits; it does not run somewhere else");
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
    public async Task An_unattended_job_cannot_reach_a_permission_checked_service_at_all()
    {
        // Not a limitation to work around — the intended shape. A sweep runs as
        // nobody, so there are no permissions to check it against, and the only
        // safe answer to "may this caller write a customer" is to refuse loudly.
        // The alternative, a sweep that passes every check because there is
        // nobody to fail, is the security hole this whole change exists to close.
        await using var scope = await OpenAsync(
            JobContext.Unattended(Tenant, "job scope test sweep"));

        var write = async () => await AddCustomerAsync(scope, "Sweep");

        await write.Should().ThrowAsync<InvalidOperationException>(
            because: "an unattended job has no permissions, so it must not be granted any");
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
