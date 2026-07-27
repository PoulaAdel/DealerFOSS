// AuthenticationTests — proves signing in works, and that revoking a session
// stops it immediately rather than whenever a token would have expired.
//
// Use:  runs with the normal test suite; needs a reachable SQL engine.
// Edit: the "wrong password and unknown email look identical" case is a privacy
//       control, not a nicety — distinguishing them tells an attacker which
//       email addresses are real. Keep them indistinguishable.

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OpenDealer360.Host.Development;

namespace OpenDealer360.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class AuthenticationTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task Signing_in_with_correct_credentials_returns_a_session_cookie()
    {
        using var client = _fixture.CreateClient();

        using var response = await LogInAsync(client, DevelopmentSeeder.DevUsers.OrganizationWideEmail);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        SessionCookieFrom(response).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task The_session_cookie_is_not_readable_by_script_and_is_same_site()
    {
        using var client = _fixture.CreateClient();

        using var response = await LogInAsync(client, DevelopmentSeeder.DevUsers.OrganizationWideEmail);

        var header = response.Headers.GetValues("Set-Cookie")
            .Single(v => v.StartsWith("odms_session", StringComparison.Ordinal))
            .ToLowerInvariant();

        header.Should().Contain("httponly", because: "script must not be able to read the session");
        header.Should().Contain("samesite=strict", because: "another site must not be able to ride the cookie");
    }

    [Fact]
    public async Task The_token_is_never_returned_in_the_response_body()
    {
        using var client = _fixture.CreateClient();

        using var response = await LogInAsync(client, DevelopmentSeeder.DevUsers.OrganizationWideEmail);
        var body = await response.Content.ReadAsStringAsync();

        var token = SessionCookieFrom(response);
        body.Should().NotContain(token, because: "the token belongs in the cookie only");
    }

    [Fact]
    public async Task A_wrong_password_is_refused()
    {
        using var client = _fixture.CreateClient();

        using var response = await LogInAsync(
            client, DevelopmentSeeder.DevUsers.OrganizationWideEmail, password: "not-the-password");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_unknown_email_fails_exactly_like_a_wrong_password()
    {
        using var client = _fixture.CreateClient();

        using var unknown = await LogInAsync(client, "nobody-here@dev.local", password: "whatever");
        using var wrongPassword = await LogInAsync(
            client, DevelopmentSeeder.DevUsers.OrganizationWideEmail, password: "whatever");

        unknown.StatusCode.Should().Be(wrongPassword.StatusCode);
        (await unknown.Content.ReadAsStringAsync())
            .Should().Be(await wrongPassword.Content.ReadAsStringAsync(),
                because: "a different response would reveal which email addresses exist");
    }

    [Fact]
    public async Task A_request_without_a_session_is_refused()
    {
        using var client = _fixture.CreateClient();

        using var response = await GetOrganizationAsync(client, token: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_signed_in_user_can_reach_a_protected_endpoint()
    {
        using var client = _fixture.CreateClient();
        var token = await SignInAsync(client, DevelopmentSeeder.DevUsers.OrganizationWideEmail);

        using var response = await GetOrganizationAsync(client, token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Signing_out_stops_the_session_immediately()
    {
        using var client = _fixture.CreateClient();
        var token = await SignInAsync(client, DevelopmentSeeder.DevUsers.OrganizationWideEmail);

        // Confirm it works before revoking, so the failure afterwards means
        // something.
        using (var before = await GetOrganizationAsync(client, token))
        {
            before.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using (var logout = await PostAsync(client, "/api/v1/auth/logout", token))
        {
            logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        using var after = await GetOrganizationAsync(client, token);

        after.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "revocation must take effect at once, not when the token would have expired");
    }

    [Fact]
    public async Task A_made_up_token_is_refused()
    {
        using var client = _fixture.CreateClient();

        using var response = await GetOrganizationAsync(client, Convert.ToBase64String(new byte[32]));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_session_from_one_dealer_organization_does_not_work_at_another()
    {
        using var client = _fixture.CreateClient();
        var token = await SignInAsync(client, DevelopmentSeeder.DevUsers.OrganizationWideEmail, Tenant);

        // Same token, different dealer organization: the session lives in the
        // first organization's database and cannot be found in the second.
        using var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri("/api/v1/organization", UriKind.Relative));
        request.Headers.Add("X-Tenant", "citymotors");
        request.Headers.Add("Cookie", $"odms_session={token}");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_reports_the_signed_in_user()
    {
        using var client = _fixture.CreateClient();
        var token = await SignInAsync(client, DevelopmentSeeder.DevUsers.FirstRooftopOnlyEmail);

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/auth/me", UriKind.Relative));
        request.Headers.Add("X-Tenant", Tenant);
        request.Headers.Add("Cookie", $"odms_session={token}");
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<MeResponse>();
        payload!.UserId.Should().Be(DevelopmentSeeder.DevUsers.FirstRooftopOnly);
    }

    // --- helpers -----------------------------------------------------------

    /// <summary>Signs in and returns the raw session token from the cookie.</summary>
    internal static async Task<string> SignInAsync(
        HttpClient client, string email, string tenant = Tenant)
    {
        using var response = await LogInAsync(client, email, tenant: tenant);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return SessionCookieFrom(response);
    }

    private static async Task<HttpResponseMessage> LogInAsync(
        HttpClient client,
        string email,
        string password = DevelopmentSeeder.DevUsers.Password,
        string tenant = Tenant)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri("/api/v1/auth/login", UriKind.Relative))
        {
            Content = JsonContent.Create(new { email, password }),
        };
        request.Headers.Add("X-Tenant", tenant);
        return await client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> GetOrganizationAsync(HttpClient client, string? token)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri("/api/v1/organization", UriKind.Relative));
        request.Headers.Add("X-Tenant", Tenant);
        if (token is not null)
        {
            request.Headers.Add("Cookie", $"odms_session={token}");
        }

        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative));
        request.Headers.Add("X-Tenant", Tenant);
        request.Headers.Add("Cookie", $"odms_session={token}");
        return client.SendAsync(request);
    }

    private static string SessionCookieFrom(HttpResponseMessage response)
    {
        var header = response.Headers.GetValues("Set-Cookie")
            .Single(v => v.StartsWith("odms_session=", StringComparison.Ordinal));

        return Uri.UnescapeDataString(header.Split(';')[0]["odms_session=".Length..]);
    }

    private sealed record MeResponse(Guid UserId);
}
