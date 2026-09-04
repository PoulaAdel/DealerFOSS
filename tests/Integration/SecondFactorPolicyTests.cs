// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   SecondFactorPolicyTests — proves a dealership can demand a second factor of a
//   role, and that demanding it locks nobody out.
//
// Usage:
//   Runs with the normal test suite; needs a reachable SQL engine.
//
// Coding Instructions:
//   Every test here turns the policy off again in a finally block. The policy
//   is a row shared by the whole suite, and a test that left it on would
//   refuse every other test's requests with a message about authenticator
//   apps — which is a confusing way to learn that cleanup was skipped.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using DealerFOSS.App;
using DealerFOSS.Identity;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class SecondFactorPolicyTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string PolicyEndpoint = "/api/v1/security/second-factor-policy";

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task A_manager_can_see_which_roles_demand_a_second_factor()
    {
        using var response = await GetPolicyAsync(DevelopmentSeeder.DevUsers.OrganizationWideEmail);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var roles = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        roles.Should().NotBeEmpty();
        roles.Select(r => r.GetProperty("roleName").GetString())
            .Should().Contain(IdentitySeeder.SalespersonRole);
    }

    [Fact]
    public async Task The_listing_says_how_many_people_have_not_enrolled_yet()
    {
        // An administrator about to demand a second factor of a role needs to
        // know how many people that lands on before they turn it on.
        using var response = await GetPolicyAsync(DevelopmentSeeder.DevUsers.OrganizationWideEmail);

        var sales = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray()
            .Single(r => r.GetProperty("roleName").GetString() == IdentitySeeder.SalespersonRole);

        sales.GetProperty("usersHolding").GetInt32().Should().BeGreaterThan(0);
        sales.GetProperty("usersStillToEnrol").GetInt32()
            .Should().BeGreaterThan(0, because: "nobody in the development data has enrolled");
    }

    [Fact]
    public async Task A_rooftop_scoped_user_cannot_change_the_dealerships_policy()
    {
        using var response = await GetPolicyAsync(DevelopmentSeeder.DevUsers.FirstRooftopOnlyEmail);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "a rule about the whole dealership is not set from one lot");
    }

    [Fact]
    public async Task Requiring_a_second_factor_stops_the_role_reaching_business_data()
    {
        var salesRoleId = await RoleIdAsync(IdentitySeeder.SalespersonRole);

        // Confirm the salesperson can work before the policy changes, so the
        // refusal afterwards means the policy and not something else.
        using (var before = await ReadInventoryAsync(DevelopmentSeeder.DevUsers.SalespersonEmail))
        {
            before.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        try
        {
            await SetPolicyAsync(salesRoleId, required: true);

            using var after = await ReadInventoryAsync(DevelopmentSeeder.DevUsers.SalespersonEmail);

            after.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await after.Content.ReadAsStringAsync())
                .Should().Contain("auth.second_factor_required");
        }
        finally
        {
            await SetPolicyAsync(salesRoleId, required: false);
        }
    }

    [Fact]
    public async Task The_policy_takes_effect_without_waiting_for_a_new_sign_in()
    {
        var salesRoleId = await RoleIdAsync(IdentitySeeder.SalespersonRole);

        // The same session throughout: this is the property that matters. A
        // dealership that turns the policy on at nine o'clock should not wait
        // for everyone to sign out before it means anything.
        var session = await _fixture.SignInAsync(DevelopmentSeeder.DevUsers.SalespersonEmail, Tenant);

        using (var before = await ReadInventoryAsync(session))
        {
            before.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        try
        {
            await SetPolicyAsync(salesRoleId, required: true);

            using var after = await ReadInventoryAsync(session);
            after.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            await SetPolicyAsync(salesRoleId, required: false);
        }
    }

    [Fact]
    public async Task Somebody_who_owes_a_second_factor_can_still_sign_in_and_enrol()
    {
        // The whole grace path. Turning the policy on must never leave a
        // dealership unable to satisfy it.
        var salesRoleId = await RoleIdAsync(IdentitySeeder.SalespersonRole);

        try
        {
            await SetPolicyAsync(salesRoleId, required: true);

            var session = await _fixture.SignInAsync(
                DevelopmentSeeder.DevUsers.SalespersonEmail, Tenant);

            // Signing in still works — the password was never the problem.
            session.SessionToken.Should().NotBeNullOrWhiteSpace();

            // And the caller is told, rather than left guessing.
            using (var me = await SendAsync(HttpMethod.Get, "/api/v1/auth/me", session))
            {
                me.StatusCode.Should().Be(HttpStatusCode.OK);
                (await me.Content.ReadFromJsonAsync<JsonElement>())
                    .GetProperty("mustEnrolSecondFactor").GetBoolean().Should().BeTrue();
            }

            // The way out is open.
            using var enrol = await SendAsync(HttpMethod.Post, "/api/v1/auth/mfa/enrol", session);
            enrol.StatusCode.Should().Be(HttpStatusCode.OK,
                because: await enrol.Content.ReadAsStringAsync());
        }
        finally
        {
            await SetPolicyAsync(salesRoleId, required: false);
            await ClearEnrolmentAsync(DevelopmentSeeder.DevUsers.Salesperson);
        }
    }

    [Fact]
    public async Task Enrolling_lifts_the_refusal()
    {
        var salesRoleId = await RoleIdAsync(IdentitySeeder.SalespersonRole);

        try
        {
            await SetPolicyAsync(salesRoleId, required: true);

            var session = await _fixture.SignInAsync(
                DevelopmentSeeder.DevUsers.SalespersonEmail, Tenant);

            using var begun = await SendAsync(HttpMethod.Post, "/api/v1/auth/mfa/enrol", session);
            var secret = (await begun.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("secret").GetString()!;

            using (var confirmed = await SendAsync(
                HttpMethod.Post, "/api/v1/auth/mfa/confirm", session,
                new { code = Totp.Generate(secret, DateTimeOffset.UtcNow) }))
            {
                confirmed.StatusCode.Should().Be(HttpStatusCode.OK);
            }

            using var after = await ReadInventoryAsync(session);

            after.StatusCode.Should().Be(HttpStatusCode.OK,
                because: "satisfying the policy must let the person get on with their job");
        }
        finally
        {
            await SetPolicyAsync(salesRoleId, required: false);
            await ClearEnrolmentAsync(DevelopmentSeeder.DevUsers.Salesperson);
        }
    }

    [Fact]
    public async Task A_role_that_does_not_demand_it_is_unaffected()
    {
        var salesRoleId = await RoleIdAsync(IdentitySeeder.SalespersonRole);

        try
        {
            await SetPolicyAsync(salesRoleId, required: true);

            // The manager holds a different role and must be untouched — not
            // least because the manager is who turns the policy off again.
            using var response = await ReadInventoryAsync(
                DevelopmentSeeder.DevUsers.OrganizationWideEmail);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            await SetPolicyAsync(salesRoleId, required: false);
        }
    }

    // --- helpers -----------------------------------------------------------

    private Task<HttpResponseMessage> GetPolicyAsync(string email) =>
        SendAsync(HttpMethod.Get, PolicyEndpoint, email);

    private async Task SetPolicyAsync(Guid roleId, bool required)
    {
        using var response = await SendAsync(
            HttpMethod.Post, PolicyEndpoint,
            DevelopmentSeeder.DevUsers.OrganizationWideEmail,
            new { roleId, required });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            because: await response.Content.ReadAsStringAsync());
    }

    private async Task<Guid> RoleIdAsync(string roleName)
    {
        using var response = await GetPolicyAsync(DevelopmentSeeder.DevUsers.OrganizationWideEmail);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray()
            .Single(r => r.GetProperty("roleName").GetString() == roleName)
            .GetProperty("roleId").GetGuid();
    }

    private Task<HttpResponseMessage> ReadInventoryAsync(string email) =>
        SendAsync(HttpMethod.Get, "/api/v1/inventory?limit=1", email);

    private Task<HttpResponseMessage> ReadInventoryAsync(SignedInSession session) =>
        SendAsync(HttpMethod.Get, "/api/v1/inventory?limit=1", session);

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, string email, object? body = null) =>
        await SendAsync(method, path, await _fixture.SignInAsync(email, Tenant), body);

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, SignedInSession session, object? body = null)
    {
        using var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Headers.Add("X-Tenant", Tenant);
        request.Headers.Add("Cookie", $"dfoss_session={session.SessionToken}");

        if (method != HttpMethod.Get)
        {
            request.Headers.Add("X-CSRF-Token", session.AntiForgeryToken);
            request.Content = JsonContent.Create(body ?? new { });
        }

        return await client.SendAsync(request);
    }

    /// <summary>
    /// Puts the account back directly. These tests enrol the shared salesperson
    /// account to prove the refusal lifts; leaving it enrolled would make every
    /// later sign-in demand a code.
    /// </summary>
    private static async Task ClearEnrolmentAsync(Guid userId)
    {
        await using var connection = new SqlConnection(
            HostFixture.TenantConnectionString(Tenant));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();

        // "identity" is a reserved T-SQL keyword, so it is bracketed.
        command.CommandText = """
            DELETE FROM [identity].[RecoveryCodes] WHERE UserId = @user;
            DELETE FROM [identity].[SignInChallenges] WHERE UserId = @user;
            UPDATE [identity].[Users]
               SET MfaSecretProtected = NULL, MfaConfirmedAt = NULL
             WHERE Id = @user;
            """;
        command.Parameters.AddWithValue("@user", userId);

        await command.ExecuteNonQueryAsync();
    }
}
