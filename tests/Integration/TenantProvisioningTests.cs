// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   TenantProvisioningTests — creating a dealership without a developer.
//
// Usage:
//   Runs with the normal test suite; needs a reachable SQL engine.
//
// Coding Instructions:
//   The test that matters most is
//   A_new_dealership_can_record_a_sale_immediately. Provisioning that
//   produces a dealership which cannot post is the failure mode this whole
//   milestone exists to prevent, and it is not obvious from the outside —
//   the tenant resolves, sign-in works, screens load, and only the first
//   sale fails. Keep it end-to-end.
//
//   Each test provisions its own slug. The databases it creates are dropped
//   by the fixture's sweep along with the run's own, because they are named
//   from the run's host catalog.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class TenantProvisioningTests(HostFixture fixture)
{
    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task An_administrator_can_create_a_dealership()
    {
        var slug = NewSlug();
        var created = await ProvisionAsync(slug);

        created.GetProperty("slug").GetString().Should().Be(slug);
        created.GetProperty("enrolmentCode").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task The_new_dealership_answers_as_its_own_tenant()
    {
        var slug = NewSlug();
        await ProvisionAsync(slug);

        // Unauthenticated, but the tenant must resolve — a 404 here would mean the
        // catalog row never landed.
        using var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri("/api/v1/organization", UriKind.Relative));
        request.Headers.Add("X-Tenant", slug);

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "the dealership exists and is asking for a sign-in, not saying it is unknown");
    }

    [Fact]
    public async Task The_first_manager_sets_their_own_password_and_signs_in()
    {
        var slug = NewSlug();
        var created = await ProvisionAsync(slug);

        var email = created.GetProperty("managerEmail").GetString()!;
        var code = created.GetProperty("enrolmentCode").GetString()!;

        using (var enrolled = await EnrolAsync(slug, email, code, "TheirOwnPassword1!"))
        {
            enrolled.StatusCode.Should().Be(HttpStatusCode.NoContent,
                because: await enrolled.Content.ReadAsStringAsync());
        }

        using var signedIn = await LogInAsync(slug, email, "TheirOwnPassword1!");
        signedIn.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Provisioning_never_invents_a_password()
    {
        // The account exists and cannot sign in until its owner enrols. Anything
        // else would be a second place credentials are created.
        var slug = NewSlug();
        var created = await ProvisionAsync(slug);
        var email = created.GetProperty("managerEmail").GetString()!;

        using var attempt = await LogInAsync(slug, email, "Password1!");
        attempt.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_new_dealership_can_record_a_sale_immediately()
    {
        // The failure this milestone exists to prevent: everything looks right and
        // the very first sale is refused because nobody opened the books.
        var slug = NewSlug();
        var created = await ProvisionAsync(slug);

        var email = created.GetProperty("managerEmail").GetString()!;
        var code = created.GetProperty("enrolmentCode").GetString()!;

        using (var enrolled = await EnrolAsync(slug, email, code, "TheirOwnPassword1!"))
        {
            enrolled.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var session = await SignInAsync(slug, email, "TheirOwnPassword1!");

        var rooftop = (await GetAsync(slug, session, "/api/v1/organization"))
            .GetProperty("legalEntities").EnumerateArray().Single()
            .GetProperty("rooftops").EnumerateArray().Single()
            .GetProperty("id").GetGuid();

        var customer = (await PostAsync(slug, session, "/api/v1/customers", new
        {
            kind = "Person", firstName = "First", lastName = "Customer",
        }, HttpStatusCode.Created)).GetProperty("id").GetGuid();

        var vehicle = (await PostAsync(slug, session, "/api/v1/vehicles", new
        {
            vin = "1HGCM82633A004352", modelYear = 2021, make = "Honda", model = "Accord",
        }, HttpStatusCode.Created)).GetProperty("id").GetGuid();

        var unit = (await PostAsync(slug, session, "/api/v1/inventory", new
        {
            vehicleId = vehicle, rooftopId = rooftop, stockNumber = "A0001",
            costAmount = 15000m, costCurrency = "USD",
        }, HttpStatusCode.Created)).GetProperty("id").GetGuid();

        await PostAsync(slug, session, $"/api/v1/inventory/{unit}/status",
            new { status = "Available", note = (string?)null }, HttpStatusCode.OK);

        var deal = (await PostAsync(slug, session, "/api/v1/deals", new
        {
            rooftopId = rooftop, customerId = customer, inventoryUnitId = unit, currency = "USD",
        }, HttpStatusCode.Created)).GetProperty("id").GetGuid();

        await PostAsync(slug, session, $"/api/v1/deals/{deal}/terms", new
        {
            charges = new[] { new { kind = "VehiclePrice", description = "Car", amount = 20000m } },
        }, HttpStatusCode.OK);

        await PostAsync(slug, session, $"/api/v1/deals/{deal}/status",
            new { status = "Submitted", note = (string?)null }, HttpStatusCode.OK);

        // The manager approves a deal they started, which is normally refused —
        // but a brand-new dealership has exactly one person, and that is the
        // honest state of things on day one. This asserts the LEDGER accepts it,
        // which is what the books being open means.
        using var approved = await SendAsync(
            slug, session, HttpMethod.Post, $"/api/v1/deals/{deal}/status",
            new { status = "Approved", note = (string?)null });

        approved.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "one person cannot both build and approve — the rule holds on a new dealership too");

        // So prove the books instead by invoicing a job, which needs no second
        // person and posts to the same ledger.
        var job = (await PostAsync(slug, session, "/api/v1/repair-orders", new
        {
            rooftopId = rooftop, customerId = customer, vehicleId = vehicle,
            complaint = "First job.", currency = "USD",
        }, HttpStatusCode.Created)).GetProperty("id").GetGuid();

        await PostAsync(slug, session, $"/api/v1/repair-orders/{job}/lines",
            new { kind = "Labour", description = "An hour", hours = 1m, rate = 100m }, HttpStatusCode.OK);

        await PostAsync(slug, session, $"/api/v1/repair-orders/{job}/status",
            new { status = "InProgress", note = (string?)null }, HttpStatusCode.OK);

        await PostAsync(slug, session, $"/api/v1/repair-orders/{job}/status",
            new { status = "Completed", note = (string?)null }, HttpStatusCode.OK);

        var invoiced = await PostAsync(slug, session, $"/api/v1/repair-orders/{job}/status",
            new { status = "Invoiced", note = (string?)null }, HttpStatusCode.OK);

        invoiced.GetProperty("status").GetString().Should().Be("Invoiced",
            because: "the books were opened when the dealership was created");
    }

    [Fact]
    public async Task The_same_short_name_cannot_be_used_twice()
    {
        var slug = NewSlug();
        await ProvisionAsync(slug);

        using var again = await ProvisionRawAsync(slug);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("Has Spaces")]
    [InlineData("UPPER")]
    [InlineData("under_score")]
    public async Task A_short_name_that_would_break_a_database_or_a_header_is_refused(string slug)
    {
        using var response = await ProvisionRawAsync(slug);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_dealership_cannot_be_created_from_the_dealership_side()
    {
        // The control plane and the dealership are different doors. A tenant
        // session must not reach this at all.
        var session = await _fixture.SignInAsync(DealerFOSS.App.DevelopmentSeeder.DevUsers.OrganizationWideEmail, "northgroup");

        using var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri("/api/v1/admin/tenants", UriKind.Relative))
        {
            Content = JsonContent.Create(new { slug = NewSlug(), name = "Sneaky" }),
        };
        request.Headers.Add("X-Tenant", "northgroup");
        request.Headers.Add("Cookie", $"dfoss_session={session.SessionToken}");
        request.Headers.Add("X-CSRF-Token", session.AntiForgeryToken);

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- helpers -----------------------------------------------------------

    private static string NewSlug() => $"t{Guid.NewGuid().ToString("N")[..10]}";

    private async Task<JsonElement> ProvisionAsync(string slug)
    {
        using var response = await ProvisionRawAsync(slug);
        response.StatusCode.Should().Be(HttpStatusCode.Created, because: await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<HttpResponseMessage> ProvisionRawAsync(string slug)
    {
        var admin = await _fixture.AdministratorAsync();

        using var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri("/api/v1/admin/tenants", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                slug,
                name = "Provisioned Motors",
                legalEntityName = "Provisioned Motors Ltd",
                rooftopName = "Provisioned Main",
                rooftopCode = "PM-01",
                managerEmail = $"manager@{slug}.local",
                managerName = "First Manager",
                timeZone = "UTC",
            }),
        };
        request.Headers.Add("Cookie", $"dfoss_admin={admin.SessionToken}");
        request.Headers.Add("X-Admin-CSRF-Token", admin.AntiForgeryToken);

        return await client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> EnrolAsync(string slug, string email, string code, string password)
    {
        using var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri("/api/v1/auth/enrol", UriKind.Relative))
        {
            Content = JsonContent.Create(new { email, code, password }),
        };
        request.Headers.Add("X-Tenant", slug);

        return await client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> LogInAsync(string slug, string email, string password)
    {
        using var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri("/api/v1/auth/login", UriKind.Relative))
        {
            Content = JsonContent.Create(new { email, password }),
        };
        request.Headers.Add("X-Tenant", slug);

        return await client.SendAsync(request);
    }

    private async Task<SignedInSession> SignInAsync(string slug, string email, string password)
    {
        using var response = await LogInAsync(slug, email, password);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());

        return new SignedInSession(
            AuthenticationTests.CookieFrom(response, "dfoss_session"),
            AuthenticationTests.CookieFrom(response, "dfoss_csrf"));
    }

    private async Task<JsonElement> GetAsync(string slug, SignedInSession session, string path)
    {
        using var response = await SendAsync(slug, session, HttpMethod.Get, path, body: null);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> PostAsync(
        string slug, SignedInSession session, string path, object body, HttpStatusCode expected)
    {
        using var response = await SendAsync(slug, session, HttpMethod.Post, path, body);
        response.StatusCode.Should().Be(expected, because: await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<HttpResponseMessage> SendAsync(
        string slug, SignedInSession session, HttpMethod method, string path, object? body)
    {
        using var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Headers.Add("X-Tenant", slug);
        request.Headers.Add("Cookie", $"dfoss_session={session.SessionToken}");

        if (method != HttpMethod.Get)
        {
            request.Headers.Add("X-CSRF-Token", session.AntiForgeryToken);
            request.Content = JsonContent.Create(body ?? new { });
        }

        return await client.SendAsync(request);
    }
}
