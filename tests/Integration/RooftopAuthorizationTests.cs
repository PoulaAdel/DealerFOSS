// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   RooftopAuthorizationTests — proves a user assigned to one rooftop cannot read
//   another, by any route. This is the evidence that closes risk R05.
//
// Usage:
//   Runs with the normal test suite; needs a reachable SQL engine.
//
// Coding Instructions:
//   These must fail if the scope check in OrganizationService is removed —
//   that is the point of them, and it has been rehearsed. Cover both routes
//   when you extend them: filtering a list is not enough if the record can
//   still be fetched by id.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

/// <summary>
/// Closes risk R05 (tenant or rooftop authorization leak). The multi-rooftop
/// organization "northgroup" has two rooftops; a user assigned to one of them
/// must not be able to read the other, by any route.
/// </summary>
/// <remarks>
/// These tests fail if the scope check in <c>OrganizationService</c> is removed
/// — that is the point of them. See docs/implementation/STATUS.md.
/// </remarks>
[Collection(nameof(HostCollection))]
public sealed class RooftopAuthorizationTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string StructureEndpoint = "/api/v1/organization";

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task Organization_wide_user_sees_every_rooftop()
    {
        var codes = await GetVisibleRooftopCodesAsync(DevelopmentSeeder.DevUsers.OrganizationWide);

        codes.Should().BeEquivalentTo(["NAG-01", "NAG-02"]);
    }

    [Fact]
    public async Task Rooftop_scoped_user_sees_only_their_own_rooftop()
    {
        var codes = await GetVisibleRooftopCodesAsync(DevelopmentSeeder.DevUsers.FirstRooftopOnly);

        codes.Should().BeEquivalentTo(["NAG-01"],
            because: "the response must not carry a rooftop the caller cannot reach");
    }

    [Fact]
    public async Task Rooftop_scoped_user_is_denied_a_sibling_rooftop_by_direct_id()
    {
        // Filtering the list is not enough: addressing the rooftop directly
        // must also be refused.
        var siblingId = await GetRooftopIdAsync("NAG-02");

        using var response = await SendAsync(
            $"{StructureEndpoint}/rooftops/{siblingId}",
            DevelopmentSeeder.DevUsers.FirstRooftopOnly);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rooftop_scoped_user_may_read_their_own_rooftop_by_direct_id()
    {
        var ownId = await GetRooftopIdAsync("NAG-01");

        using var response = await SendAsync(
            $"{StructureEndpoint}/rooftops/{ownId}",
            DevelopmentSeeder.DevUsers.FirstRooftopOnly);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task User_without_any_assignment_is_denied()
    {
        using var response = await SendAsync(StructureEndpoint, DevelopmentSeeder.DevUsers.Unassigned);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "no assignment must mean no access, never unfiltered access");
    }

    [Fact]
    public async Task Request_without_an_identified_user_is_refused()
    {
        using var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(StructureEndpoint, UriKind.Relative));
        request.Headers.Add("X-Tenant", Tenant);

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Denial_is_recorded_as_an_audit_event()
    {
        var siblingId = await GetRooftopIdAsync("NAG-02");

        using var denied = await SendAsync(
            $"{StructureEndpoint}/rooftops/{siblingId}",
            DevelopmentSeeder.DevUsers.FirstRooftopOnly);
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var count = await CountDeniedAuditEventsAsync(DevelopmentSeeder.DevUsers.FirstRooftopOnly);
        count.Should().BeGreaterThan(0, because: "a refused access attempt must leave an audit trail");
    }

    private async Task<IReadOnlyList<string>> GetVisibleRooftopCodesAsync(Guid userId)
    {
        using var response = await SendAsync(StructureEndpoint, userId);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var root = await response.Content.ReadFromJsonAsync<JsonElement>();
        return root.GetProperty("legalEntities")
            .EnumerateArray()
            .SelectMany(entity => entity.GetProperty("rooftops").EnumerateArray())
            .Select(rooftop => rooftop.GetProperty("code").GetString()!)
            .ToList();
    }

    private async Task<string> GetRooftopIdAsync(string code)
    {
        // Read through the organization-wide user, which can see both rooftops.
        using var response = await SendAsync(StructureEndpoint, DevelopmentSeeder.DevUsers.OrganizationWide);
        var root = await response.Content.ReadFromJsonAsync<JsonElement>();

        return root.GetProperty("legalEntities")
            .EnumerateArray()
            .SelectMany(entity => entity.GetProperty("rooftops").EnumerateArray())
            .Single(rooftop => rooftop.GetProperty("code").GetString() == code)
            .GetProperty("id")
            .ToString();
    }

    private static async Task<int> CountDeniedAuditEventsAsync(Guid actorUserId)
    {
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(
            HostFixture.TenantConnectionString(Tenant));

        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        // "identity" is a T-SQL reserved keyword, so the schema must be bracketed
        // in hand-written SQL. EF quotes it automatically.
        command.CommandText =
            "SELECT COUNT(*) FROM [identity].[AuditEvents] WHERE ActorUserId = @actor AND Outcome = 'Denied'";
        command.Parameters.AddWithValue("@actor", actorUserId);

        return (int)(await command.ExecuteScalarAsync())!;
    }

    private async Task<HttpResponseMessage> SendAsync(string path, Guid userId)
    {
        using var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        request.Headers.Add("X-Tenant", Tenant);

        var token = await _fixture.TokenForAsync(EmailFor(userId), Tenant);
        request.Headers.Add("Cookie", $"dfoss_session={token}");

        return await client.SendAsync(request);
    }

    /// <summary>Maps a well-known development user id to the address they sign in with.</summary>
    private static string EmailFor(Guid userId) =>
        userId == DevelopmentSeeder.DevUsers.OrganizationWide
            ? DevelopmentSeeder.DevUsers.OrganizationWideEmail
            : userId == DevelopmentSeeder.DevUsers.FirstRooftopOnly
                ? DevelopmentSeeder.DevUsers.FirstRooftopOnlyEmail
                : DevelopmentSeeder.DevUsers.UnassignedEmail;
}
