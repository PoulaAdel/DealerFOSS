// SecondFactorTests — proves the password alone stops being enough once a second
// factor is on, and that the way back in without a phone works exactly once.
//
// Use:  runs with the normal test suite; needs a reachable SQL engine.
// Edit: these run against mfa@dev.local, an account reserved for this file so
//       switching MFA on cannot stop every other test signing in. Each test puts
//       it back afterwards.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using OpenDealer360.App;
using OpenDealer360.Identity;

namespace OpenDealer360.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class SecondFactorTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string Email = DevelopmentSeeder.DevUsers.SecondFactorEmail;
    private const string Password = DevelopmentSeeder.DevUsers.Password;

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task An_account_without_a_second_factor_signs_in_on_the_password_alone()
    {
        // The regression that matters: turning this feature on must not change
        // how everybody else signs in.
        using var response = await LoginAsync(DevelopmentSeeder.DevUsers.OrganizationWideEmail);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("secondFactorRequired", out _).Should().BeFalse();
        SessionCookie(response).Should().NotBeNull();
    }

    [Fact]
    public async Task Enrolling_then_signing_in_demands_a_code_and_accepts_the_right_one()
    {
        var (secret, _) = await EnrolAsync();

        try
        {
            using var login = await LoginAsync(Email);
            login.StatusCode.Should().Be(HttpStatusCode.OK);

            var challenge = await login.Content.ReadFromJsonAsync<JsonElement>();
            challenge.GetProperty("secondFactorRequired").GetBoolean().Should().BeTrue();

            // Critically: the password alone did NOT produce a session cookie.
            SessionCookie(login).Should().BeNull(
                because: "a password is not a session once a second factor is on");

            var token = challenge.GetProperty("challengeToken").GetString()!;

            using var wrong = await PostAsync("/api/v1/auth/login/second-factor",
                new { challengeToken = token, code = "000000" });
            wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            using var right = await PostAsync("/api/v1/auth/login/second-factor",
                new { challengeToken = token, code = Totp.Generate(secret, DateTimeOffset.UtcNow) });

            // The body carries the stable error code, which is what tells you
            // which check refused rather than just that something did.
            right.StatusCode.Should().Be(HttpStatusCode.OK,
                because: await right.Content.ReadAsStringAsync());
            SessionCookie(right).Should().NotBeNull(because: "the code completed the sign-in");
        }
        finally
        {
            await DisableAsync(secret);
        }
    }

    [Fact]
    public async Task A_recovery_code_works_once_and_then_never_again()
    {
        var (secret, recoveryCodes) = await EnrolAsync();

        try
        {
            recoveryCodes.Should().HaveCount(10);
            var code = recoveryCodes[0];

            var firstToken = await ChallengeTokenAsync();
            using var first = await PostAsync("/api/v1/auth/login/second-factor",
                new { challengeToken = firstToken, code });
            first.StatusCode.Should().Be(HttpStatusCode.OK);

            // The same code again, on a fresh challenge.
            var secondToken = await ChallengeTokenAsync();
            using var second = await PostAsync("/api/v1/auth/login/second-factor",
                new { challengeToken = secondToken, code });

            second.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                because: "a recovery code that can be replayed is a password with extra steps");
        }
        finally
        {
            await DisableAsync(secret);
        }
    }

    [Fact]
    public async Task A_challenge_cannot_be_reused_after_it_has_worked()
    {
        var (secret, _) = await EnrolAsync();

        try
        {
            var token = await ChallengeTokenAsync();

            using var first = await PostAsync("/api/v1/auth/login/second-factor",
                new { challengeToken = token, code = Totp.Generate(secret, DateTimeOffset.UtcNow) });
            first.StatusCode.Should().Be(HttpStatusCode.OK);

            using var replay = await PostAsync("/api/v1/auth/login/second-factor",
                new { challengeToken = token, code = Totp.Generate(secret, DateTimeOffset.UtcNow) });

            replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        finally
        {
            await DisableAsync(secret);
        }
    }

    [Fact]
    public async Task Guessing_is_cut_off_after_a_handful_of_wrong_codes()
    {
        var (secret, _) = await EnrolAsync();

        try
        {
            var token = await ChallengeTokenAsync();

            for (var i = 0; i < 5; i++)
            {
                using var wrong = await PostAsync("/api/v1/auth/login/second-factor",
                    new { challengeToken = token, code = "000000" });
                wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            }

            // Now even the correct code is refused on this challenge.
            using var correct = await PostAsync("/api/v1/auth/login/second-factor",
                new { challengeToken = token, code = Totp.Generate(secret, DateTimeOffset.UtcNow) });

            correct.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                because: "six digits is a million guesses without a cap");
        }
        finally
        {
            await DisableAsync(secret);
        }
    }

    [Fact]
    public async Task Turning_it_off_needs_a_current_code()
    {
        var (secret, _) = await EnrolAsync();

        try
        {
            var session = await SignedInSessionAsync(secret);

            using var withoutCode = await PostAsync("/api/v1/auth/mfa/disable",
                new { code = "000000" }, session);

            withoutCode.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                because: "a borrowed session must not be able to strip the second factor");
        }
        finally
        {
            await DisableAsync(secret);
        }
    }

    // --- helpers -----------------------------------------------------------

    /// <summary>
    /// Wipes the second factor off the reserved account, in the database.
    ///
    /// Turning MFA off through the API needs a working code, so a run that fails
    /// between enrolling and cleaning up would leave the account permanently
    /// stuck — the secret only ever exists in that test's memory. Resetting here
    /// makes every test independent of whatever the last one managed to do.
    /// </summary>
    private static async Task ResetSecondFactorAsync()
    {
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(
            new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(HostFixture.ConnectionString)
            {
                InitialCatalog = $"OpenDealer360_Tenant_{Tenant}",
            }.ConnectionString);

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
        command.Parameters.AddWithValue("@user", DevelopmentSeeder.DevUsers.SecondFactor);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Enrols the reserved account and returns its secret and recovery codes.</summary>
    private async Task<(string Secret, IReadOnlyList<string> RecoveryCodes)> EnrolAsync()
    {
        await ResetSecondFactorAsync();
        var session = await PasswordOnlySessionAsync();

        using var begun = await PostAsync("/api/v1/auth/mfa/enrol", new { }, session);
        begun.StatusCode.Should().Be(HttpStatusCode.OK,
            "enrolment should be available — if this is a conflict, a previous run left MFA on");

        var secret = (await begun.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("secret").GetString()!;

        using var confirmed = await PostAsync("/api/v1/auth/mfa/confirm",
            new { code = Totp.Generate(secret, DateTimeOffset.UtcNow) }, session);
        confirmed.StatusCode.Should().Be(HttpStatusCode.OK);

        var codes = (await confirmed.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("recoveryCodes").EnumerateArray()
            .Select(c => c.GetString()!)
            .ToList();

        return (secret, codes);
    }

    /// <summary>
    /// Puts the account back. Goes through the API so the disable path is
    /// exercised, then resets directly in case it did not take — a test must not
    /// be able to poison the next one.
    /// </summary>
    private async Task DisableAsync(string secret)
    {
        try
        {
            var session = await SignedInSessionAsync(secret);
            using var _ = await PostAsync("/api/v1/auth/mfa/disable",
                new { code = Totp.Generate(secret, DateTimeOffset.UtcNow) }, session);
        }
        finally
        {
            await ResetSecondFactorAsync();
        }
    }

    private async Task<string> PasswordOnlySessionAsync()
    {
        using var login = await LoginAsync(Email);
        return SessionCookie(login)
            ?? throw new InvalidOperationException("Expected a session; the account already has MFA on.");
    }

    private async Task<string> SignedInSessionAsync(string secret)
    {
        using var login = await LoginAsync(Email);
        var direct = SessionCookie(login);
        if (direct is not null)
        {
            return direct;
        }

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("challengeToken").GetString()!;

        using var completed = await PostAsync("/api/v1/auth/login/second-factor",
            new { challengeToken = token, code = Totp.Generate(secret, DateTimeOffset.UtcNow) });

        return SessionCookie(completed)!;
    }

    private async Task<string> ChallengeTokenAsync()
    {
        using var login = await LoginAsync(Email);
        return (await login.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("challengeToken").GetString()!;
    }

    private static string? SessionCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        var raw = cookies.FirstOrDefault(c => c.StartsWith("odms_session=", StringComparison.Ordinal));
        if (raw is null)
        {
            return null;
        }

        var value = raw.Split(';')[0]["odms_session=".Length..];
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private Task<HttpResponseMessage> LoginAsync(string email) =>
        PostAsync("/api/v1/auth/login", new { email, password = Password });

    private async Task<HttpResponseMessage> PostAsync(string path, object body, string? sessionToken = null)
    {
        using var client = _fixture.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative))
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("X-Tenant", Tenant);

        if (sessionToken is not null)
        {
            request.Headers.Add("Cookie", $"odms_session={sessionToken}");
        }

        return await client.SendAsync(request);
    }
}
