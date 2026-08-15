// PasskeyTests — the whole ceremony, over HTTP, against a real database.
//
// Use:  runs with the normal test suite.
// Edit: the unit tests prove the cryptography refuses what it must. These prove
//       the WIRING — that a real registration reaches the table, that a real
//       assertion produces a real session cookie, and that the refusals survive
//       the trip through the endpoint instead of being swallowed into a 500 or,
//       worse, a success.
//
//       The fake authenticator is linked from the unit project rather than
//       copied. Its relying party must match what the host is configured with,
//       which is "localhost" by default.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using DealerFOSS.App;
using DealerFOSS.Identity;
using DealerFOSS.UnitTests;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class PasskeyTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string Passkeys = "/api/v1/auth/passkeys";
    private const string Manager = DevelopmentSeeder.DevUsers.OrganizationWideEmail;

    /// <summary>Matches PasskeyOptions' default, which the host is running with.</summary>
    private const string RelyingParty = "localhost";
    private const string Origin = "http://localhost:5173";

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task A_passkey_can_be_registered_and_then_used_to_sign_in()
    {
        using var authenticator = new FakeAuthenticator(RelyingParty);
        var session = await _fixture.SignInAsync(Manager, Tenant);

        var registered = await RegisterAsync(authenticator, session, "Work laptop");
        registered.GetProperty("label").GetString().Should().Be("Work laptop");
        registered.GetProperty("lastUsedAt").ValueKind.Should().Be(JsonValueKind.Null);

        // The sign-in half takes no session: that is the point of it.
        using var signedIn = await SignInWithPasskeyAsync(authenticator);

        signedIn.StatusCode.Should().Be(HttpStatusCode.OK);
        signedIn.Headers.GetValues("Set-Cookie")
            .Should().Contain(c => c.StartsWith("dfoss_session=", StringComparison.Ordinal),
                because: "a passkey sign-in ends in the same session cookie as a password one");
    }

    /// <summary>
    /// The anti-phishing property, end to end. A passkey used on a lookalike
    /// site produces a response naming that site, and the endpoint must refuse
    /// it — not merely the verifier in isolation.
    /// </summary>
    [Fact]
    public async Task A_sign_in_from_a_lookalike_site_is_refused_by_the_endpoint()
    {
        using var authenticator = new FakeAuthenticator(RelyingParty);
        var session = await _fixture.SignInAsync(Manager, Tenant);
        await RegisterAsync(authenticator, session, "Phone");

        using var response = await SignInWithPasskeyAsync(
            authenticator, origin: "http://localhost.evil.test");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Contains("Set-Cookie").Should().BeFalse(
            because: "a refused ceremony must not hand out anything at all");
    }

    /// <summary>
    /// A challenge is single-use. Replaying a whole recorded response must fail
    /// the second time even though it succeeded the first.
    /// </summary>
    [Fact]
    public async Task A_recorded_sign_in_cannot_be_replayed()
    {
        using var authenticator = new FakeAuthenticator(RelyingParty);
        var session = await _fixture.SignInAsync(Manager, Tenant);
        await RegisterAsync(authenticator, session, "Phone");

        var challenge = await BeginSignInAsync();
        authenticator.Counter = 10;
        var (clientData, authData, signature) = authenticator.Sign(
            Base64Url.Decode(challenge.GetProperty("challenge").GetString()!), Origin);

        var body = new
        {
            challengeId = challenge.GetProperty("challengeId").GetGuid(),
            credentialId = Base64Url.Encode(authenticator.CredentialId),
            clientDataJson = Base64Url.Encode(clientData),
            authenticatorData = Base64Url.Encode(authData),
            signature = Base64Url.Encode(signature),
        };

        using var first = await PostAnonymousAsync($"{Passkeys}/sign-in/finish", body);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        using var again = await PostAnonymousAsync($"{Passkeys}/sign-in/finish", body);
        again.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "the challenge was spent the first time");
    }

    /// <summary>
    /// Asking for a sign-in challenge must not require, or reveal, an account.
    /// If it did, the endpoint would be a way to enumerate who has a passkey.
    /// </summary>
    [Fact]
    public async Task Asking_for_a_sign_in_challenge_needs_no_account_and_names_none()
    {
        var challenge = await BeginSignInAsync();

        challenge.GetProperty("challenge").GetString().Should().NotBeNullOrEmpty();
        challenge.TryGetProperty("userName", out _).Should().BeFalse();
        challenge.TryGetProperty("userHandle", out _).Should().BeFalse();
    }

    [Fact]
    public async Task A_passkey_is_listed_for_its_owner_and_can_be_forgotten()
    {
        using var authenticator = new FakeAuthenticator(RelyingParty);
        var session = await _fixture.SignInAsync(Manager, Tenant);
        var registered = await RegisterAsync(authenticator, session, "Spare key");
        var id = registered.GetProperty("id").GetString()!;

        var mine = await ListAsync(session);
        mine.Should().Contain(p => p.GetProperty("id").GetString() == id);

        using var forgotten = await SendAsync(
            HttpMethod.Delete, $"{Passkeys}/{id}", session);
        forgotten.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ListAsync(session)).Should().NotContain(p => p.GetProperty("id").GetString() == id);

        // And it stops working, which is the part that matters.
        using var refused = await SignInWithPasskeyAsync(authenticator);
        refused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- helpers -------------------------------------------------------------

    private async Task<JsonElement> RegisterAsync(
        FakeAuthenticator authenticator, SignedInSession session, string label)
    {
        using var begun = await SendAsync(HttpMethod.Post, $"{Passkeys}/register/begin", session);
        begun.StatusCode.Should().Be(HttpStatusCode.OK);

        var challenge = await begun.Content.ReadFromJsonAsync<JsonElement>();
        var (clientData, attestation) = authenticator.Register(
            Base64Url.Decode(challenge.GetProperty("challenge").GetString()!), Origin);

        using var finished = await SendAsync(
            HttpMethod.Post, $"{Passkeys}/register/finish", session, new
            {
                challengeId = challenge.GetProperty("challengeId").GetGuid(),
                clientDataJson = Base64Url.Encode(clientData),
                attestationObject = Base64Url.Encode(attestation),
                label,
            });

        finished.StatusCode.Should().Be(HttpStatusCode.OK);
        return await finished.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> BeginSignInAsync()
    {
        using var response = await PostAnonymousAsync($"{Passkeys}/sign-in/begin", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<HttpResponseMessage> SignInWithPasskeyAsync(
        FakeAuthenticator authenticator, string? origin = null)
    {
        var challenge = await BeginSignInAsync();

        // Advanced every time, so the counter rule never refuses a legitimate
        // second sign-in in a test that is not about counters.
        authenticator.Counter++;

        var (clientData, authData, signature) = authenticator.Sign(
            Base64Url.Decode(challenge.GetProperty("challenge").GetString()!), origin ?? Origin);

        return await PostAnonymousAsync($"{Passkeys}/sign-in/finish", new
        {
            challengeId = challenge.GetProperty("challengeId").GetGuid(),
            credentialId = Base64Url.Encode(authenticator.CredentialId),
            clientDataJson = Base64Url.Encode(clientData),
            authenticatorData = Base64Url.Encode(authData),
            signature = Base64Url.Encode(signature),
        });
    }

    private async Task<List<JsonElement>> ListAsync(SignedInSession session)
    {
        using var response = await SendAsync(HttpMethod.Get, Passkeys, session);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return [.. (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()];
    }

    private async Task<HttpResponseMessage> PostAnonymousAsync(string path, object? body)
    {
        using var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative));
        request.Headers.Add("X-Tenant", Tenant);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }

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
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }
}
