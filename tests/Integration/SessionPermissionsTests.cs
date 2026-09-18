// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   SessionPermissionsTests — what /auth/me tells the browser about the caller,
//   and the promise that telling it changes nothing about what is allowed.
//
// Usage:
//   dotnet test
//
// Coding Instructions:
//   Permissions_are_a_hint_not_a_control IS THE POINT OF THIS FILE. Everything
//   else here checks that the list is accurate; that one checks that the list
//   is powerless.
//
//   On 2026-09-18 the session began carrying what the caller holds, so the
//   navigation could stop offering doors that answer 403 — a technician used
//   to be shown Books and Staff and find out by clicking. The risk that comes
//   with it is obvious and permanent: somebody, one day, decides the browser
//   already knows and stops checking on the server, or starts branching on
//   this list inside a service. That test fails the moment either happens.
//
//   If you are here because that test is in your way: it is not in your way,
//   it is the reason the feature was allowed to exist.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using DealerFOSS.App;
using DealerFOSS.Identity;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class SessionPermissionsTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string Me = "/api/v1/auth/me";

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task Permissions_are_a_hint_not_a_control()
    {
        // A technician's session does not list Accounting.Read, so their
        // navigation draws no accounting menu. They type the address anyway.
        var held = await PermissionsFor(DevelopmentSeeder.DevUsers.TechnicianEmail);
        held.Should().NotContain(Permissions.AccountingRead);

        using var response = await GetAsync(
            "/api/v1/accounting/balances", DevelopmentSeeder.DevUsers.TechnicianEmail);

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            because: "the endpoint enforces for itself. Hiding the link was a courtesy to the "
                + "reader, and courtesy is not access control — if this ever returns 200 "
                + "because 'the browser already filters it', the filter has become the lock.");
    }

    [Fact]
    public async Task A_caller_is_told_what_they_hold()
    {
        var held = await PermissionsFor(DevelopmentSeeder.DevUsers.TechnicianEmail);

        held.Should().Contain(Permissions.ServiceWrite,
            because: "writing work up is what a technician is for");
    }

    [Fact]
    public async Task A_caller_is_not_told_they_hold_what_they_do_not()
    {
        var held = await PermissionsFor(DevelopmentSeeder.DevUsers.TechnicianEmail);

        // The split this seeded user exists to make testable: they write the
        // work up, somebody else records that the customer agreed to pay.
        held.Should().NotContain(Permissions.ServiceAuthorize);
        held.Should().NotContain(Permissions.StaffManage);
    }

    [Fact]
    public async Task Somebody_with_no_assignment_holds_nothing()
    {
        var held = await PermissionsFor(DevelopmentSeeder.DevUsers.UnassignedEmail);

        held.Should().BeEmpty(
            because: "an empty list must read as 'nothing', never as 'unfiltered' — the same "
                + "contract AuthorizedScope keeps");
    }

    [Fact]
    public async Task A_wider_role_holds_more_than_a_narrower_one()
    {
        var manager = await PermissionsFor(DevelopmentSeeder.DevUsers.OrganizationWideEmail);
        var technician = await PermissionsFor(DevelopmentSeeder.DevUsers.TechnicianEmail);

        manager.Should().Contain(Permissions.AccountingRead);
        manager.Count.Should().BeGreaterThan(technician.Count);
    }

    [Fact]
    public async Task The_list_says_nothing_about_WHERE_a_permission_is_held()
    {
        // A rooftop-scoped advisor appears here the same as somebody holding the
        // permission everywhere. That is deliberate: the question this list
        // answers is "draw the link at all?". Anything needing the scope must
        // ask IAccessDirectory, which is the only thing that knows.
        var advisor = await PermissionsFor(DevelopmentSeeder.DevUsers.FirstRooftopOnlyEmail);

        advisor.Should().Contain(Permissions.ServiceRead);
        advisor.Should().OnlyContain(p => !p.Contains("NAG-", StringComparison.Ordinal),
            because: "these are permission names, not a map of the caller's rooftops");
    }

    [Fact]
    public async Task The_list_is_ordered_so_the_response_does_not_churn()
    {
        var held = await PermissionsFor(DevelopmentSeeder.DevUsers.OrganizationWideEmail);

        held.Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    private async Task<IReadOnlyList<string>> PermissionsFor(string email)
    {
        using var response = await GetAsync(Me, email);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var root = await response.Content.ReadFromJsonAsync<JsonElement>();
        return root.GetProperty("permissions")
            .EnumerateArray()
            .Select(p => p.GetString()!)
            .ToList();
    }

    private async Task<HttpResponseMessage> GetAsync(string path, string email)
    {
        using var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        request.Headers.Add("X-Tenant", Tenant);

        var token = await _fixture.TokenForAsync(email, Tenant);
        request.Headers.Add("Cookie", $"dfoss_session={token}");

        return await client.SendAsync(request);
    }
}
