// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   OwnRecordsOnlyTests — a grant that reaches only the records its holder is
//   named on, across every capability that names one.
//
//   THE CONTROL IS THE POINT OF THIS FILE. `ownwork@dev.local` holds the SAME
//   role at the SAME rooftop as `sales@dev.local`; the only difference between
//   the two accounts is `OwnRecordsOnly` on the assignment. So every test here
//   runs both and asserts they differ — a refusal the salesperson also gets is
//   a role gap, not this dimension, and proves nothing. That comparison is what
//   makes the assertions attributable (ADR-030).
//
// Usage:
//   Runs with the normal test suite; needs a reachable SQL engine.
//
// Coding Instructions:
//   Cover the LIST and the RECORD for each capability. They are enforced in two
//   different places — a `Where` on the query, and a check inside the service —
//   and gating one while forgetting the other is exactly the failure this
//   dimension exists to prevent. A list that hides a deal while the deal is
//   still readable by its id is not scoped; it is decorated.
//
//   The enquiry pool is the deliberate exception and has its own test. Do not
//   "fix" it to match the others: an unclaimed enquiry has to be reachable or
//   nobody limited to their own work could ever claim one, which is the act
//   that makes it theirs.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class OwnRecordsOnlyTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";

    /// <summary>Organization-wide. Builds the records; never the subject.</summary>
    private const string Manager = DevelopmentSeeder.DevUsers.OrganizationWideEmail;

    /// <summary>Sales role at NAG-01, unrestricted. The control.</summary>
    private const string Salesperson = DevelopmentSeeder.DevUsers.SalespersonEmail;

    /// <summary>The same role at the same rooftop, narrowed to their own work.</summary>
    private const string OwnWorkOnly = DevelopmentSeeder.DevUsers.OwnWorkOnlyEmail;

    private readonly HostFixture _fixture = fixture;

    // --- deals ---------------------------------------------------------------

    [Fact]
    public async Task A_deal_somebody_else_is_selling_is_not_in_their_list()
    {
        var deal = await ADealSoldByAsync(DevelopmentSeeder.DevUsers.Salesperson);

        (await ListsDealAsync(Salesperson, deal)).Should().BeTrue(
            because: "the unrestricted salesperson at this lot sees the lot's deals — "
                + "without this the next assertion would pass for the wrong reason");

        (await ListsDealAsync(OwnWorkOnly, deal)).Should().BeFalse(
            because: "the only difference between these two accounts is OwnRecordsOnly");
    }

    [Fact]
    public async Task And_it_is_not_reachable_by_its_id_either()
    {
        // The half that matters. Hiding a record from a list while leaving it
        // readable by id is not scope, it is decoration.
        var deal = await ADealSoldByAsync(DevelopmentSeeder.DevUsers.Salesperson);

        using var control = await SendAsync(HttpMethod.Get, $"/api/v1/deals/{deal}", Salesperson);
        control.StatusCode.Should().Be(HttpStatusCode.OK);

        using var narrowed = await SendAsync(HttpMethod.Get, $"/api/v1/deals/{deal}", OwnWorkOnly);
        narrowed.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Their_own_deal_is_theirs_to_read_and_to_work_on()
    {
        // The other direction, and the one that would fail if the narrowing were
        // simply "refuse everything".
        var mine = await ADealSoldByAsync(DevelopmentSeeder.DevUsers.OwnWorkOnly);

        using var read = await SendAsync(HttpMethod.Get, $"/api/v1/deals/{mine}", OwnWorkOnly);
        read.StatusCode.Should().Be(HttpStatusCode.OK);

        using var write = await SendAsync(
            HttpMethod.Post, $"/api/v1/deals/{mine}/terms", OwnWorkOnly,
            new
            {
                charges = new object[]
                {
                    new { kind = "VehiclePrice", description = "A car", amount = 15000m },
                },
                tradeIn = (object?)null,
            });

        write.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "a narrowed grant still does the whole job on the work it covers");
    }

    [Fact]
    public async Task A_deal_nobody_is_named_on_is_not_everybody_s()
    {
        // Unlike an enquiry. A deal with no salesperson is not an invitation, and
        // AuthorizedScope.Allows refuses a null owner for exactly this case.
        var orphan = await ADealSoldByAsync(salesperson: null);

        (await ListsDealAsync(Salesperson, orphan)).Should().BeTrue();
        (await ListsDealAsync(OwnWorkOnly, orphan)).Should().BeFalse();
    }

    // --- enquiries, which are deliberately different --------------------------

    [Fact]
    public async Task An_enquiry_nobody_has_taken_stays_reachable_so_it_can_be_taken()
    {
        // The documented exception. Hiding the pool would leave somebody limited
        // to their own work unable to ever claim one — and claiming is the act
        // that makes an enquiry theirs.
        var pooled = await AnEnquiryAssignedToAsync(null);

        using var response = await SendAsync(HttpMethod.Get, $"/api/v1/leads/{pooled}", OwnWorkOnly);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "an unclaimed enquiry is the pool, and the pool is everybody's at this lot");
    }

    [Fact]
    public async Task An_enquiry_somebody_else_has_taken_is_not()
    {
        var theirs = await AnEnquiryAssignedToAsync(DevelopmentSeeder.DevUsers.Salesperson);

        using var control = await SendAsync(HttpMethod.Get, $"/api/v1/leads/{theirs}", Salesperson);
        control.StatusCode.Should().Be(HttpStatusCode.OK);

        using var narrowed = await SendAsync(HttpMethod.Get, $"/api/v1/leads/{theirs}", OwnWorkOnly);
        narrowed.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "the pool exception is about UNCLAIMED enquiries, not about everybody's");
    }

    // --- the grant itself -----------------------------------------------------

    [Fact]
    public async Task The_narrowing_is_visible_to_whoever_reviews_who_holds_what()
    {
        // A right nobody can see is not reviewable, and the access review is the
        // reason half of this exists (ADR-030, and the Safeguards Rule behind it).
        using var response = await SendAsync(
            HttpMethod.Get, $"/api/v1/staff/{DevelopmentSeeder.DevUsers.OwnWorkOnly}", Manager);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var staff = await response.Content.ReadFromJsonAsync<JsonElement>();
        var assignment = staff.GetProperty("assignments").EnumerateArray().Should().ContainSingle().Subject;

        assignment.GetProperty("ownRecordsOnly").GetBoolean().Should().BeTrue();

        // And the control: the unrestricted salesperson's grant reads false, so
        // the field is reporting the assignment rather than always saying yes.
        using var other = await SendAsync(
            HttpMethod.Get, $"/api/v1/staff/{DevelopmentSeeder.DevUsers.Salesperson}", Manager);

        var control = await other.Content.ReadFromJsonAsync<JsonElement>();
        control.GetProperty("assignments").EnumerateArray()
            .Should().Contain(a => a.GetProperty("ownRecordsOnly").GetBoolean() == false);
    }

    // --- building something to be refused -------------------------------------

    private async Task<Guid> ADealSoldByAsync(Guid? salesperson)
    {
        var rooftop = await RooftopAsync("NAG-01");
        var customer = await ACustomerAsync();
        var unit = await ACarInStockAsync(rooftop);

        // Started by whoever will own it, because DealService names the CALLER as
        // the salesperson — which is the honest way to produce a deal that really
        // does belong to somebody.
        var asWhom = salesperson switch
        {
            null => Manager,
            var id when id == DevelopmentSeeder.DevUsers.OwnWorkOnly => OwnWorkOnly,
            _ => Salesperson,
        };

        var body = await PostAsync("/api/v1/deals", asWhom, new
        {
            rooftopId = rooftop, customerId = customer, inventoryUnitId = unit, currency = "USD",
        }, HttpStatusCode.Created);

        return body.GetProperty("id").GetGuid();
    }

    private async Task<Guid> AnEnquiryAssignedToAsync(Guid? assignee)
    {
        var body = await PostAsync("/api/v1/leads", Manager, new
        {
            rooftopId = await RooftopAsync("NAG-01"),
            customerId = await ACustomerAsync(),
            source = "Website",
            assignedToUserId = assignee,
            enquiry = "Asked what it would be on finance.",
        }, HttpStatusCode.Created);

        return body.GetProperty("id").GetGuid();
    }

    private async Task<Guid> ACustomerAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var body = await PostAsync("/api/v1/customers", Manager, new
        {
            kind = "Person",
            firstName = "Own",
            lastName = $"Work{tag}",
            email = $"own.work.{tag}@example.test",
        }, HttpStatusCode.Created);

        return body.GetProperty("id").GetGuid();
    }

    private async Task<Guid> ACarInStockAsync(Guid rooftop)
    {
        var tag = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var vehicle = (await PostAsync("/api/v1/vehicles", Manager, new
        {
            vin = $"JH4KA96{tag}12",   // 7 + 8 + 2 = the 17 a real VIN has
            modelYear = 2020,
            make = "Acura",
            model = "Legend",
            bodyStyle = "Saloon",
        }, HttpStatusCode.Created)).GetProperty("id").GetGuid();

        var unit = (await PostAsync("/api/v1/inventory", Manager, new
        {
            vehicleId = vehicle,
            rooftopId = rooftop,
            stockNumber = $"OWN-{tag}",
            costAmount = 9000m,
            costCurrency = "USD",
        }, HttpStatusCode.Created)).GetProperty("id").GetGuid();

        await PostAsync($"/api/v1/inventory/{unit}/status", Manager,
            new { status = "Available", note = (string?)null }, HttpStatusCode.OK);

        return unit;
    }

    // --- plumbing -------------------------------------------------------------

    private async Task<bool> ListsDealAsync(string email, Guid dealId)
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/v1/deals?limit=200", email);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());

        var page = await response.Content.ReadFromJsonAsync<JsonElement>();

        return page.GetProperty("rows").EnumerateArray()
            .Any(row => row.GetProperty("id").GetGuid() == dealId);
    }

    private async Task<Guid> RooftopAsync(string code)
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/v1/organization", Manager);

        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("legalEntities").EnumerateArray()
            .SelectMany(e => e.GetProperty("rooftops").EnumerateArray())
            .Single(r => r.GetProperty("code").GetString() == code)
            .GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> PostAsync(
        string path, string email, object body, HttpStatusCode expected)
    {
        using var response = await SendAsync(HttpMethod.Post, path, email, body);

        response.StatusCode.Should().Be(expected,
            because: $"{path} should have been written: " + await response.Content.ReadAsStringAsync());

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
