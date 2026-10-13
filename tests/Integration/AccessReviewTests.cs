// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   AccessReviewTests — who holds what, who may read it, and what a signature is
//   worth.
//
//   The FTC Safeguards Rule obliges a dealership to review access periodically.
//   These tests are the evidence that it can: that the answer is complete, that
//   reading it is privileged, that signing it means something, and that an
//   operator never appears in a dealership's own review.
//
// Usage:
//   Runs with the normal test suite; needs a reachable SQL engine.
//
// Coding Instructions:
//   THE SIGNATURE TESTS ARE THE POINT. "A manager can read a table" is the easy
//   half. The half that makes the record worth filing is that a signature is
//   over a KNOWN picture: sign a stale checksum and it is refused by name, and
//   the stored checksum is the one that was displayed. Without those, an
//   attestation is a date with nothing behind it.
//
//   The administrator test must keep asserting on the WHOLE table rather than a
//   filtered view of it. It is checking that a separation which is structural —
//   different context, different schema — has not quietly stopped being so.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class AccessReviewTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string Review = "/api/v1/security/access-review";

    /// <summary>Organization-wide manager — the only seeded role holding the right.</summary>
    private const string Manager = DevelopmentSeeder.DevUsers.OrganizationWideEmail;

    /// <summary>
    /// Holds Staff.Read, so they can already see colleagues' roles — and must
    /// still be refused the review. That gap is the reason the right exists.
    /// </summary>
    private const string Salesperson = DevelopmentSeeder.DevUsers.SalespersonEmail;

    private readonly HostFixture _fixture = fixture;

    // --- who may read it -----------------------------------------------------

    [Fact]
    public async Task A_manager_can_read_who_holds_what()
    {
        var review = await ReadAsync(Manager);

        var rows = review.GetProperty("rows").EnumerateArray().ToList();
        rows.Should().NotBeEmpty();

        // The reviewer's direction: the question is "who can do X", so X has to
        // be answerable from the table without opening each person.
        rows.Should().Contain(r => r.GetProperty("permission").GetString() == "Deals.Approve");
        rows.Should().Contain(r => r.GetProperty("permission").GetString() == "Profitability.Read");

        // And the scope, not just the role name — a grant at one lot and a grant
        // everywhere are different access and a review that conflated them would
        // be worthless.
        rows.Should().Contain(r => r.GetProperty("isOrganizationWide").GetBoolean());
        rows.Should().Contain(r => r.GetProperty("rooftopId").ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public async Task Somebody_who_can_see_the_staff_list_still_cannot_review_access()
    {
        // The salesperson holds Staff.Read and can already list colleagues with
        // their roles. Reading who holds POWER is a different act and this is the
        // test that keeps the two apart.
        using var staff = await SendAsync(HttpMethod.Get, "/api/v1/staff", Salesperson);
        staff.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "the existing directory read is unchanged — this milestone takes nothing away");

        using var refused = await SendAsync(HttpMethod.Get, Review, Salesperson);
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_rooftop_manager_is_refused_rather_than_shown_their_own_lot()
    {
        // A review narrowed to one lot would omit everybody who can reach every
        // lot — exactly the people a reviewer most needs to see — so a partial
        // answer is worse than none.
        using var response = await SendAsync(
            HttpMethod.Get, Review, DevelopmentSeeder.DevUsers.FirstRooftopOnlyEmail);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- what it must never contain ------------------------------------------

    [Fact]
    public async Task No_operator_appears_in_a_dealerships_own_review()
    {
        // Structural rather than filtered: administrators live in ControlPlaneDb,
        // a different context and schema that IAccessReview never opens. This
        // asserts that the separation has not quietly stopped holding — the same
        // reason the control plane has its own tests rather than a comment.
        var review = await ReadAsync(Manager);

        var emails = review.GetProperty("rows").EnumerateArray()
            .Select(r => r.GetProperty("email").GetString())
            .ToList();

        emails.Should().NotContain(DevelopmentSeeder.DevAdministrator.Email);
        emails.Should().NotContain(DevelopmentSeeder.DevAdministrator.UnenrolledEmail);
        emails.Should().NotContain(e => e != null && e.EndsWith("@control.local", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_review_carries_no_secret()
    {
        using var response = await SendAsync(HttpMethod.Get, Review, Manager);
        var body = await response.Content.ReadAsStringAsync();

        foreach (var word in new[] { "passwordHash", "mfaSecret", "sessionToken", "recoveryCode" })
        {
            body.Should().NotContain(word, because: "a review says who may act, never how they prove it");
        }
    }

    // --- what a signature is worth -------------------------------------------

    [Fact]
    public async Task Signing_records_who_looked_and_at_what()
    {
        var review = await ReadAsync(Manager);
        var checksum = review.GetProperty("checksum").GetString();

        checksum.Should().NotBeNullOrWhiteSpace();

        using var signed = await SendAsync(
            HttpMethod.Post, $"{Review}/sign", Manager,
            new { checksum, note = "Quarterly review, two leavers removed." });

        signed.StatusCode.Should().Be(HttpStatusCode.OK, because: await signed.Content.ReadAsStringAsync());

        var signature = await signed.Content.ReadFromJsonAsync<JsonElement>();

        signature.GetProperty("checksum").GetString().Should().Be(checksum,
            because: "the signature is over the picture that was displayed, not over a later one");
        signature.GetProperty("note").GetString().Should().Be("Quarterly review, two leavers removed.");
        signature.GetProperty("signedByUserId").GetGuid()
            .Should().Be(DevelopmentSeeder.DevUsers.OrganizationWide);

        // And it comes back on the next read, with a due date, which is the
        // "periodic" half of the obligation.
        var after = await ReadAsync(Manager);
        after.GetProperty("lastSignature").ValueKind.Should().NotBe(JsonValueKind.Null);
        after.GetProperty("nextDueAt").ValueKind.Should().NotBe(JsonValueKind.Null);
        after.GetProperty("reviewEveryDays").GetInt32().Should().Be(90,
            because: "the default cadence is 90 days until a dealership changes it");
    }

    [Fact]
    public async Task Signing_a_picture_that_has_changed_is_refused_by_name()
    {
        // The test that makes an attestation worth keeping. A checksum from a
        // review that no longer describes reality must not be signable, or the
        // record says somebody approved access they never saw.
        using var response = await SendAsync(
            HttpMethod.Post, $"{Review}/sign", Manager,
            new { checksum = new string('0', 64), note = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("access_review.picture_changed",
            because: "the screen has a specific thing to do about this, so it needs its own code");
    }

    [Fact]
    public async Task Somebody_without_the_right_cannot_sign_either()
    {
        var review = await ReadAsync(Manager);

        using var response = await SendAsync(
            HttpMethod.Post, $"{Review}/sign", Salesperson,
            new { checksum = review.GetProperty("checksum").GetString(), note = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "reading and attesting are the same right — signing without reading would be worse");
    }

    // --- the file that gets kept ---------------------------------------------

    [Fact]
    public async Task The_review_comes_out_as_a_file_whose_checksum_matches_it()
    {
        using var response = await SendAsync(HttpMethod.Get, $"{Review}/export", Manager);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");

        var content = await response.Content.ReadAsStringAsync();

        content.Should().StartWith("\"email\",\"name\",\"active\",\"second_factor\",\"permission\"");
        content.Should().Contain("Deals.Approve");

        // The header checksum is the REVIEW's, so a filed CSV can be matched
        // against the signature that was recorded for it.
        var claimed = response.Headers.GetValues("X-Content-SHA256").Single();
        var fromApi = (await ReadAsync(Manager)).GetProperty("checksum").GetString();

        claimed.Should().Be(fromApi,
            because: "the file somebody files and the thing they signed have to be the same review");
    }

    [Fact]
    public async Task The_export_is_refused_to_somebody_who_may_not_read_it()
    {
        using var response = await SendAsync(HttpMethod.Get, $"{Review}/export", Salesperson);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "an export is the same disclosure as the screen, in a form that leaves the building");
    }

    // --- how often it is due --------------------------------------------------

    [Fact]
    public async Task The_dealership_can_change_how_often_a_review_is_due()
    {
        try
        {
            using var set = await SendAsync(
                HttpMethod.Post, $"{Review}/interval", Manager, new { everyDays = 30 });

            set.StatusCode.Should().Be(HttpStatusCode.NoContent);

            (await ReadAsync(Manager)).GetProperty("reviewEveryDays").GetInt32().Should().Be(30);
        }
        finally
        {
            // Put it back: every other test in this suite reads the default.
            using var restore = await SendAsync(
                HttpMethod.Post, $"{Review}/interval", Manager, new { everyDays = 90 });
            restore.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
    }

    [Fact]
    public async Task A_cadence_that_would_switch_the_obligation_off_is_refused()
    {
        // Zero makes every review instantly overdue; a decade makes the screen
        // say "reviewed" forever. Both are ways of having the setting mean
        // nothing, which is worse than not having it.
        foreach (var days in new[] { 0, -1, 4000 })
        {
            using var response = await SendAsync(
                HttpMethod.Post, $"{Review}/interval", Manager, new { everyDays = days });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                because: $"{days} days is not a review cadence");
        }
    }

    [Fact]
    public async Task Changing_the_cadence_is_managing_policy_not_reviewing()
    {
        // A reviewer must not be able to make themselves on time by moving the
        // date. The seeded manager holds both rights so this cannot be proved by
        // the two coming apart on a real account — it is asserted here as the
        // rule the endpoint is written to, and the refusal below is the evidence
        // that the interval route checks a DIFFERENT permission from the read.
        using var response = await SendAsync(
            HttpMethod.Post, $"{Review}/interval", Salesperson, new { everyDays = 365 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("security.policy_forbidden",
            because: "it is refused as POLICY management, not as a failed review — "
                + "which is how the two rights stay distinguishable");
    }

    // --- plumbing -------------------------------------------------------------

    private async Task<JsonElement> ReadAsync(string email)
    {
        using var response = await SendAsync(HttpMethod.Get, Review, email);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, string email, object? body = null)
    {
        var session = await _fixture.SignInAsync(email, Tenant);
        using var client = _fixture.CreateClient();

        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.Headers.Add("Cookie", $"dfoss_session={session.SessionToken}");
        request.Headers.Add("X-Tenant", Tenant);

        if (method != HttpMethod.Get)
        {
            request.Headers.Add("X-CSRF-Token", session.AntiForgeryToken);
        }

        return await client.SendAsync(request);
    }
}
