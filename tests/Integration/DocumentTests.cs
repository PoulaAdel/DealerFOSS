// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   DocumentTests — the paperwork a customer is handed.
//
// Usage:
//   Runs with the normal test suite; needs a reachable SQL engine.
//
// Coding Instructions:
//   The test that matters most is
//   No_dealership_only_figure_reaches_a_customers_copy. It reads the raw HTML
//   rather than a model, because the risk is a field somebody adds later
//   without thinking about who sees the document — and a typed assertion can
//   only catch fields somebody remembered to declare. Keep it looking at the
//   wire.

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class DocumentTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string Manager = DevelopmentSeeder.DevUsers.OrganizationWideEmail;
    private const string Salesperson = DevelopmentSeeder.DevUsers.SalespersonEmail;
    private const string Advisor = DevelopmentSeeder.DevUsers.FirstRooftopOnlyEmail;

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task A_deal_summary_shows_the_car_the_charges_and_the_total()
    {
        var deal = await DealWithCoverAsync();
        var html = await DocumentAsync($"/api/v1/documents/deals/{deal}");

        html.Should().Contain("Vehicle order");
        html.Should().Contain("Due from the customer");
        html.Should().Contain("$20,900.00", because: "the total is the number the customer cares about");
        html.Should().Contain("3-year warranty", because: "they are paying for it, so it is itemised");
    }

    [Fact]
    public async Task No_dealership_only_figure_reaches_a_customers_copy()
    {
        // The cover is sold at 900 having cost 700, so the gross is 200. The price
        // must appear; neither of the other two may.
        var deal = await DealWithCoverAsync();
        var html = await DocumentAsync($"/api/v1/documents/deals/{deal}");

        html.Should().Contain("$900.00", because: "the customer is paying that");

        html.Should().NotContain("$700.00",
            because: "what the dealership paid the provider is not the customer's business");
        html.Should().NotContain("$200.00",
            because: "the gross on the product is not the customer's business");

        foreach (var word in new[] { "gross", "Gross", "cost", "Cost" })
        {
            html.Should().NotContain(word,
                because: $"'{word}' has no place on something handed across a desk");
        }
    }

    [Fact]
    public async Task The_document_is_a_complete_standalone_page()
    {
        // It has to survive being saved and opened next year, so nothing may be
        // fetched at open time.
        var deal = await DealWithCoverAsync();
        var html = await DocumentAsync($"/api/v1/documents/deals/{deal}");

        html.Should().StartWith("<!doctype html>");
        html.Should().Contain("@page", because: "it is meant to be printed on paper");
        html.Should().NotContain("<script", because: "a saved document must not run anything");
        html.Should().NotContain("<link ", because: "a linked stylesheet would not be there later");
    }

    [Fact]
    public async Task Values_are_escaped_rather_than_pasted_in()
    {
        var customer = await CreateCustomerAsync("Bob & Sons <Motors>");
        var deal = await DealWithCoverAsync(customer);

        var html = await DocumentAsync($"/api/v1/documents/deals/{deal}");

        html.Should().Contain("Bob &amp; Sons &lt;Motors&gt;");
        html.Should().NotContain("<Motors>", because: "an unescaped value produces a broken document");
    }

    [Fact]
    public async Task A_trade_in_is_shown_as_reducing_what_is_owed()
    {
        // Same rule as the deal desk: a column somebody reads down has to reach
        // the total printed under it.
        var deal = await DealWithCoverAsync(tradeAllowance: 3000m);
        var html = await DocumentAsync($"/api/v1/documents/deals/{deal}");

        html.Should().Contain("-$3,000.00");
        html.Should().Contain("$17,900.00", because: "20,900 less a 3,000 trade-in");
    }

    [Fact]
    public async Task The_printed_order_adds_up_to_its_own_total()
    {
        // The one test in this file that is not about a particular row, and the
        // reason it exists: FOUR separate lines have been inside AmountDue and
        // missing from the column printed above it — the trade-in, the F&I
        // products, the tax on the deal desk, and the tax on this document,
        // which was found on 2026-09-21 by reading a real order in a browser
        // and adding it up by hand. Each of the first three was fixed by
        // printing that one line. This asserts the property instead, so the
        // fifth cannot be quiet.
        //
        // Every component at once, because the defect has always been one
        // component among several rather than an empty document.
        var deal = await DealWithCoverAsync(tradeAllowance: 3000m, tax: 1650m);
        var html = await DocumentAsync($"/api/v1/documents/deals/{deal}");

        var (amounts, total) = ColumnOf(html);

        amounts.Should().HaveCountGreaterThanOrEqualTo(4,
            because: "a car, a warranty, a trade-in and tax were all recorded on this deal");

        amounts.Sum().Should().Be(total,
            because: "a customer reads the column down and expects to arrive at the figure they are asked to pay");

        // And the row says what the tax was worked out on, since an amount with
        // no basis and no rate cannot be queried by the person paying it.
        html.Should().Contain("$20,000.00 at 8.25%");
    }

    [Fact]
    public async Task Somebody_who_cannot_read_the_deal_cannot_print_it()
    {
        // No permission is checked in the document endpoint — it reads through
        // IDeals, which already applies scope. This test is what proves that is
        // enough rather than an oversight.
        //
        // The unassigned user, not the advisor: the advisor holds read access at
        // NAG-01 and can legitimately read a NAG-01 deal, which this test
        // discovered by expecting the wrong refusal.
        var deal = await DealWithCoverAsync();

        using var response = await SendAsync(
            $"/api/v1/documents/deals/{deal}", DevelopmentSeeder.DevUsers.UnassignedEmail);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_service_invoice_shows_the_work_and_the_split_totals()
    {
        var job = await InvoicedJobAsync();
        var html = await DocumentAsync($"/api/v1/documents/repair-orders/{job}");

        html.Should().Contain("Service invoice");
        html.Should().Contain("Labour");
        html.Should().Contain("Parts");
        html.Should().Contain("Total due");
    }

    [Fact]
    public async Task Declined_work_is_printed_at_nothing_rather_than_hidden()
    {
        // A customer who said no in March and returns in September with the same
        // fault should be able to see they were told.
        var job = await InvoicedJobAsync(declineExtra: true);
        var html = await DocumentAsync($"/api/v1/documents/repair-orders/{job}");

        html.Should().Contain("Replace the discs");
        html.Should().Contain("declined");
    }

    [Fact]
    public async Task An_uninvoiced_job_says_it_is_not_a_bill()
    {
        var job = await OpenJobAsync();
        var html = await DocumentAsync($"/api/v1/documents/repair-orders/{job}");

        html.Should().Contain("Job sheet");
        html.Should().Contain("Not yet invoiced");
        html.Should().NotContain("Service invoice");
    }

    [Fact]
    public async Task A_part_cost_never_reaches_the_service_invoice()
    {
        var job = await InvoicedJobAsync();
        var html = await DocumentAsync($"/api/v1/documents/repair-orders/{job}");

        foreach (var word in new[] { "cost", "Cost", "gross", "Gross" })
        {
            html.Should().NotContain(word);
        }
    }

    // --- helpers -----------------------------------------------------------

    private async Task<string> DocumentAsync(string path)
    {
        using var response = await SendAsync(path, Manager);

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");

        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Every amount in the body of a money table, and the total printed under
    /// it. Read out of the rendered HTML rather than a model on purpose: the
    /// defect this guards against is a figure that exists server-side and never
    /// reaches the page, which a typed assertion cannot see.
    /// </summary>
    private static (IReadOnlyList<decimal> Amounts, decimal Total) ColumnOf(string html)
    {
        // The total carries both classes, so it is matched first and removed —
        // otherwise it would be counted as one of the lines as well and every
        // document would appear to be exactly double.
        var totalMatch = Regex.Match(html, "<td class=\"num total\">([^<]*)</td>");
        totalMatch.Success.Should().BeTrue(because: "a money document prints a total");

        var body = html.Replace(totalMatch.Value, string.Empty, StringComparison.Ordinal);

        var amounts = Regex.Matches(body, "<td class=\"num\">([^<]*)</td>")
            .Select(m => Money(m.Groups[1].Value))
            .Where(v => v is not null)
            .Select(v => v!.Value)
            .ToList();

        return (amounts, Money(totalMatch.Groups[1].Value) ?? 0m);

        // "declined" is printed where an amount would go, and is not one.
        static decimal? Money(string text) =>
            decimal.TryParse(
                text.Replace("$", string.Empty, StringComparison.Ordinal)
                    .Replace(",", string.Empty, StringComparison.Ordinal),
                NumberStyles.Currency,
                CultureInfo.InvariantCulture,
                out var value)
                ? value
                : null;
    }

    private async Task<Guid> DealWithCoverAsync(
        Guid? customerId = null,
        decimal tradeAllowance = 0m,
        decimal tax = 0m)
    {
        var rooftop = await RooftopIdAsync();
        var customer = customerId ?? await FirstAsync("/api/v1/customers?query=a&limit=1", "id");
        var unit = await StockACarAsync(rooftop, "DOC");

        var product = await PostAsync<JsonElement>("/api/v1/finance/products", Manager, new
        {
            name = $"3-year warranty {Guid.NewGuid():N}",
            kind = "Warranty",
            provider = "Northgate Underwriting",
            defaultPrice = 1200m,
            defaultCost = 700m,
            currency = "USD",
            termMonths = 36,
        }, HttpStatusCode.Created);

        var deal = await PostAsync<JsonElement>("/api/v1/deals", Salesperson, new
        {
            rooftopId = rooftop, customerId = customer, inventoryUnitId = unit, currency = "USD",
        }, HttpStatusCode.Created);

        var dealId = deal.GetProperty("id").GetGuid();

        await PostAsync<JsonElement>($"/api/v1/deals/{dealId}/terms", Manager, new
        {
            charges = new[] { new { kind = "VehiclePrice", description = "The car", amount = 20000m } },
            tradeIn = tradeAllowance == 0m
                ? null
                : (object)new { description = "2014 Honda Civic", allowance = tradeAllowance, payoff = 0m },
        }, HttpStatusCode.OK);

        // Sold at a discount: the recorded figures follow what was agreed.
        await PostAsync<JsonElement>($"/api/v1/deals/{dealId}/products", Manager, new
        {
            products = new[]
            {
                new { financeProductId = product.GetProperty("id").GetGuid(), price = 900m, cost = 700m },
            },
        }, HttpStatusCode.OK);

        if (tax != 0m)
        {
            // A rate against the car's price, so the row can be checked twice
            // over: the amount itself, and that the basis and rate printed
            // beside it are the ones the deal actually carries.
            await PostAsync<JsonElement>($"/api/v1/deals/{dealId}/tax", Manager, new
            {
                lines = new[]
                {
                    new
                    {
                        description = "Sales tax",
                        jurisdiction = "WA / King / Seattle",
                        basis = 20000m,
                        rate = tax / 20000m,
                        amount = tax,
                        provenance = "EnteredByPerson",
                    },
                },
                taxedAt = new { administrativeArea = "WA", county = "King", postalCode = "98101", country = "US" },
            }, HttpStatusCode.OK);
        }

        return dealId;
    }

    private async Task<Guid> OpenJobAsync()
    {
        var job = await PostAsync<JsonElement>("/api/v1/repair-orders", Manager, new
        {
            rooftopId = await RooftopIdAsync(),
            customerId = await FirstAsync("/api/v1/customers?query=a&limit=1", "id"),
            vehicleId = await FirstAsync("/api/v1/vehicles?limit=1", "id"),
            complaint = "Grinding at the front.",
            currency = "USD",
            odometerReading = 64000,
        }, HttpStatusCode.Created);

        var jobId = job.GetProperty("id").GetGuid();

        await PostAsync<JsonElement>($"/api/v1/repair-orders/{jobId}/lines", Manager, new
        {
            kind = "Labour", description = "Investigate the noise", hours = 1.5m, rate = 120m,
        }, HttpStatusCode.OK);

        return jobId;
    }

    private async Task<Guid> InvoicedJobAsync(bool declineExtra = false)
    {
        var jobId = await OpenJobAsync();

        await PostAsync<JsonElement>($"/api/v1/repair-orders/{jobId}/status", Manager,
            new { status = "InProgress", note = (string?)null }, HttpStatusCode.OK);

        if (declineExtra)
        {
            var withExtra = await PostAsync<JsonElement>($"/api/v1/repair-orders/{jobId}/lines", Manager, new
            {
                kind = "Part", description = "Replace the discs", unitAmount = 340m,
            }, HttpStatusCode.OK);

            var lineId = withExtra.GetProperty("lines").EnumerateArray()
                .Single(l => l.GetProperty("description").GetString() == "Replace the discs")
                .GetProperty("id").GetGuid();

            await PostAsync<JsonElement>($"/api/v1/repair-orders/{jobId}/lines/{lineId}/answer", Manager,
                new { approved = false, note = "Phoned; will think about it." }, HttpStatusCode.OK);
        }

        await PostAsync<JsonElement>($"/api/v1/repair-orders/{jobId}/status", Manager,
            new { status = "Completed", note = (string?)null }, HttpStatusCode.OK);

        await PostAsync<JsonElement>($"/api/v1/repair-orders/{jobId}/status", Manager,
            new { status = "Invoiced", note = (string?)null }, HttpStatusCode.OK);

        return jobId;
    }

    private async Task<Guid> CreateCustomerAsync(string lastName)
    {
        var created = await PostAsync<JsonElement>("/api/v1/customers", Manager, new
        {
            kind = "Business", firstName = string.Empty, lastName,
        }, HttpStatusCode.Created);

        return created.GetProperty("id").GetGuid();
    }

    private async Task<Guid> StockACarAsync(Guid rooftop, string prefix)
    {
        var unit = await PostAsync<JsonElement>("/api/v1/inventory", Manager, new
        {
            vehicleId = await FirstAsync("/api/v1/vehicles?limit=1", "id"),
            rooftopId = rooftop,
            stockNumber = $"{prefix}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            costAmount = 15000m,
            costCurrency = "USD",
        }, HttpStatusCode.Created);

        var unitId = unit.GetProperty("id").GetGuid();

        await PostAsync<JsonElement>($"/api/v1/inventory/{unitId}/status", Manager,
            new { status = "Available", note = (string?)null }, HttpStatusCode.OK);

        return unitId;
    }

    private async Task<T> PostAsync<T>(string path, string email, object body, HttpStatusCode expected)
    {
        var session = await _fixture.SignInAsync(email, Tenant);

        using var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative));
        request.Headers.Add("X-Tenant", Tenant);
        request.Headers.Add("Cookie", $"dfoss_session={session.SessionToken}");
        request.Headers.Add("X-CSRF-Token", session.AntiForgeryToken);
        request.Content = JsonContent.Create(body);

        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(expected, because: await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<Guid> FirstAsync(string path, string property)
    {
        using var response = await SendAsync(path, Manager);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .Rows()[0].GetProperty(property).GetGuid();
    }

    private async Task<Guid> RooftopIdAsync()
    {
        using var response = await SendAsync("/api/v1/organization", Manager);

        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("legalEntities").EnumerateArray()
            .SelectMany(e => e.GetProperty("rooftops").EnumerateArray())
            .Single(r => r.GetProperty("code").GetString() == "NAG-01")
            .GetProperty("id").GetGuid();
    }

    private async Task<HttpResponseMessage> SendAsync(string path, string email)
    {
        var session = await _fixture.SignInAsync(email, Tenant);

        using var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        request.Headers.Add("X-Tenant", Tenant);
        request.Headers.Add("Cookie", $"dfoss_session={session.SessionToken}");

        return await client.SendAsync(request);
    }
}
