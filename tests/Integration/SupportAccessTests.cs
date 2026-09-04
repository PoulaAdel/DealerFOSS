// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   SupportAccessTests — proves the one door from operating the deployment into a
//   dealership's data is deliberate, limited, visible, and closable.
//
// Usage:
//   Runs with the normal test suite; needs a reachable SQL engine.
//
// Coding Instructions:
//   Four properties are what make this a control rather than a back door, and
//   each has a test below. It needs a written reason. It is read-only. It
//   appears in the dealership's own audit trail, not only in ours. And ending
//   it stops the session on the very next request.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class SupportAccessTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task Support_access_without_a_reason_is_refused()
    {
        var administrator = await _fixture.AdministratorAsync();

        using var response = await OpenAsync(administrator, reason: "   ", minutes: 30);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("code").GetString().Should().Be("admin.support_reason_required",
                because: "access nobody had to justify is a back door with a timestamp");
    }

    [Fact]
    public async Task Support_access_needs_a_dealership_to_be_named()
    {
        var administrator = await _fixture.AdministratorAsync();

        // No X-Tenant header: the control plane belongs to no single dealership,
        // so "enter one" has to say which.
        using var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri("/api/v1/admin/support-access", UriKind.Relative))
        {
            Content = JsonContent.Create(new { reason = "Investigating a posting error.", minutes = 30 }),
        };
        request.Headers.Add("Cookie", $"dfoss_admin={administrator.SessionToken}");
        request.Headers.Add("X-Admin-CSRF-Token", administrator.AntiForgeryToken);

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("code").GetString().Should().Be("admin.support_tenant_required");
    }

    [Fact]
    public async Task A_granted_session_can_read_the_dealerships_records_but_not_change_them()
    {
        var administrator = await _fixture.AdministratorAsync();
        var granted = await GrantAsync(administrator, "Customer reports the stock list is empty.");

        try
        {
            using var read = await TenantCallAsync(
                HttpMethod.Get, "/api/v1/inventory", granted);

            read.StatusCode.Should().Be(HttpStatusCode.OK,
                because: "support that cannot see the problem cannot help with it");

            using var write = await TenantCallAsync(
                HttpMethod.Post, "/api/v1/customers", granted,
                body: new { customerKind = "Person", firstName = "Support", lastName = "Should Not Exist" });

            write.StatusCode.Should().Be(HttpStatusCode.Forbidden,
                because: "the vendor looks; the dealership decides");
        }
        finally
        {
            await EndAsync(administrator, granted.GrantId);
        }
    }

    [Fact]
    public async Task The_dealership_can_see_in_their_own_log_that_somebody_came_in()
    {
        var administrator = await _fixture.AdministratorAsync();
        const string Reason = "Diagnosing a duplicated journal entry raised on ticket 4412.";

        var granted = await GrantAsync(administrator, Reason);

        try
        {
            // Read the tenant's own audit trail directly. Visibility that only
            // exists in the vendor's console is not visibility.
            var entry = await ScalarAsync(
                HostFixture.TenantConnectionString(Tenant),
                "SELECT TOP 1 Reason FROM [identity].[AuditEvents] "
                + "WHERE Action = 'Support.AccessOpened' ORDER BY OccurredAt DESC");

            entry.Should().NotBeNull();
            entry.Should().Contain(DevelopmentSeeder.DevAdministrator.Email,
                because: "the dealership must be able to see who came in");
            entry.Should().Contain(Reason,
                because: "and why they said they were coming");
        }
        finally
        {
            await EndAsync(administrator, granted.GrantId);
        }
    }

    [Fact]
    public async Task The_support_principal_cannot_be_signed_into_with_a_password()
    {
        var administrator = await _fixture.AdministratorAsync();

        // Created by the first grant, and then permanently present in the
        // dealership's user list — so it had better be unopenable.
        var granted = await GrantAsync(administrator, "Confirming the support account exists.");
        await EndAsync(administrator, granted.GrantId);

        using var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri("/api/v1/auth/login", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                email = "support@dealerfoss.invalid",
                password = DevelopmentSeeder.DevUsers.Password,
            }),
        };
        request.Headers.Add("X-Tenant", Tenant);

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "the account holds no password hash, so no credential opens it — "
                + "a grant is the only way to hold a session as this user");
    }

    [Fact]
    public async Task Ending_the_grant_stops_the_session_on_the_very_next_request()
    {
        var administrator = await _fixture.AdministratorAsync();
        var granted = await GrantAsync(administrator, "Checking a report of missing stock.");

        using var before = await TenantCallAsync(HttpMethod.Get, "/api/v1/inventory", granted);
        before.StatusCode.Should().Be(HttpStatusCode.OK);

        await EndAsync(administrator, granted.GrantId);

        using var after = await TenantCallAsync(HttpMethod.Get, "/api/v1/inventory", granted);
        after.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "closing the record and closing the access are one act");
    }

    [Fact]
    public async Task A_request_for_a_whole_day_gets_an_hour()
    {
        var administrator = await _fixture.AdministratorAsync();
        var before = DateTimeOffset.UtcNow;

        var granted = await GrantAsync(administrator, "Long investigation.", minutes: 1440);

        try
        {
            granted.ExpiresAt.Should().BeCloseTo(before.AddHours(1), TimeSpan.FromMinutes(2),
                because: "the ceiling is not negotiable; a longer look means asking again");
        }
        finally
        {
            await EndAsync(administrator, granted.GrantId);
        }
    }

    [Fact]
    public async Task Every_grant_is_listed_with_who_why_and_whether_it_is_still_open()
    {
        var administrator = await _fixture.AdministratorAsync();
        const string Reason = "Reproducing a rounding complaint on ticket 5510.";

        var granted = await GrantAsync(administrator, Reason);
        await EndAsync(administrator, granted.GrantId);

        using var response = await AdminCallAsync(
            HttpMethod.Get, "/api/v1/admin/support-access", administrator);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var row = body.EnumerateArray()
            .First(g => g.GetProperty("id").GetGuid() == granted.GrantId);

        row.GetProperty("administratorEmail").GetString()
            .Should().Be(DevelopmentSeeder.DevAdministrator.Email);
        row.GetProperty("tenantSlug").GetString().Should().Be(Tenant);
        row.GetProperty("reason").GetString().Should().Be(Reason);
        row.GetProperty("isActive").GetBoolean().Should().BeFalse();
        row.GetProperty("endedAt").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task An_administrator_without_a_second_factor_cannot_open_support_access()
    {
        // The reserved never-enrolled operator. This is the reason the second
        // factor is mandatory rather than encouraged: the account that can step
        // into any dealership is the one worth stealing.
        using var client = _fixture.CreateClient();
        using var login = await client.PostAsJsonAsync(
            new Uri("/api/v1/admin/login", UriKind.Relative),
            new
            {
                email = DevelopmentSeeder.DevAdministrator.UnenrolledEmail,
                password = DevelopmentSeeder.DevUsers.Password,
            });

        login.EnsureSuccessStatusCode();
        var session = CookieValue(login, "dfoss_admin");
        var antiForgery = CookieValue(login, "dfoss_admin_csrf");

        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri("/api/v1/admin/support-access", UriKind.Relative))
        {
            Content = JsonContent.Create(new { reason = "Trying it on.", minutes = 30 }),
        };
        request.Headers.Add("Cookie", $"dfoss_admin={session}");
        request.Headers.Add("X-Admin-CSRF-Token", antiForgery);
        request.Headers.Add("X-Tenant", Tenant);

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("code").GetString().Should().Be("admin.second_factor_required");
    }

    // --- helpers ---

    private sealed record Granted(Guid GrantId, string SessionToken, string AntiForgeryToken, DateTimeOffset ExpiresAt);

    private async Task<Granted> GrantAsync(
        SignedInAdministrator administrator, string reason, int minutes = 30)
    {
        using var response = await OpenAsync(administrator, reason, minutes);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: await response.Content.ReadAsStringAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        return new Granted(
            body.GetProperty("grantId").GetGuid(),
            CookieValue(response, "dfoss_session"),
            CookieValue(response, "dfoss_csrf"),
            body.GetProperty("expiresAt").GetDateTimeOffset());
    }

    private async Task<HttpResponseMessage> OpenAsync(
        SignedInAdministrator administrator, string reason, int minutes)
    {
        var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri("/api/v1/admin/support-access", UriKind.Relative))
        {
            Content = JsonContent.Create(new { reason, minutes }),
        };
        request.Headers.Add("Cookie", $"dfoss_admin={administrator.SessionToken}");
        request.Headers.Add("X-Admin-CSRF-Token", administrator.AntiForgeryToken);
        request.Headers.Add("X-Tenant", Tenant);

        try
        {
            return await client.SendAsync(request);
        }
        finally
        {
            client.Dispose();
        }
    }

    private async Task EndAsync(SignedInAdministrator administrator, Guid grantId)
    {
        using var response = await AdminCallAsync(
            HttpMethod.Post, $"/api/v1/admin/support-access/{grantId}/end", administrator, Tenant);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            because: await response.Content.ReadAsStringAsync());
    }

    private async Task<HttpResponseMessage> AdminCallAsync(
        HttpMethod method, string path, SignedInAdministrator administrator, string? tenant = null)
    {
        var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Headers.Add("Cookie", $"dfoss_admin={administrator.SessionToken}");
        request.Headers.Add("X-Admin-CSRF-Token", administrator.AntiForgeryToken);

        if (tenant is not null)
        {
            request.Headers.Add("X-Tenant", tenant);
        }

        try
        {
            return await client.SendAsync(request);
        }
        finally
        {
            client.Dispose();
        }
    }

    private async Task<HttpResponseMessage> TenantCallAsync(
        HttpMethod method, string path, Granted granted, object? body = null)
    {
        var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.Headers.Add("Cookie", $"dfoss_session={granted.SessionToken}");
        request.Headers.Add("X-Tenant", Tenant);

        if (method != HttpMethod.Get)
        {
            request.Headers.Add("X-CSRF-Token", granted.AntiForgeryToken);
        }

        try
        {
            return await client.SendAsync(request);
        }
        finally
        {
            client.Dispose();
        }
    }

    private static async Task<string?> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        return await command.ExecuteScalarAsync() as string;
    }

    private static string CookieValue(HttpResponseMessage response, string name)
    {
        var header = response.Headers.GetValues("Set-Cookie")
            .Single(v => v.StartsWith($"{name}=", StringComparison.Ordinal));

        return Uri.UnescapeDataString(header.Split(';')[0][(name.Length + 1)..]);
    }
}
