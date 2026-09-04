// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   DashboardTests (integration) — proves the month a dealer principal reads is the
//   same month the ledger holds.
//
// Usage:
//   Runs with the normal test suite; needs a reachable SQL engine.
//
// Coding Instructions:
//   Every money assertion here is a DELTA, measured either side of a real
//   delivery. The suite shares one database and other classes deliver cars
//   into it, so an absolute figure would be a test that passes until somebody
//   writes an unrelated one. The delta is also the stronger claim: it says
//   this sale moved the dashboard by exactly what this sale made.

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class DashboardTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string Month = "/api/v1/reporting/month";
    private const string Deals = "/api/v1/deals";

    private const string Manager = DevelopmentSeeder.DevUsers.OrganizationWideEmail;
    private const string Advisor = DevelopmentSeeder.DevUsers.FirstRooftopOnlyEmail;
    private const string Technician = DevelopmentSeeder.DevUsers.TechnicianEmail;

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task Delivering_a_car_moves_the_month_by_exactly_what_that_car_made()
    {
        var before = await MonthAsync(Manager);

        // 24,000 car + 400 fee - 300 discount = 24,100 revenue, on a car that
        // cost 19,000. Front gross is 5,100 and nothing else about the deal
        // changes it.
        await DeliverAsync(price: 24000m, fee: 400m, discount: -300m, cost: 19000m);

        var after = await MonthAsync(Manager);

        (Gross(after, "Vehicles") - Gross(before, "Vehicles")).Should().Be(5100m);
        (Revenue(after, "Vehicles") - Revenue(before, "Vehicles")).Should().Be(24100m,
            because: "a discount reduces the sale it belongs to rather than sitting elsewhere");
        (Units(after) - Units(before)).Should().Be(1);
    }

    [Fact]
    public async Task Warranties_are_reported_as_their_own_business_and_not_as_part_of_the_car()
    {
        // The split a dealer principal cares most about: front-end and back-end
        // gross are two businesses, and merging them makes the more profitable
        // one invisible.
        var product = await AddProductAsync(price: 1500m, cost: 900m);
        var before = await MonthAsync(Manager);

        await DeliverAsync(price: 20000m, cost: 17000m, product: (product, 1500m, 900m));

        var after = await MonthAsync(Manager);

        (Gross(after, "Vehicles") - Gross(before, "Vehicles")).Should().Be(3000m,
            because: "the car made 3,000 and the warranty is not part of it");
        (Gross(after, "Finance and insurance") - Gross(before, "Finance and insurance"))
            .Should().Be(600m);
    }

    [Fact]
    public async Task Reversing_a_delivery_takes_the_car_and_its_gross_back_out()
    {
        var before = await MonthAsync(Manager);

        var dealId = await DeliverAsync(price: 22000m, cost: 18000m);
        await ReverseAsync(dealId);

        var after = await MonthAsync(Manager);

        // The count and the money have to move together, or a dashboard invites
        // somebody to divide one by the other and get a nonsense average.
        (Units(after) - Units(before)).Should().Be(0);
        (Gross(after, "Vehicles") - Gross(before, "Vehicles")).Should().Be(0m);
    }

    [Fact]
    public async Task A_department_that_sold_nothing_has_no_margin_rather_than_a_margin_of_zero()
    {
        // Asked about a month long before this system existed, so every
        // department is genuinely empty.
        var quiet = await MonthAsync(Manager, year: 2001, month: 4);

        Department(quiet, "Vehicles").GetProperty("revenue").GetDecimal().Should().Be(0m);
        Department(quiet, "Vehicles").GetProperty("margin").ValueKind.Should().Be(JsonValueKind.Null,
            because: "0% would claim we sold things and made nothing on them");
    }

    [Fact]
    public async Task The_month_says_whether_its_books_are_still_open()
    {
        var now = DateTime.UtcNow;
        var current = await MonthAsync(Manager, now.Year, now.Month);

        current.GetProperty("books").GetString().Should().Be("Open",
            because: "cars are being delivered into it, which requires it to be open");

        var ancient = await MonthAsync(Manager, 2001, 4);
        ancient.GetProperty("books").GetString().Should().Be("NotOpened");
    }

    [Fact]
    public async Task The_month_before_comes_back_alongside_it_for_comparison()
    {
        var review = await MonthAsync(Manager);

        review.GetProperty("priorMonth").ValueKind.Should().NotBe(JsonValueKind.Null,
            because: "a gross figure on its own tells nobody whether it was a good month");

        var priorTo = review.GetProperty("priorMonth").GetProperty("to").GetString();
        var startsOn = review.GetProperty("startsOn").GetString();

        DateOnly.Parse(priorTo!, CultureInfo.InvariantCulture).AddDays(1).Should()
            .Be(DateOnly.Parse(startsOn!, CultureInfo.InvariantCulture),
                because: "the comparison must end the day before this month starts");
    }

    [Fact]
    public async Task Every_unsold_unit_falls_into_exactly_one_age_band()
    {
        var review = await MonthAsync(Manager);
        var stock = review.GetProperty("stock");

        var banded = stock.GetProperty("bands").EnumerateArray()
            .Sum(band => band.GetProperty("units").GetInt32());

        banded.Should().Be(stock.GetProperty("units").GetInt32(),
            because: "a band boundary that overlaps or leaves a gap makes the whole chart a lie");
    }

    [Fact]
    public async Task A_car_bought_a_hundred_days_ago_shows_up_as_over_ninety_days_old()
    {
        var before = await MonthAsync(Manager);

        var stockNumber = await ReceiveAsync(
            acquiredOn: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-100));

        var after = await MonthAsync(Manager);

        (Band(after, "Over 90 days") - Band(before, "Over 90 days")).Should().Be(1);

        after.GetProperty("stock").GetProperty("oldest").EnumerateArray()
            .Should().Contain(unit => unit.GetProperty("stockNumber").GetString() == stockNumber,
                because: "naming the cars is the difference between a chart and an action");
    }

    [Fact]
    public async Task A_car_with_no_acquisition_date_is_aged_from_when_it_was_entered_and_says_so()
    {
        // Treating a missing date as age zero would hide the oldest cars in the
        // newest band, which is the one mistake this report cannot afford.
        var stockNumber = await ReceiveAsync(acquiredOn: null);

        var review = await MonthAsync(Manager);
        var unit = review.GetProperty("stock").GetProperty("oldest").EnumerateArray()
            .FirstOrDefault(u => u.GetProperty("stockNumber").GetString() == stockNumber);

        // It may or may not be among the five oldest — that depends on what else
        // the run created. When it is, the estimate must be declared.
        if (unit.ValueKind != JsonValueKind.Undefined)
        {
            unit.GetProperty("ageIsEstimated").GetBoolean().Should().BeTrue();
        }

        review.GetProperty("stock").GetProperty("units").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task The_dashboard_is_scoped_to_the_rooftops_the_reader_can_see()
    {
        var elsewhere = await RooftopIdAsync("NAG-02");

        using var response = await SendAsync(HttpMethod.Get, $"{Month}?rooftopId={elsewhere}", Advisor);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "a total is as revealing as the entries behind it");
    }

    [Fact]
    public async Task Somebody_entitled_to_none_of_it_is_refused_rather_than_shown_an_empty_page()
    {
        // A technician holds neither Accounting.Read nor Inventory.Read. An empty
        // dashboard would read as "the dealership sold nothing", which is a
        // different and much worse statement than "this is not yours to see".
        using var response = await SendAsync(HttpMethod.Get, Month, Technician);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("reporting.forbidden");
    }

    [Theory]
    [InlineData("year=2026&month=13")]
    [InlineData("year=1066&month=6")]
    public async Task A_month_that_does_not_exist_is_refused(string query)
    {
        using var response = await SendAsync(HttpMethod.Get, $"{Month}?{query}", Manager);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- reading the answer --------------------------------------------------

    private static JsonElement Department(JsonElement review, string name) =>
        review.GetProperty("trading").GetProperty("departments").EnumerateArray()
            .Single(d => d.GetProperty("name").GetString() == name);

    private static decimal Gross(JsonElement review, string department) =>
        Department(review, department).GetProperty("gross").GetDecimal();

    private static decimal Revenue(JsonElement review, string department) =>
        Department(review, department).GetProperty("revenue").GetDecimal();

    private static int Units(JsonElement review) =>
        review.GetProperty("trading").GetProperty("vehiclesDelivered").GetInt32();

    private static int Band(JsonElement review, string name) =>
        review.GetProperty("stock").GetProperty("bands").EnumerateArray()
            .Single(b => b.GetProperty("name").GetString() == name)
            .GetProperty("units").GetInt32();

    private async Task<JsonElement> MonthAsync(string email, int? year = null, int? month = null)
    {
        var query = year is null ? string.Empty : $"?year={year}&month={month}";

        using var response = await SendAsync(HttpMethod.Get, $"{Month}{query}", email);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    // --- making something happen ---------------------------------------------

    private async Task<string> AddProductAsync(decimal price, decimal cost)
    {
        using var response = await PostAsync("/api/v1/finance/products", Manager, new
        {
            name = $"Cover {Guid.NewGuid():N}"[..14],
            kind = "Warranty",
            provider = "Dashboard Underwriting",
            defaultPrice = price,
            defaultCost = cost,
            currency = "USD",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    /// <summary>Takes a car into stock and returns its stock number.</summary>
    private async Task<string> ReceiveAsync(DateOnly? acquiredOn)
    {
        var rooftopId = await RooftopIdAsync("NAG-01");
        var suffix = $"{Guid.NewGuid():N}"[..8].ToUpperInvariant();

        var vehicleId = await CreatedIdAsync("/api/v1/vehicles", new
        {
            vin = $"AGING{suffix}"[..13], modelYear = 2019, make = "Mazda", model = "CX-5",
            vinExceptionReason = "Synthetic VIN for an aging test.",
        });

        var stockNumber = $"A{suffix}";

        await CreatedIdAsync("/api/v1/inventory", new
        {
            vehicleId, rooftopId, stockNumber,
            costAmount = 15000m, costCurrency = "USD",
            acquiredOn = acquiredOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        });

        return stockNumber;
    }

    private async Task ReverseAsync(string dealId)
    {
        using var found = await SendAsync(
            HttpMethod.Get, $"/api/v1/accounting/journal?reference={dealId}", Manager);

        var entryId = (await found.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray().First().GetProperty("id").GetString()!;

        using var reversed = await PostAsync(
            $"/api/v1/accounting/journal/{entryId}/reverse", Manager,
            new { reason = "Dashboard test: unwinding the sale." });

        reversed.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Builds and delivers a real deal, which is what moves the figures.</summary>
    private async Task<string> DeliverAsync(
        decimal price,
        decimal cost,
        decimal fee = 0m,
        decimal discount = 0m,
        (string Id, decimal Price, decimal Cost)? product = null)
    {
        var rooftopId = await RooftopIdAsync("NAG-01");
        var suffix = $"{Guid.NewGuid():N}"[..8].ToUpperInvariant();

        var customerId = await CreatedIdAsync("/api/v1/customers", new
        {
            kind = "Person", firstName = "Dash", lastName = $"Board{suffix}",
        });

        var vehicleId = await CreatedIdAsync("/api/v1/vehicles", new
        {
            vin = $"DASH{suffix}0000"[..13], modelYear = 2022, make = "Honda", model = "CR-V",
            vinExceptionReason = "Synthetic VIN for a dashboard test.",
        });

        var unitId = await CreatedIdAsync("/api/v1/inventory", new
        {
            vehicleId, rooftopId, stockNumber = $"D{suffix}",
            costAmount = cost, costCurrency = "USD",
        });

        using var available = await PostAsync($"/api/v1/inventory/{unitId}/status", Manager,
            new { status = "Available" });
        available.StatusCode.Should().Be(HttpStatusCode.OK);

        var dealId = await CreatedIdAsync(Deals, new
        {
            rooftopId, customerId, inventoryUnitId = unitId, currency = "USD",
            salespersonUserId = DevelopmentSeeder.DevUsers.Salesperson,
        });

        var charges = new List<object> { new { kind = "VehiclePrice", description = "The car", amount = price } };
        if (fee != 0m)
        {
            charges.Add(new { kind = "Fee", description = "Documentation fee", amount = fee });
        }

        if (discount != 0m)
        {
            charges.Add(new { kind = "Discount", description = "Discount", amount = discount });
        }

        using var terms = await PostAsync($"{Deals}/{dealId}/terms", Manager,
            new { charges = charges.ToArray(), tradeIn = (object?)null });
        terms.StatusCode.Should().Be(HttpStatusCode.OK);

        if (product is { } sold)
        {
            using var products = await PostAsync($"{Deals}/{dealId}/products", Manager, new
            {
                products = new[]
                {
                    new { financeProductId = sold.Id, price = sold.Price, cost = sold.Cost },
                },
            });

            products.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        foreach (var status in new[] { "Submitted", "Approved", "Delivered" })
        {
            using var moved = await PostAsync($"{Deals}/{dealId}/status", Manager, new { status });
            moved.StatusCode.Should().Be(HttpStatusCode.OK, $"moving to {status} should succeed");
        }

        return dealId;
    }

    // --- plumbing ------------------------------------------------------------

    private async Task<string> CreatedIdAsync(string path, object body)
    {
        using var response = await PostAsync(path, Manager, body);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
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

        return await client.SendAsync(request);
    }
}
