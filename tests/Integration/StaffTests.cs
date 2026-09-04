// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   StaffTests — proves a dealership can see and manage its own people, and that
//   opening Identity's fourth public surface did not open anything else.
//
// Usage:
//   Runs with the normal test suite; needs a reachable SQL engine.
//
// Coding Instructions:
//   The test that matters most is
//   No_credential_material_ever_appears_in_a_staff_response. It reads the raw
//   JSON rather than a typed model on purpose — a typed model can only fail
//   on fields somebody remembered to declare, and the risk here is a field
//   nobody remembered. Keep it looking at the wire.
//
//   Several tests grant and remove roles. Every one of them cleans up in a
//   finally block: an assignment left behind changes what a later test's user
//   can reach, which surfaces as an unrelated failure somewhere else.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using DealerFOSS.App;
using DealerFOSS.Identity;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class StaffTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string Staff = "/api/v1/staff";

    private const string Manager = DevelopmentSeeder.DevUsers.OrganizationWideEmail;
    private const string Advisor = DevelopmentSeeder.DevUsers.FirstRooftopOnlyEmail;

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task A_manager_can_see_who_works_here()
    {
        using var response = await SendAsync(HttpMethod.Get, Staff, Manager);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var people = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        people.Should().NotBeEmpty();
        people.Select(p => p.GetProperty("email").GetString())
            .Should().Contain(DevelopmentSeeder.DevUsers.SalespersonEmail);
    }

    [Fact]
    public async Task No_credential_material_ever_appears_in_a_staff_response()
    {
        // The reason this surface was allowed out of Identity at all. Read the
        // wire, not a model: a typed check can only catch fields somebody thought
        // to declare, and what would hurt here is one nobody thought about.
        using var list = await SendAsync(HttpMethod.Get, Staff, Manager);
        var body = await list.Content.ReadAsStringAsync();

        foreach (var forbidden in new[]
                 {
                     "passwordHash", "password", "mfaSecret", "mfaSecretProtected",
                     "recoveryCode", "tokenHash", "sessionToken", "codeHash",
                 })
        {
            body.Should().NotContainEquivalentOf(forbidden,
                because: $"'{forbidden}' is credential material and must never cross this boundary");
        }

        // And the useful facts are present, so this is not passing by returning
        // nothing at all.
        var first = (await list.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().First();
        first.TryGetProperty("hasSecondFactor", out _).Should().BeTrue();
        first.TryGetProperty("isActive", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Somebody_without_the_permission_is_refused_the_staff_list()
    {
        using var response = await SendAsync(HttpMethod.Get, Staff, Advisor);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_role_says_what_holding_it_grants()
    {
        using var response = await SendAsync(HttpMethod.Get, $"{Staff}/roles", Manager);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var roles = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        var salesperson = roles.Single(r => r.GetProperty("name").GetString() == IdentitySeeder.SalespersonRole);

        salesperson.GetProperty("permissions").EnumerateArray()
            .Select(p => p.GetString())
            .Should().Contain(Permissions.DealsWrite,
                because: "somebody handing this role over should see what they are handing over");
    }

    [Fact]
    public async Task A_starter_is_created_unable_to_sign_in()
    {
        var email = $"starter-{Guid.NewGuid():N}@dev.local";

        using var created = await SendAsync(
            HttpMethod.Post, Staff, Manager, new { email, displayName = "New Starter" });

        created.StatusCode.Should().Be(HttpStatusCode.Created, because: await created.Content.ReadAsStringAsync());

        var person = await created.Content.ReadFromJsonAsync<JsonElement>();
        person.GetProperty("canSignIn").GetBoolean().Should().BeFalse();
        person.GetProperty("awaitingEnrolment").GetBoolean().Should().BeTrue(
            because: "a starter with no credential is a different state from a leaver who has been stopped");

        // And the password path really is closed, not merely reported as closed.
        using var attempt = await LogInAsync(email, "AnythingAtAll1!");
        attempt.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_same_email_cannot_be_used_twice()
    {
        using var response = await SendAsync(
            HttpMethod.Post, Staff, Manager,
            new { email = DevelopmentSeeder.DevUsers.SalespersonEmail, displayName = "Impostor" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_one_time_code_lets_a_starter_set_their_own_password_and_sign_in()
    {
        var (email, userId) = await AddStarterAsync();
        var code = await IssueCodeAsync(userId);

        const string chosen = "TheirOwnPassword1!";

        using (var redeemed = await EnrolAsync(email, code, chosen))
        {
            redeemed.StatusCode.Should().Be(HttpStatusCode.NoContent,
                because: await redeemed.Content.ReadAsStringAsync());
        }

        using var signedIn = await LogInAsync(email, chosen);
        signedIn.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "the whole point of the code is to end with a working account");
    }

    [Fact]
    public async Task A_code_cannot_be_redeemed_twice()
    {
        // Two independent guards stop this, and it is worth knowing which one
        // fires: the account now HAS a password, so the "already enrolled" check
        // refuses before the consumed-code check is ever reached.
        //
        // That makes this test unable to prove consumption on its own — removing
        // `enrolment.Consume(...)` leaves it green, which was confirmed by
        // rehearsal rather than assumed. Consumption still matters as the second
        // guard: an unconsumed code stays usable in the table, so the day a
        // password reset exists, an old enrolment code would spring back to life.
        // `Issuing_a_new_code_kills_the_previous_one` is what actually exercises
        // the row's lifecycle.
        var (email, userId) = await AddStarterAsync();
        var code = await IssueCodeAsync(userId);

        using (var first = await EnrolAsync(email, code, "FirstPassword1!"))
        {
            first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        using var second = await EnrolAsync(email, code, "SecondPassword1!");
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // And the first password is still the one that works — a second
        // redemption must not quietly overwrite it.
        using var signedIn = await LogInAsync(email, "FirstPassword1!");
        signedIn.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Issuing_a_new_code_stops_the_previous_one_working()
    {
        // Behaviour, not mechanism. Redemption looks at the newest live code
        // only, so this passes on that alone — rehearsal confirmed it stays green
        // with the supersede loop removed. The assertion is still worth having:
        // "the code I read out five minutes ago no longer works" is what a
        // manager relies on, whichever guard delivers it.
        var (email, userId) = await AddStarterAsync();
        var first = await IssueCodeAsync(userId);
        var second = await IssueCodeAsync(userId);

        using (var stale = await EnrolAsync(email, first, "PasswordOne1!"))
        {
            stale.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                because: "two live codes double the guessing surface for no benefit");
        }

        using var fresh = await EnrolAsync(email, second, "PasswordTwo1!");
        fresh.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_wrong_code_and_an_unknown_email_fail_identically()
    {
        var (email, _) = await AddStarterAsync();

        using var wrongCode = await EnrolAsync(email, "AAAA-BBBB-CCCC", "SomePassword1!");
        using var unknownEmail = await EnrolAsync(
            $"nobody-{Guid.NewGuid():N}@dev.local", "AAAA-BBBB-CCCC", "SomePassword1!");

        wrongCode.StatusCode.Should().Be(unknownEmail.StatusCode);
        (await wrongCode.Content.ReadAsStringAsync())
            .Should().Be(await unknownEmail.Content.ReadAsStringAsync(),
                because: "a different answer tells somebody holding a code whose account it opens");
    }

    [Fact]
    public async Task An_account_that_already_has_a_password_cannot_be_handed_a_code()
    {
        // This would be a password reset without any of the safeguards a password
        // reset needs — most obviously, proof that the person asking is the owner.
        using var response = await SendAsync(
            HttpMethod.Post, $"{Staff}/{DevelopmentSeeder.DevUsers.Salesperson}/enrolment", Manager);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_short_password_is_refused()
    {
        var (email, userId) = await AddStarterAsync();
        var code = await IssueCodeAsync(userId);

        using var response = await EnrolAsync(email, code, "short");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_rooftop_scoped_manager_cannot_grant_organization_wide_access()
    {
        // The rule the endpoint exists to hold. Without it, whoever manages one
        // lot can hand themselves the whole group.
        var managerRoleId = await RoleIdAsync(IdentitySeeder.ManagerRole);
        var rooftop = await RooftopIdAsync("NAG-01");
        var subject = DevelopmentSeeder.DevUsers.Unassigned;

        var assignmentId = await GrantAsync(subject, managerRoleId, rooftop);

        try
        {
            // They may manage staff, but only at NAG-01.
            using var organizationWide = await SendAsync(
                HttpMethod.Post,
                $"{Staff}/{DevelopmentSeeder.DevUsers.Salesperson}/assignments",
                DevelopmentSeeder.DevUsers.UnassignedEmail,
                new { roleId = managerRoleId, rooftopId = (Guid?)null });

            organizationWide.StatusCode.Should().Be(HttpStatusCode.Forbidden,
                because: "holding a permission at one location must not let somebody grant it everywhere");
        }
        finally
        {
            await RevokeAsync(subject, assignmentId);
        }
    }

    [Fact]
    public async Task A_rooftop_scoped_manager_sees_only_people_whose_access_touches_their_lot()
    {
        var managerRoleId = await RoleIdAsync(IdentitySeeder.ManagerRole);
        var rooftop = await RooftopIdAsync("NAG-01");
        var subject = DevelopmentSeeder.DevUsers.Unassigned;

        var assignmentId = await GrantAsync(subject, managerRoleId, rooftop);

        try
        {
            using var response = await SendAsync(
                HttpMethod.Get, Staff, DevelopmentSeeder.DevUsers.UnassignedEmail);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var emails = (await response.Content.ReadFromJsonAsync<JsonElement>())
                .EnumerateArray()
                .Select(p => p.GetProperty("email").GetString())
                .ToList();

            emails.Should().Contain(Manager,
                because: "an organization-wide colleague really can reach this lot, so hiding them "
                    + "would misrepresent who has access");

            // Somebody with no assignment anywhere touches no lot, so they are not
            // on a rooftop-scoped listing. (The subject themselves is, via the
            // grant this test made.)
            emails.Should().NotContain(DevelopmentSeeder.DevUsers.SecondFactorEmail);
        }
        finally
        {
            await RevokeAsync(subject, assignmentId);
        }
    }

    [Fact]
    public async Task Stopping_a_leaver_ends_their_session_on_the_very_next_request()
    {
        // A leaver who keeps working until their session happens to expire is not
        // a leaver.
        var (email, userId) = await AddStarterAsync();
        var code = await IssueCodeAsync(userId);

        using (var redeemed = await EnrolAsync(email, code, "LeaverPassword1!"))
        {
            redeemed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        // Not the fixture helper: that signs in with the shared development
        // password, and this account has one the starter chose.
        var session = await SignInWithAsync(email, "LeaverPassword1!");

        using (var before = await SendAsync(HttpMethod.Get, "/api/v1/auth/me", session))
        {
            before.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using (var stopped = await SendAsync(
            HttpMethod.Post, $"{Staff}/{userId}/active", Manager, new { active = false }))
        {
            stopped.StatusCode.Should().Be(HttpStatusCode.NoContent,
                because: await stopped.Content.ReadAsStringAsync());
        }

        using var after = await SendAsync(HttpMethod.Get, "/api/v1/auth/me", session);
        after.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Nobody_can_stop_their_own_account()
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            $"{Staff}/{DevelopmentSeeder.DevUsers.OrganizationWide}/active",
            Manager,
            new { active = false });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: "locking yourself out is never the intent, and getting back in needs somebody else");
    }

    [Fact]
    public async Task Granting_the_same_role_twice_is_not_an_error()
    {
        var salesRoleId = await RoleIdAsync(IdentitySeeder.SalespersonRole);
        var rooftop = await RooftopIdAsync("NAG-01");
        var subject = DevelopmentSeeder.DevUsers.Unassigned;

        var assignmentId = await GrantAsync(subject, salesRoleId, rooftop);

        try
        {
            using var again = await SendAsync(
                HttpMethod.Post, $"{Staff}/{subject}/assignments", Manager,
                new { roleId = salesRoleId, rooftopId = rooftop });

            again.StatusCode.Should().Be(HttpStatusCode.NoContent,
                because: "it is already true — that is not a conflict");
        }
        finally
        {
            await RevokeAsync(subject, assignmentId);
        }
    }

    // --- helpers -----------------------------------------------------------

    private async Task<(string Email, Guid UserId)> AddStarterAsync()
    {
        var email = $"starter-{Guid.NewGuid():N}@dev.local";

        using var response = await SendAsync(
            HttpMethod.Post, Staff, Manager, new { email, displayName = "New Starter" });

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            because: await response.Content.ReadAsStringAsync());

        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        return (email, id);
    }

    private async Task<string> IssueCodeAsync(Guid userId)
    {
        using var response = await SendAsync(HttpMethod.Post, $"{Staff}/{userId}/enrolment", Manager);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("code").GetString()!;
    }

    /// <summary>Anonymous by design — the caller has no session yet.</summary>
    private async Task<HttpResponseMessage> EnrolAsync(string email, string code, string password)
    {
        using var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri("/api/v1/auth/enrol", UriKind.Relative))
        {
            Content = JsonContent.Create(new { email, code, password }),
        };
        request.Headers.Add("X-Tenant", Tenant);

        return await client.SendAsync(request);
    }

    private async Task<SignedInSession> SignInWithAsync(string email, string password)
    {
        using var response = await LogInAsync(email, password);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());

        return new SignedInSession(
            AuthenticationTests.CookieFrom(response, "dfoss_session"),
            AuthenticationTests.CookieFrom(response, "dfoss_csrf"));
    }

    private async Task<HttpResponseMessage> LogInAsync(string email, string password)
    {
        using var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri("/api/v1/auth/login", UriKind.Relative))
        {
            Content = JsonContent.Create(new { email, password }),
        };
        request.Headers.Add("X-Tenant", Tenant);

        return await client.SendAsync(request);
    }

    private async Task<Guid> GrantAsync(Guid userId, Guid roleId, string rooftopId)
    {
        using var granted = await SendAsync(
            HttpMethod.Post, $"{Staff}/{userId}/assignments", Manager,
            new { roleId, rooftopId });

        granted.StatusCode.Should().Be(HttpStatusCode.NoContent,
            because: await granted.Content.ReadAsStringAsync());

        using var person = await SendAsync(HttpMethod.Get, $"{Staff}/{userId}", Manager);

        return (await person.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("assignments")
            .EnumerateArray()
            .Single(a => a.GetProperty("roleId").GetGuid() == roleId
                && a.GetProperty("rooftopId").ToString() == rooftopId)
            .GetProperty("id")
            .GetGuid();
    }

    private async Task RevokeAsync(Guid userId, Guid assignmentId)
    {
        using var response = await SendAsync(
            HttpMethod.Delete, $"{Staff}/{userId}/assignments/{assignmentId}", Manager);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            because: "an assignment left behind changes what a later test's user can reach");
    }

    private async Task<Guid> RoleIdAsync(string roleName)
    {
        using var response = await SendAsync(HttpMethod.Get, $"{Staff}/roles", Manager);

        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray()
            .Single(r => r.GetProperty("name").GetString() == roleName)
            .GetProperty("id")
            .GetGuid();
    }

    private async Task<string> RooftopIdAsync(string code)
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/v1/organization", Manager);
        var root = await response.Content.ReadFromJsonAsync<JsonElement>();

        return root.GetProperty("legalEntities")
            .EnumerateArray()
            .SelectMany(entity => entity.GetProperty("rooftops").EnumerateArray())
            .Single(rooftop => rooftop.GetProperty("code").GetString() == code)
            .GetProperty("id")
            .ToString();
    }

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
}
