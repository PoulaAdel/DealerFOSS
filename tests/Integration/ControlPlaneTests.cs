// ControlPlaneTests — proves that running the deployment and reading a
// dealership's records are different jobs held by different identities.
//
// Use:  runs with the normal test suite; needs a reachable SQL engine.
// Edit: the assertions to protect are the two crossings. An administrator cookie
//       must not authenticate a business endpoint, and a dealership cookie must
//       not authenticate the control plane. Both are refused before any endpoint
//       runs, so a capability added next year is covered without being told.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class ControlPlaneTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string Password = DevelopmentSeeder.DevUsers.Password;

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task An_administrator_session_cannot_read_a_dealerships_data()
    {
        var administrator = await _fixture.AdministratorAsync();

        using var response = await SendAsync(
            HttpMethod.Get,
            "/api/v1/inventory",
            adminCookie: administrator.SessionToken,
            tenant: Tenant);

        // 401 and not 403: to a business endpoint this caller is not a caller at
        // all. There is no code path that turns an administrator into one.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "operating the installation is not permission to read what is in it");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("auth.session_required");
    }

    [Fact]
    public async Task A_dealership_session_cannot_reach_the_control_plane()
    {
        var session = await _fixture.SignInAsync(
            DevelopmentSeeder.DevUsers.OrganizationWideEmail, Tenant);

        using var response = await SendAsync(
            HttpMethod.Get, "/api/v1/admin/tenants", sessionCookie: session.SessionToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("admin.session_required",
            because: "the most privileged dealership account is still a dealership account");
    }

    [Fact]
    public async Task An_administrator_is_not_a_user_in_any_dealerships_database()
    {
        // The same address and password that open the control plane must mean
        // nothing at a dealership's sign-in. If this ever passes, the two stores
        // have been merged and the separation is gone.
        using var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri("/api/v1/auth/login", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                email = DevelopmentSeeder.DevAdministrator.Email,
                password = Password,
            }),
        };
        request.Headers.Add("X-Tenant", Tenant);

        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_new_administrator_may_only_enrol_until_they_hold_a_second_factor()
    {
        // A fresh operator account, reserved for this test because enrolling is
        // one-way and the shared one is already enrolled.
        var (session, antiForgery) = await SignInUnenrolledAsync();

        using var me = await SendAsync(HttpMethod.Get, "/api/v1/admin/me", adminCookie: session);
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        (await me.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("mustEnrolSecondFactor").GetBoolean().Should().BeTrue();

        using var tenants = await SendAsync(
            HttpMethod.Get, "/api/v1/admin/tenants", adminCookie: session);

        tenants.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "an administrator without a second factor is the most valuable "
                + "credential in the deployment");

        var problem = await tenants.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("admin.second_factor_required");

        // And the way out is open, in the same session, with no second sign-in.
        using var enrol = await SendAsync(
            HttpMethod.Post, "/api/v1/admin/mfa/enrol",
            adminCookie: session, adminAntiForgery: antiForgery);

        enrol.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "an account that cannot enrol is an account nobody can rescue");
    }

    [Fact]
    public async Task The_enrolled_administrator_can_see_which_dealerships_exist()
    {
        var administrator = await _fixture.AdministratorAsync();

        using var response = await SendAsync(
            HttpMethod.Get, "/api/v1/admin/tenants", adminCookie: administrator.SessionToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: await response.Content.ReadAsStringAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var slugs = body.EnumerateArray().Select(t => t.GetProperty("slug").GetString()).ToList();
        slugs.Should().Contain(["northgroup", "citymotors"]);

        // Routing and lifecycle only. A field carrying anything a dealership
        // would call theirs does not belong on this endpoint.
        var first = body.EnumerateArray().First();
        first.TryGetProperty("connectionString", out _).Should().BeFalse();
        first.TryGetProperty("protectedConnectionString", out _).Should().BeFalse();
    }

    [Fact]
    public async Task A_control_plane_write_without_its_own_token_is_refused()
    {
        var administrator = await _fixture.AdministratorAsync();

        using var forged = await SendAsync(
            HttpMethod.Post, "/api/v1/admin/tenants/citymotors/status",
            adminCookie: administrator.SessionToken,
            body: new { status = "Active" });

        forged.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await forged.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("code").GetString().Should().Be("admin.antiforgery_failed");
    }

    [Fact]
    public async Task A_dealerships_anti_forgery_token_does_not_authorize_a_control_plane_write()
    {
        var administrator = await _fixture.AdministratorAsync();
        var session = await _fixture.SignInAsync(
            DevelopmentSeeder.DevUsers.OrganizationWideEmail, Tenant);

        // The token is real, and belongs to a real session — just not to this
        // one, in this store. Holding both cookies at once is the ordinary state
        // of a browser during support access, so the two must not be confusable.
        using var response = await SendAsync(
            HttpMethod.Post, "/api/v1/admin/tenants/citymotors/status",
            adminCookie: administrator.SessionToken,
            adminAntiForgery: session.AntiForgeryToken,
            body: new { status = "Active" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("code").GetString().Should().Be("admin.antiforgery_failed");
    }

    [Fact]
    public async Task Suspending_a_dealership_takes_it_out_of_service_and_resuming_brings_it_back()
    {
        var administrator = await _fixture.AdministratorAsync();

        try
        {
            using var suspend = await SendAsync(
                HttpMethod.Post, "/api/v1/admin/tenants/citymotors/status",
                adminCookie: administrator.SessionToken,
                adminAntiForgery: administrator.AntiForgeryToken,
                body: new { status = "Suspended" });

            suspend.StatusCode.Should().Be(HttpStatusCode.OK,
                because: await suspend.Content.ReadAsStringAsync());

            // Routing is cached in process. If the endpoint forgot to invalidate
            // it, a suspension would take effect whenever the cache happened to
            // expire — which is not a control.
            using var refused = await SendAsync(
                HttpMethod.Get, "/api/v1/organization", tenant: "citymotors");

            refused.StatusCode.Should().Be(HttpStatusCode.NotFound,
                because: "a suspended dealership stops resolving immediately");
        }
        finally
        {
            using var resume = await SendAsync(
                HttpMethod.Post, "/api/v1/admin/tenants/citymotors/status",
                adminCookie: administrator.SessionToken,
                adminAntiForgery: administrator.AntiForgeryToken,
                body: new { status = "Active" });

            resume.EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task Signing_out_of_the_control_plane_takes_effect_on_the_next_request()
    {
        var (session, antiForgery) = await SignInUnenrolledAsync();

        using var out_ = await SendAsync(
            HttpMethod.Post, "/api/v1/admin/logout",
            adminCookie: session, adminAntiForgery: antiForgery);
        out_.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var after = await SendAsync(HttpMethod.Get, "/api/v1/admin/me", adminCookie: session);
        after.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Signs the reserved, never-enrolled operator account in and returns its
    /// cookies. A fresh session each time, so these tests do not interfere.
    /// </summary>
    private async Task<(string Session, string AntiForgery)> SignInUnenrolledAsync()
    {
        using var client = _fixture.CreateClient();
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/admin/login", UriKind.Relative),
            new { email = DevelopmentSeeder.DevAdministrator.UnenrolledEmail, password = Password });

        response.EnsureSuccessStatusCode();
        return (CookieValue(response, "dfoss_admin"), CookieValue(response, "dfoss_admin_csrf"));
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string? adminCookie = null,
        string? adminAntiForgery = null,
        string? sessionCookie = null,
        string? tenant = null,
        object? body = null)
    {
        var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        var cookies = new List<string>();
        if (adminCookie is not null)
        {
            cookies.Add($"dfoss_admin={adminCookie}");
        }

        if (sessionCookie is not null)
        {
            cookies.Add($"dfoss_session={sessionCookie}");
        }

        if (cookies.Count > 0)
        {
            request.Headers.Add("Cookie", string.Join("; ", cookies));
        }

        if (adminAntiForgery is not null)
        {
            request.Headers.Add("X-Admin-CSRF-Token", adminAntiForgery);
        }

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

    /// <summary>
    /// Cookie values are percent-encoded on the way out. ASP.NET decodes an
    /// incoming Cookie header but not a plain one, so a token echoed into a
    /// header has to be decoded here or it will not match.
    /// </summary>
    private static string CookieValue(HttpResponseMessage response, string name)
    {
        var header = response.Headers.GetValues("Set-Cookie")
            .Single(v => v.StartsWith($"{name}=", StringComparison.Ordinal));

        return Uri.UnescapeDataString(header.Split(';')[0][(name.Length + 1)..]);
    }
}
