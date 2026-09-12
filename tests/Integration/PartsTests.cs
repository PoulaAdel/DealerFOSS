// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   PartsTests — parts as real stock, and the service profit figure that depends
//   on it.
//
// Usage:
//   Runs with the normal test suite; needs a reachable SQL engine.
//
// Coding Instructions:
//   The test that matters most is
//   Invoicing_a_job_relieves_stock_and_posts_what_the_parts_cost. Before
//   parts existed, invoicing recorded revenue and no cost, so the workshop
//   had no profit figure at all. That test is the whole justification for
//   this capability — if it ever goes green for the wrong reason, the books
//   are lying about what service earned.
//
//   Tests here add their own parts with generated numbers rather than
//   sharing a fixture. Stock is consumed by the tests that sell it, and a
//   shared part would make every test depend on the order they ran in.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class PartsTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string PartsApi = "/api/v1/parts";

    private const string Manager = DevelopmentSeeder.DevUsers.OrganizationWideEmail;
    private const string Advisor = DevelopmentSeeder.DevUsers.FirstRooftopOnlyEmail;

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task A_part_can_be_added_and_stock_booked_in_against_it()
    {
        var part = await AddPartAsync();
        await ReceiveAsync(part, quantity: 10, unitCost: 5.00m);

        using var response = await SendAsync(HttpMethod.Get, $"{PartsApi}/{part}", Manager);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        var shelf = detail.GetProperty("stock").EnumerateArray().Single();

        shelf.GetProperty("quantityOnHand").GetDecimal().Should().Be(10m);
        shelf.GetProperty("unitCost").GetDecimal().Should().Be(5.00m);
    }

    [Fact]
    public async Task A_part_with_no_stock_still_appears_so_it_can_be_stocked()
    {
        // Found by adding a part on the screen: the list only emitted a row per
        // shelf that HAD stock, so a new part was invisible — and a part nobody
        // can open is a part nobody can book a delivery onto. A closed loop with
        // no way in.
        var part = await AddPartAsync();

        using var response = await SendAsync(HttpMethod.Get, PartsApi, Manager);
        var listed = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .Rows()
            .SingleOrDefault(p => p.GetProperty("id").GetGuid() == part);

        listed.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            because: "a part with no stock still has to be reachable");
        listed.GetProperty("rooftopId").ValueKind.Should().Be(JsonValueKind.Null,
            because: "it is on no shelf, and naming one would be a lie");
        listed.GetProperty("quantityOnHand").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task Asking_only_for_stocked_parts_leaves_the_unstocked_ones_out()
    {
        var part = await AddPartAsync();

        using var response = await SendAsync(HttpMethod.Get, $"{PartsApi}?inStockOnly=true", Manager);
        (await response.Content.ReadFromJsonAsync<JsonElement>())
            .Rows()
            .Should().NotContain(p => p.GetProperty("id").GetGuid() == part);
    }

    [Fact]
    public async Task The_same_part_number_cannot_be_catalogued_twice()
    {
        var number = $"PN{Guid.NewGuid():N}"[..14];
        await AddPartAsync(number);

        // Typed differently, meaning the same thing. Two rows for one component is
        // what makes a parts department stop trusting the figures.
        using var again = await SendAsync(
            HttpMethod.Post, PartsApi, Manager,
            new { partNumber = number.ToLowerInvariant(), description = "Same thing, typed differently" });

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_rooftop_scoped_user_cannot_add_to_the_catalogue()
    {
        // The number means the same thing at every location, so one lot must not
        // be able to define it.
        using var response = await SendAsync(
            HttpMethod.Post, PartsApi, Advisor,
            new { partNumber = $"X{Guid.NewGuid():N}"[..12], description = "Should be refused" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Invoicing_a_job_relieves_stock_and_posts_what_the_parts_cost()
    {
        // The reason this capability exists. Before it, invoicing recorded revenue
        // and no cost, so "what did the workshop make" had no answer.
        var part = await AddPartAsync();
        await ReceiveAsync(part, quantity: 10, unitCost: 20.00m);

        var job = await OpenJobAsync();
        await AddPartLineAsync(job, part, quantity: 2, sellFor: 90.00m);
        await MoveAsync(job, "InProgress");
        await MoveAsync(job, "Completed");
        await MoveAsync(job, "Invoiced");

        // Two came off the shelf.
        using (var after = await SendAsync(HttpMethod.Get, $"{PartsApi}/{part}", Manager))
        {
            var shelf = (await after.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("stock").EnumerateArray().Single();

            shelf.GetProperty("quantityOnHand").GetDecimal().Should().Be(8m);
        }

        // And the cost is frozen on the line, not recomputed on read.
        using var detail = await SendAsync(HttpMethod.Get, $"/api/v1/repair-orders/{job}", Manager);
        var line = (await detail.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("lines").EnumerateArray()
            .Single(l => l.GetProperty("partId").ValueKind != JsonValueKind.Null);

        line.GetProperty("cost").GetDecimal().Should().Be(40.00m, because: "2 at 20.00 each");
    }

    [Fact]
    public async Task A_job_cannot_be_invoiced_for_parts_that_are_not_on_the_shelf()
    {
        // Refusing is what sends somebody to book the delivery in. Going negative
        // would leave the stock figure meaningless and nobody would notice.
        var part = await AddPartAsync();
        await ReceiveAsync(part, quantity: 1, unitCost: 12.00m);

        var job = await OpenJobAsync();
        await AddPartLineAsync(job, part, quantity: 5, sellFor: 50.00m);
        await MoveAsync(job, "InProgress");
        await MoveAsync(job, "Completed");

        using var invoice = await SendAsync(
            HttpMethod.Post, $"/api/v1/repair-orders/{job}/status", Manager,
            new { status = "Invoiced", note = (string?)null });

        invoice.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await invoice.Content.ReadAsStringAsync())
            .Should().Contain("Book the delivery in", because: "the refusal should say what to do about it");

        // And nothing moved: the refusal rolled the whole invoice back.
        using var stock = await SendAsync(HttpMethod.Get, $"{PartsApi}/{part}", Manager);
        (await stock.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("stock").EnumerateArray().Single()
            .GetProperty("quantityOnHand").GetDecimal()
            .Should().Be(1m);
    }

    [Fact]
    public async Task A_hand_typed_part_still_bills_and_simply_has_no_cost()
    {
        // A one-off item bought for a single job never enters the catalogue, and
        // it still has to be billable. What it must not do is invent a cost.
        var job = await OpenJobAsync();

        using (var added = await SendAsync(
            HttpMethod.Post, $"/api/v1/repair-orders/{job}/lines", Manager,
            new { kind = "Part", description = "One-off bracket from the factor", unitAmount = 35.00m }))
        {
            added.StatusCode.Should().Be(HttpStatusCode.OK, because: await added.Content.ReadAsStringAsync());
        }

        await MoveAsync(job, "InProgress");
        await MoveAsync(job, "Completed");
        await MoveAsync(job, "Invoiced");

        using var detail = await SendAsync(HttpMethod.Get, $"/api/v1/repair-orders/{job}", Manager);
        var line = (await detail.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("lines").EnumerateArray().Single();

        line.GetProperty("partId").ValueKind.Should().Be(JsonValueKind.Null);
        line.GetProperty("cost").ValueKind.Should().Be(JsonValueKind.Null,
            because: "no cost is honest; a zero would read as free");
        line.GetProperty("amount").GetDecimal().Should().Be(35.00m);
    }

    [Fact]
    public async Task Changing_the_costing_method_is_a_group_decision()
    {
        using var refused = await SendAsync(
            HttpMethod.Post, $"{PartsApi}/costing", Advisor, new { method = "Fifo" });

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_costing_method_can_be_changed_and_offers_its_choices()
    {
        try
        {
            using var set = await SendAsync(
                HttpMethod.Post, $"{PartsApi}/costing", Manager, new { method = "Fifo" });

            set.StatusCode.Should().Be(HttpStatusCode.OK, because: await set.Content.ReadAsStringAsync());

            var payload = await set.Content.ReadFromJsonAsync<JsonElement>();
            payload.GetProperty("method").GetString().Should().Be("Fifo");

            // The options come from the server so a screen never keeps its own
            // copy of the list.
            payload.GetProperty("options").EnumerateArray()
                .Select(o => o.GetProperty("method").GetString())
                .Should().BeEquivalentTo(["MovingAverage", "LastCost", "Fifo"]);
        }
        finally
        {
            await SendAsync(HttpMethod.Post, $"{PartsApi}/costing", Manager, new { method = "MovingAverage" });
        }
    }

    [Fact]
    public async Task The_method_changes_what_a_future_sale_costs_and_leaves_past_ones_alone()
    {
        // Two deliveries at different prices, so the methods disagree: moving
        // average is 7.00, FIFO's next one out is 5.00.
        var part = await AddPartAsync();
        await ReceiveAsync(part, quantity: 10, unitCost: 5.00m);
        await ReceiveAsync(part, quantity: 10, unitCost: 9.00m);

        var first = await InvoiceOnePartAsync(part);
        first.Should().Be(7.00m, because: "moving average is the default: (50 + 90) / 20");

        try
        {
            using (var set = await SendAsync(
                HttpMethod.Post, $"{PartsApi}/costing", Manager, new { method = "Fifo" }))
            {
                set.StatusCode.Should().Be(HttpStatusCode.OK);
            }

            var second = await InvoiceOnePartAsync(part);
            second.Should().Be(5.00m, because: "FIFO takes the next one out of the oldest layer");
        }
        finally
        {
            await SendAsync(HttpMethod.Post, $"{PartsApi}/costing", Manager, new { method = "MovingAverage" });
        }

        // The first job's cost is untouched. Switching method must never rewrite
        // a figure somebody has already reported on.
        first.Should().Be(7.00m);
    }

    // --- helpers -----------------------------------------------------------

    /// <summary>Invoices a job selling one of the part, and reports what it cost.</summary>
    private async Task<decimal> InvoiceOnePartAsync(Guid part)
    {
        var job = await OpenJobAsync();
        await AddPartLineAsync(job, part, quantity: 1, sellFor: 40.00m);
        await MoveAsync(job, "InProgress");
        await MoveAsync(job, "Completed");
        await MoveAsync(job, "Invoiced");

        using var detail = await SendAsync(HttpMethod.Get, $"/api/v1/repair-orders/{job}", Manager);

        return (await detail.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("lines").EnumerateArray()
            .Single(l => l.GetProperty("partId").ValueKind != JsonValueKind.Null)
            .GetProperty("cost").GetDecimal();
    }

    private async Task<Guid> AddPartAsync(string? number = null)
    {
        using var response = await SendAsync(
            HttpMethod.Post, PartsApi, Manager,
            new
            {
                partNumber = number ?? $"PN{Guid.NewGuid():N}"[..14],
                description = "Front brake pad set",
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            because: await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task ReceiveAsync(Guid part, decimal quantity, decimal unitCost)
    {
        using var response = await SendAsync(
            HttpMethod.Post, $"{PartsApi}/{part}/receipts", Manager,
            new { quantity, unitCost, rooftopId = await RooftopIdAsync(), reference = "DN-1" });

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());
    }

    private async Task<Guid> OpenJobAsync()
    {
        var customer = await FirstAsync("/api/v1/customers?query=a&limit=1", "id");
        var vehicle = await FirstAsync("/api/v1/vehicles?limit=1", "id");

        using var response = await SendAsync(
            HttpMethod.Post, "/api/v1/repair-orders", Manager,
            new
            {
                rooftopId = await RooftopIdAsync(),
                customerId = customer,
                vehicleId = vehicle,
                complaint = "Brakes",
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            because: await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task AddPartLineAsync(Guid job, Guid part, decimal quantity, decimal sellFor)
    {
        using var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/repair-orders/{job}/lines", Manager,
            new
            {
                kind = "Part",
                description = "Front brake pad set",
                unitAmount = sellFor,
                partId = part,
                partQuantity = quantity,
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());
    }

    private async Task MoveAsync(Guid job, string status)
    {
        using var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/repair-orders/{job}/status", Manager,
            new { status, note = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());
    }

    private async Task<Guid> FirstAsync(string path, string property)
    {
        using var response = await SendAsync(HttpMethod.Get, path, Manager);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .Rows()[0].GetProperty(property).GetGuid();
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
