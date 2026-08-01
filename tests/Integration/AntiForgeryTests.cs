// AntiForgeryTests — proves a write needs more than the session cookie.
//
// Use:  runs with the normal test suite; needs a reachable SQL engine.
// Edit: the case that matters most is "valid session, no token" — that is
//       exactly the shape of a request a malicious site can cause a signed-in
//       browser to send. If that one ever returns anything other than 403, the
//       protection is gone regardless of what the other tests say.

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OpenDealer360.App;

namespace OpenDealer360.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class AntiForgeryTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string Endpoint = "/api/v1/customers";

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task Signing_in_hands_the_browser_a_token_it_can_read()
    {
        using var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri("/api/v1/auth/login", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                email = DevelopmentSeeder.DevUsers.OrganizationWideEmail,
                password = DevelopmentSeeder.DevUsers.Password,
            }),
        };
        request.Headers.Add("X-Tenant", Tenant);

        using var response = await client.SendAsync(request);

        var header = response.Headers.GetValues("Set-Cookie")
            .Single(v => v.StartsWith("odms_csrf=", StringComparison.Ordinal))
            .ToLowerInvariant();

        header.Should().NotContain("httponly",
            because: "the client has to read this one to put it in a header");
        header.Should().Contain("samesite=strict",
            because: "it should travel under the same rule as the session it belongs to");
    }

    [Fact]
    public async Task A_write_with_a_valid_session_but_no_token_is_refused()
    {
        var session = await _fixture.SignInAsync(
            DevelopmentSeeder.DevUsers.OrganizationWideEmail, Tenant);

        using var response = await WriteAsync(session.SessionToken, antiForgeryToken: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "this is precisely what a cross-site forged write looks like");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("auth.antiforgery_failed",
            because: "the caller needs a stable code saying which check refused");
    }

    [Fact]
    public async Task A_write_with_a_made_up_token_is_refused()
    {
        var session = await _fixture.SignInAsync(
            DevelopmentSeeder.DevUsers.OrganizationWideEmail, Tenant);

        using var response = await WriteAsync(
            session.SessionToken, Convert.ToBase64String(new byte[32]));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_token_from_another_session_does_not_work()
    {
        var mine = await _fixture.SignInAsync(
            DevelopmentSeeder.DevUsers.OrganizationWideEmail, Tenant);
        var theirs = await _fixture.SignInAsync(
            DevelopmentSeeder.DevUsers.FirstRooftopOnlyEmail, Tenant);

        using var response = await WriteAsync(mine.SessionToken, theirs.AntiForgeryToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "a token that authorizes writes on any session is not protection at all");
    }

    [Fact]
    public async Task A_write_with_the_matching_token_goes_through()
    {
        var session = await _fixture.SignInAsync(
            DevelopmentSeeder.DevUsers.OrganizationWideEmail, Tenant);

        using var response = await WriteAsync(session.SessionToken, session.AntiForgeryToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            because: await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Reads_do_not_need_a_token()
    {
        var session = await _fixture.SignInAsync(
            DevelopmentSeeder.DevUsers.OrganizationWideEmail, Tenant);

        using var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri(Endpoint, UriKind.Relative));
        request.Headers.Add("X-Tenant", Tenant);
        request.Headers.Add("Cookie", $"odms_session={session.SessionToken}");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "requiring a token to read would break every link into the application");
    }

    [Fact]
    public async Task The_two_tokens_are_independent_secrets()
    {
        var session = await _fixture.SignInAsync(
            DevelopmentSeeder.DevUsers.SalespersonEmail, Tenant);

        session.AntiForgeryToken.Should().NotBeNullOrWhiteSpace();
        session.AntiForgeryToken.Should().NotBe(session.SessionToken,
            because: "the readable one must not be derivable from the credential one");
    }

    [Fact]
    public async Task A_revoked_session_cannot_write_even_with_its_own_token()
    {
        using var client = _fixture.CreateClient();

        // A session of its own, so signing out does not disturb the cached ones
        // the rest of the suite shares.
        using var login = await client.SendAsync(LoginRequest());
        var sessionToken = CookieFrom(login, "odms_session");
        var antiForgeryToken = CookieFrom(login, "odms_csrf");

        using (var logout = await client.SendAsync(
            Authenticated(HttpMethod.Post, "/api/v1/auth/logout", sessionToken, antiForgeryToken)))
        {
            logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        using var response = await WriteAsync(sessionToken, antiForgeryToken);

        // Either refusal is correct: the session check runs first and answers
        // 401, and the anti-forgery check behind it would answer 403.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    // --- helpers -----------------------------------------------------------

    private async Task<HttpResponseMessage> WriteAsync(string sessionToken, string? antiForgeryToken)
    {
        using var client = _fixture.CreateClient();
        using var request = Authenticated(HttpMethod.Post, Endpoint, sessionToken, antiForgeryToken);
        request.Content = JsonContent.Create(new
        {
            kind = "Person",
            firstName = "Forgery",
            lastName = $"Case{Guid.NewGuid():N}"[..12],
        });

        return await client.SendAsync(request);
    }

    private static HttpRequestMessage Authenticated(
        HttpMethod method, string path, string sessionToken, string? antiForgeryToken)
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Headers.Add("X-Tenant", Tenant);
        request.Headers.Add("Cookie", $"odms_session={sessionToken}");

        if (antiForgeryToken is not null)
        {
            request.Headers.Add("X-CSRF-Token", antiForgeryToken);
        }

        return request;
    }

    private static HttpRequestMessage LoginRequest()
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri("/api/v1/auth/login", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                email = DevelopmentSeeder.DevUsers.OrganizationWideEmail,
                password = DevelopmentSeeder.DevUsers.Password,
            }),
        };
        request.Headers.Add("X-Tenant", Tenant);
        return request;
    }

    private static string CookieFrom(HttpResponseMessage response, string name)
    {
        var header = response.Headers.GetValues("Set-Cookie")
            .Single(v => v.StartsWith($"{name}=", StringComparison.Ordinal));

        return Uri.UnescapeDataString(header.Split(';')[0][(name.Length + 1)..]);
    }
}
