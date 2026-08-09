// AccountRecoveryTests — the way back into an account, and everything it must
// refuse to tell you on the way.
//
// Use:  runs with the normal test suite; needs a reachable SQL engine.
// Edit: this is the softest surface in the application — unauthenticated, names
//       an account by email, and succeeds by setting a password on somebody's
//       login. The tests are weighted accordingly: most of them assert on what
//       the endpoint REFUSES to reveal rather than on the happy path.
//
//       The two that matter most are the enumeration test (every failure is one
//       indistinguishable error) and the crossover test (an enrolment code is not
//       a reset code). Both fail loudly if the checks are relaxed.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class AccountRecoveryTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string Staff = "/api/v1/staff";
    private const string Recover = "/api/v1/auth/recover";
    private const string Enrol = "/api/v1/auth/enrol";

    private const string Manager = DevelopmentSeeder.DevUsers.OrganizationWideEmail;
    private const string Sales = DevelopmentSeeder.DevUsers.SalespersonEmail;

    private const string GoodPassword = "A-Long-Enough-Pass1!";

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task The_offered_methods_are_a_property_of_the_installation_not_of_an_account()
    {
        using var response = await GetAnonymousAsync(Recover);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var offered = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Both built-in methods need nothing external. Email and text message
        // report false rather than being offered and then failing (ADR-018).
        offered.GetProperty("authenticator").GetBoolean().Should().BeTrue();
        offered.GetProperty("issuedCode").GetBoolean().Should().BeTrue();
        offered.GetProperty("email").GetBoolean().Should().BeFalse();
        offered.GetProperty("textMessage").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Asking_what_is_offered_never_requires_or_accepts_an_email()
    {
        // If this route ever grew a per-account answer it would become a way to
        // ask "does this person work here, and do they have an authenticator" —
        // which is a map of who is easiest to attack.
        using var withEmail = await GetAnonymousAsync($"{Recover}?email={Manager}");
        using var without = await GetAnonymousAsync(Recover);

        withEmail.StatusCode.Should().Be(HttpStatusCode.OK);
        (await withEmail.Content.ReadAsStringAsync())
            .Should().Be(await without.Content.ReadAsStringAsync(),
                because: "the answer must not vary with an email address");
    }

    [Fact]
    public async Task A_manager_issued_code_sets_a_new_password_and_it_works()
    {
        var (userId, email) = await AddEnrolledPersonAsync();

        var code = await IssueRecoveryCodeAsync(userId);

        using var reset = await PostAnonymousAsync($"{Recover}/code", new
        {
            email,
            code,
            password = GoodPassword,
        });

        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The proof of a reset is signing in with it, not the status code.
        (await SignInWorksAsync(email, GoodPassword)).Should().BeTrue();
    }

    [Fact]
    public async Task A_reset_code_works_once()
    {
        var (userId, email) = await AddEnrolledPersonAsync();
        var code = await IssueRecoveryCodeAsync(userId);

        using var first = await PostAnonymousAsync($"{Recover}/code", new { email, code, password = GoodPassword });
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var again = await PostAnonymousAsync($"{Recover}/code", new
        {
            email,
            code,
            password = "Another-Password-9!",
        });

        // A code that could be replayed to set a password is a password.
        again.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await again.Content.ReadAsStringAsync()).Should().Contain("recovery.refused");
    }

    [Fact]
    public async Task An_enrolment_code_is_not_a_reset_code_and_a_reset_code_is_not_an_enrolment_code()
    {
        // What this actually proves is the OUTCOME — neither code opens the other
        // door — and not the mechanism. Rehearsed 2026-08-09: removing the purpose
        // filter from both paths leaves this passing, because the opposite
        // preconditions on PasswordHash already separate them. The outcome is
        // still the thing worth guarding: if either precondition is ever relaxed,
        // this test is what notices.
        var starter = await AddStarterAsync();
        var starterCode = await IssueEnrolmentCodeAsync(starter.UserId);

        using var asReset = await PostAnonymousAsync($"{Recover}/code", new
        {
            email = starter.Email,
            code = starterCode,
            password = GoodPassword,
        });

        asReset.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var (enrolledId, enrolledEmail) = await AddEnrolledPersonAsync();
        var resetCode = await IssueRecoveryCodeAsync(enrolledId);

        using var asEnrolment = await PostAnonymousAsync(Enrol, new
        {
            email = enrolledEmail,
            code = resetCode,
            password = GoodPassword,
        });

        asEnrolment.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_unknown_address_and_a_wrong_code_are_indistinguishable()
    {
        var (userId, email) = await AddEnrolledPersonAsync();
        await IssueRecoveryCodeAsync(userId);

        using var unknown = await PostAnonymousAsync($"{Recover}/code", new
        {
            email = $"nobody{Guid.NewGuid():N}@dev.local",
            code = "ABCD-EFGH-JKLM",
            password = GoodPassword,
        });

        using var wrongCode = await PostAnonymousAsync($"{Recover}/code", new
        {
            email,
            code = "ABCD-EFGH-JKLM",
            password = GoodPassword,
        });

        // Same status AND same body. A caller who can tell these apart can map a
        // dealership's staff list from outside.
        unknown.StatusCode.Should().Be(wrongCode.StatusCode);
        (await unknown.Content.ReadAsStringAsync())
            .Should().Be(await wrongCode.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task An_account_that_never_had_a_password_cannot_be_recovered()
    {
        // Nothing to recover: that person needs a starter code. Answered with the
        // same refusal as everything else, so it does not confirm the address.
        var starter = await AddStarterAsync();

        using var refused = await PostAnonymousAsync($"{Recover}/code", new
        {
            email = starter.Email,
            code = "ABCD-EFGH-JKLM",
            password = GoodPassword,
        });

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("recovery.refused");
    }

    [Fact]
    public async Task A_short_password_is_refused_before_anything_is_looked_up()
    {
        // The one refusal that differs, and it has to: a caller must be told
        // their password is too short or they cannot proceed. It reveals nothing,
        // because the answer is the same whether or not the account exists.
        using var refused = await PostAnonymousAsync($"{Recover}/code", new
        {
            email = $"nobody{Guid.NewGuid():N}@dev.local",
            code = "ABCD-EFGH-JKLM",
            password = "short",
        });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("recovery.password_too_short");
    }

    [Fact]
    public async Task Issuing_a_reset_needs_its_own_permission_not_just_staff_management()
    {
        var (userId, _) = await AddEnrolledPersonAsync();

        // Sales holds neither, which is the cheap half of the assertion. The
        // valuable half is that the permission EXISTS separately at all: handing
        // over the ability to sign in as an existing colleague is not the same
        // act as fixing a rota, and Permissions.All carries both names.
        using var refused = await PostAsync($"{Staff}/{userId}/recovery", Sales, new { });

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Issuing_a_reset_is_visible_on_the_staff_record_not_only_in_the_audit_trail()
    {
        var (userId, _) = await AddEnrolledPersonAsync();

        var before = await GetStaffAsync(userId);
        before.GetProperty("recoveryIssuedAt").ValueKind.Should().Be(JsonValueKind.Null);

        await IssueRecoveryCodeAsync(userId);

        // A dealership must be able to SEE that somebody handed out access,
        // without going looking for it (ADR-018).
        var after = await GetStaffAsync(userId);
        after.GetProperty("recoveryIssuedAt").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Issuing_a_new_reset_code_kills_the_one_before_it()
    {
        var (userId, email) = await AddEnrolledPersonAsync();

        var first = await IssueRecoveryCodeAsync(userId);
        var second = await IssueRecoveryCodeAsync(userId);

        first.Should().NotBe(second);

        using var withOld = await PostAnonymousAsync($"{Recover}/code", new
        {
            email,
            code = first,
            password = GoodPassword,
        });

        // Two live codes double the guessing surface for no benefit; a mislaid
        // one is reissued, not kept as a spare.
        withOld.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var withNew = await PostAnonymousAsync($"{Recover}/code", new
        {
            email,
            code = second,
            password = GoodPassword,
        });

        withNew.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Recovering_an_account_ends_every_session_it_had()
    {
        var (userId, email) = await AddEnrolledPersonAsync();

        // Sign in, so there is a live session to lose.
        var session = await SignInAsync(email, GoodPassword);
        session.Should().NotBeNull();

        (await SessionStillWorksAsync(session!)).Should().BeTrue();

        var code = await IssueRecoveryCodeAsync(userId);
        using var reset = await PostAnonymousAsync($"{Recover}/code", new
        {
            email,
            code,
            password = "Yet-Another-Pass2!",
        });

        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Somebody recovering an account may be recovering it FROM someone. A
        // reset that left the other party signed in would be cosmetic.
        (await SessionStillWorksAsync(session!)).Should().BeFalse();
    }

    private async Task<JsonElement> GetStaffAsync(string userId)
    {
        using var response = await SendAsync(HttpMethod.Get, $"{Staff}/{userId}", Manager);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>A colleague who exists and has never set a password.</summary>
    private async Task<(string UserId, string Email)> AddStarterAsync()
    {
        var email = $"starter{Guid.NewGuid():N}@dev.local";

        using var response = await PostAsync(Staff, Manager, new
        {
            email,
            displayName = "Pat Starter",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        return (id, email);
    }

    /// <summary>A colleague who has redeemed a code and therefore has a password.</summary>
    private async Task<(string UserId, string Email)> AddEnrolledPersonAsync()
    {
        var starter = await AddStarterAsync();
        var code = await IssueEnrolmentCodeAsync(starter.UserId);

        using var redeemed = await PostAnonymousAsync(Enrol, new
        {
            email = starter.Email,
            code,
            password = GoodPassword,
        });

        redeemed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        return starter;
    }

    private async Task<string> IssueEnrolmentCodeAsync(string userId)
    {
        using var response = await PostAsync($"{Staff}/{userId}/enrolment", Manager, new { });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;
    }

    private async Task<string> IssueRecoveryCodeAsync(string userId)
    {
        using var response = await PostAsync($"{Staff}/{userId}/recovery", Manager, new { });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;
    }

    private async Task<bool> SignInWorksAsync(string email, string password) =>
        await SignInAsync(email, password) is not null;

    private async Task<string?> SignInAsync(string email, string password)
    {
        using var client = _fixture.CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri("/api/v1/auth/login", UriKind.Relative))
        {
            Content = JsonContent.Create(new { email, password }),
        };
        request.Headers.Add("X-Tenant", Tenant);

        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies
                .Select(c => c.Split(';')[0])
                .FirstOrDefault(c => c.StartsWith("dfoss_session=", StringComparison.Ordinal))
                ?["dfoss_session=".Length..]
            : null;
    }

    private async Task<bool> SessionStillWorksAsync(string sessionToken)
    {
        using var client = _fixture.CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri("/api/v1/auth/me", UriKind.Relative));
        request.Headers.Add("X-Tenant", Tenant);
        request.Headers.Add("Cookie", $"dfoss_session={sessionToken}");

        using var response = await client.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// No session and no anti-forgery token, but still a tenant: the caller has
    /// to say which dealership they belong to, because that is which database
    /// their account lives in. It is the one thing they can still supply.
    /// </summary>
    private async Task<HttpResponseMessage> GetAnonymousAsync(string path)
    {
        using var client = _fixture.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        request.Headers.Add("X-Tenant", Tenant);

        return await client.SendAsync(request);
    }

    /// <summary>No session and no anti-forgery token — the recovery caller has neither.</summary>
    private async Task<HttpResponseMessage> PostAnonymousAsync(string path, object body)
    {
        using var client = _fixture.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative))
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("X-Tenant", Tenant);

        return await client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> PostAsync(string path, string email, object body)
    {
        using var client = _fixture.CreateClient();
        var session = await _fixture.SignInAsync(email, Tenant);

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative))
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("X-Tenant", Tenant);
        request.Headers.Add("Cookie", $"dfoss_session={session.SessionToken}");
        request.Headers.Add("X-CSRF-Token", session.AntiForgeryToken);

        return await client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string email)
    {
        using var client = _fixture.CreateClient();
        var session = await _fixture.SignInAsync(email, Tenant);

        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Headers.Add("X-Tenant", Tenant);
        request.Headers.Add("Cookie", $"dfoss_session={session.SessionToken}");

        if (method != HttpMethod.Get)
        {
            request.Headers.Add("X-CSRF-Token", session.AntiForgeryToken);
        }

        return await client.SendAsync(request);
    }
}
