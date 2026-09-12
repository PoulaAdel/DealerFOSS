// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   DealTests (integration) — proves a car can be sold once, that a salesperson
//   cannot approve their own deal, and that one rooftop cannot see another's.
//
// Usage:
//   Runs with the normal test suite; needs a reachable SQL engine.
//
// Coding Instructions:
//   The two controls worth guarding here are the inventory hold and the
//   approval split. Both must fail loudly if the checks in DealService are
//   removed.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class DealTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string Deals = "/api/v1/deals";
    private const string Customers = "/api/v1/customers";
    private const string Vehicles = "/api/v1/vehicles";
    private const string Inventory = "/api/v1/inventory";

    private const string Manager = DevelopmentSeeder.DevUsers.OrganizationWideEmail;
    private const string Sales = DevelopmentSeeder.DevUsers.SalespersonEmail;
    private const string Advisor = DevelopmentSeeder.DevUsers.FirstRooftopOnlyEmail;

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task A_deal_can_be_started_priced_submitted_approved_and_delivered()
    {
        var dealId = await StartDealAsync(Manager, await RooftopIdAsync("NAG-01"));

        using var priced = await PostAsync($"{Deals}/{dealId}/terms", Manager, new
        {
            charges = new[]
            {
                new { kind = "VehiclePrice", description = "The car", amount = 26995m },
                new { kind = "Fee", description = "Documentation fee", amount = 399m },
                new { kind = "Discount", description = "Manager discount", amount = -500m },
            },
            tradeIn = new { description = "2014 Civic, 96k", allowance = 4500m, payoff = 1200m },
        });

        priced.StatusCode.Should().Be(HttpStatusCode.OK);
        var terms = await priced.Content.ReadFromJsonAsync<JsonElement>();
        terms.GetProperty("amountDue").GetDecimal().Should().Be(23594m);
        terms.GetProperty("tradeIn").GetProperty("equity").GetDecimal().Should().Be(3300m);

        (await MoveAsync(dealId, Manager, "Submitted")).Should().Be(HttpStatusCode.OK);
        (await MoveAsync(dealId, Manager, "Approved")).Should().Be(HttpStatusCode.OK);
        (await MoveAsync(dealId, Manager, "Delivered")).Should().Be(HttpStatusCode.OK);

        using var final = await SendAsync(HttpMethod.Get, $"{Deals}/{dealId}", Manager);
        var detail = await final.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("status").GetString().Should().Be("Delivered");
        detail.GetProperty("history").EnumerateArray().Should().HaveCount(4);
    }

    [Fact]
    public async Task Starting_a_deal_holds_the_car_so_it_cannot_be_sold_twice()
    {
        var rooftop = await RooftopIdAsync("NAG-01");
        var unitId = await ReceiveAvailableUnitAsync(rooftop);
        var customerId = await AddCustomerAsync();

        using var first = await PostAsync(Deals, Manager, new
        {
            rooftopId = rooftop, customerId, inventoryUnitId = unitId, currency = "USD",
        });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        // The car is now held, so a second deal on it must be refused.
        using var second = await PostAsync(Deals, Manager, new
        {
            rooftopId = rooftop, customerId, inventoryUnitId = unitId, currency = "USD",
        });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await second.Content.ReadAsStringAsync()).Should().Contain("not available");
    }

    [Fact]
    public async Task Cancelling_a_deal_puts_the_car_back_on_the_lot()
    {
        var rooftop = await RooftopIdAsync("NAG-01");
        var unitId = await ReceiveAvailableUnitAsync(rooftop);
        var customerId = await AddCustomerAsync();

        using var started = await PostAsync(Deals, Manager, new
        {
            rooftopId = rooftop, customerId, inventoryUnitId = unitId, currency = "USD",
        });
        var dealId = (await started.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        await AssertUnitStatusAsync(unitId, "OnHold");

        (await MoveAsync(dealId, Manager, "Cancelled")).Should().Be(HttpStatusCode.OK);

        await AssertUnitStatusAsync(unitId, "Available");
    }

    [Fact]
    public async Task Delivering_a_deal_marks_the_car_sold()
    {
        var rooftop = await RooftopIdAsync("NAG-01");
        var unitId = await ReceiveAvailableUnitAsync(rooftop);
        var dealId = await StartDealAsync(Manager, rooftop, unitId);

        await PriceAsync(dealId, Manager);
        await MoveAsync(dealId, Manager, "Submitted");
        await MoveAsync(dealId, Manager, "Approved");
        await MoveAsync(dealId, Manager, "Delivered");

        await AssertUnitStatusAsync(unitId, "Sold");
    }

    // --- the approval control ----------------------------------------------

    [Fact]
    public async Task A_salesperson_can_build_a_deal_but_cannot_approve_it()
    {
        // The whole point of splitting Deals.Write from Deals.Approve.
        var rooftop = await RooftopIdAsync("NAG-01");
        var dealId = await StartDealAsync(Sales, rooftop);

        await PriceAsync(dealId, Sales);
        (await MoveAsync(dealId, Sales, "Submitted")).Should().Be(HttpStatusCode.OK);

        (await MoveAsync(dealId, Sales, "Approved")).Should().Be(HttpStatusCode.Forbidden,
            because: "approving is a manager's right, not the author's");

        (await MoveAsync(dealId, Manager, "Approved")).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Nobody_approves_their_own_deal_even_holding_the_permission()
    {
        // The manager holds Deals.Approve and every other right. It is still
        // their own deal, so a sales manager has to sign it off instead.
        var rooftop = await RooftopIdAsync("NAG-01");
        var unitId = await ReceiveAvailableUnitAsync(rooftop);

        using var started = await PostAsync(Deals, Manager, new
        {
            rooftopId = rooftop,
            customerId = await AddCustomerAsync(),
            inventoryUnitId = unitId,
            currency = "USD",
            // No salesperson given, so it defaults to whoever started it.
        });
        var dealId = (await started.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        await PriceAsync(dealId, Manager);
        (await MoveAsync(dealId, Manager, "Submitted")).Should().Be(HttpStatusCode.OK);

        using var ownApproval = await PostAsync($"{Deals}/{dealId}/status", Manager, new { status = "Approved" });

        ownApproval.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ownApproval.Content.ReadAsStringAsync()).Should().Contain("your own deal");
    }

    [Fact]
    public async Task An_advisor_can_see_deals_but_cannot_start_one()
    {
        using var read = await SendAsync(HttpMethod.Get, Deals, Advisor);
        read.StatusCode.Should().Be(HttpStatusCode.OK);

        using var write = await PostAsync(Deals, Advisor, new
        {
            rooftopId = await RooftopIdAsync("NAG-01"),
            customerId = await AddCustomerAsync(),
            inventoryUnitId = await ReceiveAvailableUnitAsync(await RooftopIdAsync("NAG-01")),
            currency = "USD",
        });

        write.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- the rooftop boundary ----------------------------------------------

    [Fact]
    public async Task A_rooftop_scoped_user_does_not_see_another_rooftops_deals()
    {
        var mine = await StartDealAsync(Manager, await RooftopIdAsync("NAG-01"));
        var theirs = await StartDealAsync(Manager, await RooftopIdAsync("NAG-02"));

        var visible = await ListIdsAsync($"{Deals}?limit=200", Advisor);

        visible.Should().Contain(mine);
        visible.Should().NotContain(theirs,
            because: "the response must never carry another rooftop's deals");
    }

    [Fact]
    public async Task A_rooftop_scoped_user_is_refused_another_rooftops_deal_by_direct_id()
    {
        var dealId = await StartDealAsync(Manager, await RooftopIdAsync("NAG-02"));

        using var response = await SendAsync(HttpMethod.Get, $"{Deals}/{dealId}", Advisor);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_car_from_another_rooftop_cannot_be_put_on_this_rooftops_deal()
    {
        var otherLotUnit = await ReceiveAvailableUnitAsync(await RooftopIdAsync("NAG-02"));

        using var response = await PostAsync(Deals, Manager, new
        {
            rooftopId = await RooftopIdAsync("NAG-01"),
            customerId = await AddCustomerAsync(),
            inventoryUnitId = otherLotUnit,
            currency = "USD",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("another location");
    }

    // --- helpers -----------------------------------------------------------

    private static string Unique() => $"D{Guid.NewGuid():N}"[..10].ToUpperInvariant();

    private static string UniqueVin()
    {
        var body = Guid.NewGuid().ToString("N").ToUpperInvariant()
            .Replace("I", "1", StringComparison.Ordinal)
            .Replace("O", "0", StringComparison.Ordinal)
            .Replace("Q", "9", StringComparison.Ordinal);

        return body[..17];
    }

    private async Task<string> AddCustomerAsync()
    {
        using var response = await PostAsync(Customers, Manager, new
        {
            kind = "Person", firstName = "Deal", lastName = $"Test{Guid.NewGuid():N}"[..12],
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    /// <summary>Records a vehicle, takes it into stock, and makes it sellable.</summary>
    private async Task<string> ReceiveAvailableUnitAsync(string rooftopId)
    {
        using var vehicle = await PostAsync(Vehicles, Manager, new
        {
            vin = UniqueVin(), modelYear = 2021, make = "Toyota", model = "RAV4",
        });
        var vehicleId = (await vehicle.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        using var received = await PostAsync(Inventory, Manager, new
        {
            vehicleId, rooftopId, stockNumber = Unique(),
        });
        received.StatusCode.Should().Be(HttpStatusCode.Created);
        var unitId = (await received.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        using var available = await PostAsync($"{Inventory}/{unitId}/status", Manager,
            new { status = "Available" });
        available.StatusCode.Should().Be(HttpStatusCode.OK);

        return unitId;
    }

    /// <summary>
    /// Starts a deal belonging to the salesperson, so a manager is free to approve
    /// it. A deal whose salesperson is the manager cannot be approved by them —
    /// that is the segregation-of-duties rule, not a quirk of the fixture.
    /// </summary>
    // --- tax (ADR-024) -------------------------------------------------------

    [Fact]
    public async Task A_person_can_enter_the_tax_and_the_deal_records_that_a_person_did()
    {
        // The whole point of the Manual pack: a dealership in a jurisdiction
        // nobody has written rates for is not blocked, and the record says so
        // rather than presenting a typed figure as if a rate table produced it.
        var rooftopId = await RooftopIdAsync("NAG-01");
        var dealId = await StartDealAsync(Manager, rooftopId);
        await PriceAsync(dealId, Manager);

        using var response = await PostAsync($"{Deals}/{dealId}/tax", Manager, new
        {
            lines = new[]
            {
                new
                {
                    description = "Sales tax",
                    jurisdiction = "US-IL-SANGAMON",
                    basis = 24000m,
                    rate = 0.0625m,
                    amount = 1500m,
                    provenance = "EnteredByPerson",
                },
            },
            taxedAt = new
            {
                administrativeArea = "IL",
                county = "Sangamon",
                postalCode = "62704",
                country = "US",
            },
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());

        var deal = await response.Content.ReadFromJsonAsync<JsonElement>();
        var line = deal.GetProperty("taxLines").EnumerateArray().Single();

        line.GetProperty("provenance").GetString().Should().Be("EnteredByPerson");
        line.GetProperty("packId").ValueKind.Should().Be(JsonValueKind.Null,
            because: "a typed figure must not claim a pack produced it");

        deal.GetProperty("taxTotal").GetDecimal().Should().Be(1500m);
        deal.GetProperty("amountDue").GetDecimal().Should().Be(25500m,
            because: "a total that leaves the tax out is the number disputed at delivery");

        // State and county separately — the gap closed on 2026-09-05, and the
        // reason the county column exists at all.
        deal.GetProperty("taxedAt").GetProperty("administrativeArea").GetString().Should().Be("IL");
        deal.GetProperty("taxedAt").GetProperty("county").GetString().Should().Be("Sangamon");
    }

    [Fact]
    public async Task Tax_claiming_to_come_from_a_pack_it_cannot_name_is_refused()
    {
        var rooftopId = await RooftopIdAsync("NAG-01");
        var dealId = await StartDealAsync(Manager, rooftopId);
        await PriceAsync(dealId, Manager);

        using var response = await PostAsync($"{Deals}/{dealId}/tax", Manager, new
        {
            lines = new[]
            {
                new
                {
                    description = "Sales tax",
                    jurisdiction = "US-IL",
                    basis = 24000m,
                    rate = 0.0625m,
                    amount = 1500m,
                    provenance = "Pack",
                },
            },
            taxedAt = new { administrativeArea = "IL", county = "Sangamon", postalCode = "62704", country = "US" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: "a rate-table figure that cannot name its table cannot be audited");
    }

    [Fact]
    public async Task Tax_is_frozen_once_the_deal_leaves_draft()
    {
        var rooftopId = await RooftopIdAsync("NAG-01");
        var dealId = await StartDealAsync(Manager, rooftopId);
        await PriceAsync(dealId, Manager);

        object tax(decimal amount) => new
        {
            lines = new[]
            {
                new
                {
                    description = "Sales tax",
                    jurisdiction = "US-IL",
                    basis = 24000m,
                    rate = 0m,
                    amount,
                    provenance = "EnteredByPerson",
                },
            },
            taxedAt = new { administrativeArea = "IL", county = "Sangamon", postalCode = "62704", country = "US" },
        };

        (await PostAsync($"{Deals}/{dealId}/tax", Manager, tax(1500m))).StatusCode
            .Should().Be(HttpStatusCode.OK);

        (await MoveAsync(dealId, Manager, "Submitted")).Should().Be(HttpStatusCode.OK);

        using var late = await PostAsync($"{Deals}/{dealId}/tax", Manager, tax(1m));

        late.StatusCode.Should().Be(HttpStatusCode.Conflict,
            because: "the figures a manager approved are what the customer was told");
    }

    private async Task<string> StartDealAsync(string email, string rooftopId, string? unitId = null)
    {
        var inventoryUnitId = unitId ?? await ReceiveAvailableUnitAsync(rooftopId);

        using var response = await PostAsync(Deals, email, new
        {
            rooftopId,
            customerId = await AddCustomerAsync(),
            inventoryUnitId,
            currency = "USD",
            salespersonUserId = DevelopmentSeeder.DevUsers.Salesperson,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private async Task PriceAsync(string dealId, string email)
    {
        using var response = await PostAsync($"{Deals}/{dealId}/terms", email, new
        {
            charges = new[] { new { kind = "VehiclePrice", description = "The car", amount = 24000m } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<HttpStatusCode> MoveAsync(string dealId, string email, string status)
    {
        using var response = await PostAsync($"{Deals}/{dealId}/status", email, new { status });
        return response.StatusCode;
    }

    private async Task AssertUnitStatusAsync(string unitId, string expected)
    {
        using var response = await SendAsync(HttpMethod.Get, $"{Inventory}/{unitId}", Manager);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("status").GetString().Should().Be(expected);
    }

    private async Task<IReadOnlyList<string>> ListIdsAsync(string path, string email)
    {
        using var response = await SendAsync(HttpMethod.Get, path, email);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var results = await response.Content.ReadFromJsonAsync<JsonElement>();
        return results.Rows().Select(d => d.GetProperty("id").GetString()!).ToList();
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
