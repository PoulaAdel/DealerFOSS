// FinanceProductTests — selling warranties and cover with a car, and the gross
// that comes off them.
//
// Use:  runs with the normal test suite; needs a reachable SQL engine.
// Edit: the test that matters most is
//       Repricing_the_catalogue_does_not_touch_a_deal_already_done. F&I is
//       negotiated per deal, so the price and cost are copied onto the sale — if
//       either were ever read live from the catalogue, next month's price list
//       would silently rewrite last month's gross. That is the same hazard as a
//       part's cost, and it is the reason this capability is shaped the way it is.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class FinanceProductTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string Products = "/api/v1/finance/products";

    private const string Manager = DevelopmentSeeder.DevUsers.OrganizationWideEmail;
    private const string Salesperson = DevelopmentSeeder.DevUsers.SalespersonEmail;

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task A_manager_can_add_a_product_to_the_catalogue()
    {
        var product = await AddProductAsync(price: 1200m, cost: 700m);

        using var response = await SendAsync(HttpMethod.Get, Products, Manager);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await response.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray()
            .Select(p => p.GetProperty("id").GetGuid())
            .Should().Contain(product);
    }

    [Fact]
    public async Task A_rooftop_scoped_user_cannot_change_the_catalogue()
    {
        // A provider arrangement is made for the group. One lot inventing its own
        // version of the same warranty is how a catalogue stops being trustworthy.
        using var response = await SendAsync(
            HttpMethod.Post, Products, Salesperson,
            new { name = $"Sneaky {Guid.NewGuid():N}", kind = "Warranty", provider = "X", defaultPrice = 1m, defaultCost = 1m });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_salesperson_can_still_see_what_they_may_offer()
    {
        // Reading rides on Deals.Read for exactly this reason: a salesperson one
        // missing grant away from an empty menu is a salesperson who cannot sell.
        await AddProductAsync();

        using var response = await SendAsync(HttpMethod.Get, Products, Salesperson);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task A_product_needs_a_provider()
    {
        using var response = await SendAsync(
            HttpMethod.Post, Products, Manager,
            new { name = $"No provider {Guid.NewGuid():N}", kind = "Gap", provider = "", defaultPrice = 1m, defaultCost = 1m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: "'who underwrites this' is the first question asked about a product");
    }

    [Fact]
    public async Task Selling_a_product_adds_its_price_to_what_the_customer_owes()
    {
        var product = await AddProductAsync(price: 1200m, cost: 700m);
        var deal = await StartDealAsync(vehiclePrice: 20000m);

        var withProduct = await SetProductsAsync(deal, [(product, 1200m, 700m)]);

        withProduct.GetProperty("amountDue").GetDecimal().Should().Be(21200m);
        withProduct.GetProperty("productGross").GetDecimal().Should().Be(500m);
    }

    [Fact]
    public async Task The_gross_is_what_it_sold_for_less_what_it_cost()
    {
        // This and Repricing_the_catalogue_does_not_touch_a_deal_already_done are
        // the two halves of one rule, and each catches something the other misses:
        // this one that the NEGOTIATED figure is what gets recorded at the moment
        // of sale, that one that a later catalogue change cannot reach back. Both
        // were confirmed by rehearsal — reading the catalogue price at sale time
        // fails only this one.
        var product = await AddProductAsync(price: 1200m, cost: 700m);
        var deal = await StartDealAsync(vehiclePrice: 20000m);

        // Discounted to hold the deal together — an everyday F&I move, and the
        // gross has to follow the price that was actually agreed.
        var sold = await SetProductsAsync(deal, [(product, 900m, 700m)]);

        sold.GetProperty("productGross").GetDecimal().Should().Be(200m);
        sold.GetProperty("products").EnumerateArray().Single()
            .GetProperty("price").GetDecimal().Should().Be(900m);
    }

    [Fact]
    public async Task Repricing_the_catalogue_does_not_touch_a_deal_already_done()
    {
        // The whole reason price and cost are copied onto the sale.
        var product = await AddProductAsync(price: 1200m, cost: 700m);
        var deal = await StartDealAsync(vehiclePrice: 20000m);

        await SetProductsAsync(deal, [(product, 1200m, 700m)]);

        using (var repriced = await SendAsync(
            HttpMethod.Post, $"{Products}/{product}/price", Manager,
            new { defaultPrice = 1500m, defaultCost = 950m }))
        {
            repriced.StatusCode.Should().Be(HttpStatusCode.OK, because: await repriced.Content.ReadAsStringAsync());
        }

        var after = await GetDealAsync(deal);

        after.GetProperty("amountDue").GetDecimal().Should().Be(21200m,
            because: "next month's price list must not rewrite this month's deal");
        after.GetProperty("productGross").GetDecimal().Should().Be(500m);
    }

    [Fact]
    public async Task A_withdrawn_product_cannot_be_added_to_a_new_deal()
    {
        var product = await AddProductAsync();

        using (var withdrawn = await SendAsync(
            HttpMethod.Post, $"{Products}/{product}/available", Manager, new { available = false }))
        {
            withdrawn.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var deal = await StartDealAsync(vehiclePrice: 20000m);

        using var refused = await SendAsync(
            HttpMethod.Post, $"/api/v1/deals/{deal}/products", Manager,
            new { products = new[] { new { financeProductId = product, price = 100m, cost = 50m } } });

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_withdrawn_product_stays_on_the_deals_that_already_sold_it()
    {
        var product = await AddProductAsync(price: 800m, cost: 400m);
        var deal = await StartDealAsync(vehiclePrice: 20000m);

        await SetProductsAsync(deal, [(product, 800m, 400m)]);

        using (var withdrawn = await SendAsync(
            HttpMethod.Post, $"{Products}/{product}/available", Manager, new { available = false }))
        {
            withdrawn.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var after = await GetDealAsync(deal);

        after.GetProperty("products").EnumerateArray().Should().HaveCount(1,
            because: "withdrawal stops it being offered; it does not unsell it");
        after.GetProperty("amountDue").GetDecimal().Should().Be(20800m);
    }

    [Fact]
    public async Task The_same_product_cannot_be_sold_twice_on_one_deal()
    {
        var product = await AddProductAsync();
        var deal = await StartDealAsync(vehiclePrice: 20000m);

        using var refused = await SendAsync(
            HttpMethod.Post, $"/api/v1/deals/{deal}/products", Manager,
            new
            {
                products = new[]
                {
                    new { financeProductId = product, price = 100m, cost = 50m },
                    new { financeProductId = product, price = 100m, cost = 50m },
                },
            });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: "two warranties on one car is a mistake, not two sales");
    }

    [Fact]
    public async Task Products_cannot_be_changed_once_the_deal_is_submitted()
    {
        var product = await AddProductAsync();
        var deal = await StartDealAsync(vehiclePrice: 20000m);

        await PostAsync($"/api/v1/deals/{deal}/status", new { status = "Submitted", note = (string?)null });

        using var refused = await SendAsync(
            HttpMethod.Post, $"/api/v1/deals/{deal}/products", Manager,
            new { products = new[] { new { financeProductId = product, price = 100m, cost = 50m } } });

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict,
            because: "the figures a manager approved must not move underneath them");
    }

    [Fact]
    public async Task Delivering_posts_the_product_revenue_and_its_cost()
    {
        var product = await AddProductAsync(price: 1200m, cost: 700m);
        var deal = await StartDealAsync(vehiclePrice: 20000m);

        await SetProductsAsync(deal, [(product, 1200m, 700m)]);

        await PostAsync($"/api/v1/deals/{deal}/status", new { status = "Submitted", note = (string?)null });
        await PostAsync($"/api/v1/deals/{deal}/status", new { status = "Approved", note = (string?)null });
        await PostAsync($"/api/v1/deals/{deal}/status", new { status = "Delivered", note = (string?)null });

        using var entries = await SendAsync(
            HttpMethod.Get, $"/api/v1/accounting/journal?reference={deal}", Manager);

        var entryId = (await entries.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray().First().GetProperty("id").GetGuid();

        using var entry = await SendAsync(HttpMethod.Get, $"/api/v1/accounting/journal/{entryId}", Manager);
        var lines = (await entry.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("lines").EnumerateArray().ToList();

        // Its own revenue line and its own cost line, apart from the car's.
        lines.Should().Contain(l =>
            l.GetProperty("accountCode").GetString() == "4500"
            && l.GetProperty("credit").GetDecimal() == 1200m);

        lines.Should().Contain(l =>
            l.GetProperty("accountCode").GetString() == "5500"
            && l.GetProperty("debit").GetDecimal() == 700m);
    }

    // --- helpers -----------------------------------------------------------

    private async Task<Guid> AddProductAsync(decimal price = 500m, decimal cost = 250m)
    {
        using var response = await SendAsync(
            HttpMethod.Post, Products, Manager,
            new
            {
                name = $"Cover {Guid.NewGuid():N}",
                kind = "Warranty",
                provider = "Northgate Underwriting",
                defaultPrice = price,
                defaultCost = cost,
                currency = "USD",
                termMonths = 36,
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created, because: await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<Guid> StartDealAsync(decimal vehiclePrice)
    {
        var rooftop = await RooftopIdAsync();
        var customer = await FirstAsync("/api/v1/customers?query=a&limit=1", "id");

        // A car nobody else is holding. Deals hold their unit, so the suite's
        // shared stock runs out — book one in for this test rather than competing.
        var vehicle = await FirstAsync("/api/v1/vehicles?limit=1", "id");

        using var stocked = await SendAsync(HttpMethod.Post, "/api/v1/inventory", Manager, new
        {
            vehicleId = vehicle,
            rooftopId = rooftop,
            stockNumber = $"FI-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            costAmount = 15000m,
            costCurrency = "USD",
        });

        stocked.StatusCode.Should().Be(HttpStatusCode.Created, because: await stocked.Content.ReadAsStringAsync());
        var unit = (await stocked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await PostAsync($"/api/v1/inventory/{unit}/status", new { status = "Available", note = (string?)null });

        // Started by the salesperson, so the manager is free to approve it later.
        // A manager cannot sign off their own deal — that refusal is a feature,
        // and it caught this test's first arrangement.
        using var created = await SendAsync(HttpMethod.Post, "/api/v1/deals", Salesperson, new
        {
            rooftopId = rooftop,
            customerId = customer,
            inventoryUnitId = unit,
            currency = "USD",
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created, because: await created.Content.ReadAsStringAsync());
        var deal = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await PostAsync($"/api/v1/deals/{deal}/terms", new
        {
            charges = new[] { new { kind = "VehiclePrice", description = "Car", amount = vehiclePrice } },
        });

        return deal;
    }

    private async Task<JsonElement> SetProductsAsync(
        Guid deal,
        IReadOnlyList<(Guid Id, decimal Price, decimal Cost)> products)
    {
        using var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/deals/{deal}/products", Manager,
            new
            {
                products = products
                    .Select(p => new { financeProductId = p.Id, price = p.Price, cost = p.Cost })
                    .ToArray(),
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> GetDealAsync(Guid deal)
    {
        using var response = await SendAsync(HttpMethod.Get, $"/api/v1/deals/{deal}", Manager);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task PostAsync(string path, object body)
    {
        using var response = await SendAsync(HttpMethod.Post, path, Manager, body);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());
    }

    private async Task<Guid> FirstAsync(string path, string property)
    {
        using var response = await SendAsync(HttpMethod.Get, path, Manager);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray().First().GetProperty(property).GetGuid();
    }

    private async Task<Guid> RooftopIdAsync()
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/v1/organization", Manager);

        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("legalEntities").EnumerateArray()
            .SelectMany(e => e.GetProperty("rooftops").EnumerateArray())
            .Single(r => r.GetProperty("code").GetString() == "NAG-01")
            .GetProperty("id").GetGuid();
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, string email, object? body = null)
    {
        var session = await _fixture.SignInAsync(email, Tenant);

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
