// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ReceivablesReportingTests (integration) — proves the three things this adds
//   on top of the sub-ledger: an ageing report that buckets what is owed by how
//   overdue it is, a customer statement with a balance that actually runs
//   through the lines, and a credit limit that a delivery is refused against.
//
// Usage:
//   Runs with the normal test suite; needs a reachable SQL engine.
//
// Coding Instructions:
//   The credit-limit tests matter most here — see ReceivableService.OpenAsync.
//   A limit set after a customer already owes more than it allows must not
//   retroactively break anything; it only stops a NEW debt pushing them
//   further over.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class ReceivablesReportingTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string Deals = "/api/v1/deals";

    private const string Manager = DevelopmentSeeder.DevUsers.OrganizationWideEmail;
    private const string Advisor = DevelopmentSeeder.DevUsers.FirstRooftopOnlyEmail;

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task A_fresh_bill_ages_into_the_current_bucket()
    {
        var sale = await DeliverAsync(price: 5000m, cost: 3000m);

        using var response = await SendAsync(HttpMethod.Get, "/api/v1/receivables/ageing", Manager);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var report = await response.Content.ReadFromJsonAsync<JsonElement>();
        var row = report.GetProperty("customers").EnumerateArray()
            .Single(c => c.GetProperty("customerId").GetString() == sale.CustomerId);

        row.GetProperty("bucket").GetProperty("current").GetDecimal().Should().Be(5000m);
        row.GetProperty("bucket").GetProperty("over90").GetDecimal().Should().Be(0m);
        row.GetProperty("bucket").GetProperty("total").GetDecimal().Should().Be(5000m);
    }

    [Fact]
    public async Task Settling_a_bill_takes_it_out_of_the_ageing_report()
    {
        var sale = await DeliverAsync(price: 4000m, cost: 2500m);
        var receivableId = await ReceivableForAsync(sale.DealId);

        using var paid = await PostAsync($"/api/v1/receivables/{receivableId}/payments", Manager,
            new { amount = 4000m, method = "Cash" });
        paid.StatusCode.Should().Be(HttpStatusCode.OK);

        using var response = await SendAsync(HttpMethod.Get, "/api/v1/receivables/ageing", Manager);
        var report = await response.Content.ReadFromJsonAsync<JsonElement>();

        report.GetProperty("customers").EnumerateArray()
            .Any(c => c.GetProperty("customerId").GetString() == sale.CustomerId)
            .Should().BeFalse(because: "a settled bill owes nothing to age");
    }

    [Fact]
    public async Task A_statement_carries_a_running_balance_through_the_bill_and_the_payment()
    {
        var sale = await DeliverAsync(price: 6000m, cost: 4000m);
        var receivableId = await ReceivableForAsync(sale.DealId);

        using var paid = await PostAsync($"/api/v1/receivables/{receivableId}/payments", Manager,
            new { amount = 2500m, method = "Cash" });
        paid.StatusCode.Should().Be(HttpStatusCode.OK);

        var from = DateTimeOffset.UtcNow.AddDays(-1).ToString("O");
        var to = DateTimeOffset.UtcNow.AddDays(1).ToString("O");

        using var response = await SendAsync(
            HttpMethod.Get,
            $"/api/v1/receivables/statement/{sale.CustomerId}?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}",
            Manager);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var statement = await response.Content.ReadFromJsonAsync<JsonElement>();
        statement.GetProperty("openingBalance").GetDecimal().Should().Be(0m);
        statement.GetProperty("closingBalance").GetDecimal().Should().Be(3500m);

        var lines = statement.GetProperty("lines").EnumerateArray().ToList();
        lines.Should().HaveCount(2);
        lines[0].GetProperty("kind").GetString().Should().Be("Invoice");
        lines[0].GetProperty("balance").GetDecimal().Should().Be(6000m);
        lines[1].GetProperty("kind").GetString().Should().Be("Payment");
        lines[1].GetProperty("balance").GetDecimal().Should().Be(3500m);
    }

    [Fact]
    public async Task A_manager_can_set_a_customers_credit_limit()
    {
        var customerId = await CreatedIdAsync("/api/v1/customers", new
        {
            kind = "Person", firstName = "Credit", lastName = UniqueSurname(),
        });

        using var response = await PutAsync($"/api/v1/customers/{customerId}/credit-limit", Manager,
            new { limit = 10000m });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("creditLimit").GetDecimal().Should().Be(10000m);
    }

    [Fact]
    public async Task An_advisor_cannot_set_a_credit_limit()
    {
        var customerId = await CreatedIdAsync("/api/v1/customers", new
        {
            kind = "Person", firstName = "Credit", lastName = UniqueSurname(),
        });

        using var response = await PutAsync($"/api/v1/customers/{customerId}/credit-limit", Advisor,
            new { limit = 10000m });

        // Reading a customer and deciding what the dealership will let them owe
        // are different acts (see Permissions.CustomersManage) — an advisor
        // holds Customers.Read, not this.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_delivery_within_the_limit_still_succeeds()
    {
        var customerId = await CreatedIdAsync("/api/v1/customers", new
        {
            kind = "Person", firstName = "Credit", lastName = UniqueSurname(),
        });

        using var limited = await PutAsync($"/api/v1/customers/{customerId}/credit-limit", Manager,
            new { limit = 10000m });
        limited.StatusCode.Should().Be(HttpStatusCode.OK);

        var sale = await DeliverAsync(price: 8000m, cost: 6000m, buyer: customerId);

        using var bill = await SendAsync(HttpMethod.Get, await ReceivablePathAsync(sale.DealId), Manager);
        bill.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_delivery_that_would_exceed_the_limit_is_refused()
    {
        var customerId = await CreatedIdAsync("/api/v1/customers", new
        {
            kind = "Person", firstName = "Credit", lastName = UniqueSurname(),
        });

        using var limited = await PutAsync($"/api/v1/customers/{customerId}/credit-limit", Manager,
            new { limit = 5000m });
        limited.StatusCode.Should().Be(HttpStatusCode.OK);

        var failure = await DeliverFailingAsync(price: 8000m, cost: 6000m, buyer: customerId);

        failure.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await failure.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("receivables.credit_limit_exceeded");
    }

    [Fact]
    public async Task A_second_delivery_that_would_push_an_already_owing_customer_further_over_is_refused()
    {
        var customerId = await CreatedIdAsync("/api/v1/customers", new
        {
            kind = "Person", firstName = "Credit", lastName = UniqueSurname(),
        });

        // No limit yet: the first sale is unconstrained, exactly as it would be
        // for any customer who has never been given one.
        await DeliverAsync(price: 4000m, cost: 3000m, buyer: customerId);

        using var limited = await PutAsync($"/api/v1/customers/{customerId}/credit-limit", Manager,
            new { limit = 4500m });
        limited.StatusCode.Should().Be(HttpStatusCode.OK);

        // Already owes 4000; a further 4000 would put them at 8000, over the
        // 4500 cap that was set after the fact. The limit stops the NEXT debt,
        // not the one already on the books.
        var failure = await DeliverFailingAsync(price: 4000m, cost: 3000m, buyer: customerId);

        failure.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<Sale> DeliverAsync(decimal price, decimal cost, string? buyer = null)
    {
        var attempt = await AttemptDeliveryAsync(price, cost, buyer);
        attempt.Response.StatusCode.Should().Be(
            HttpStatusCode.OK, "an unconstrained delivery should not be refused");

        return new Sale(attempt.DealId, attempt.CustomerId);
    }

    /// <summary>
    /// Delivers a car for a customer who is expected to be refused — the
    /// caller inspects the response itself, so this hands it back unread
    /// rather than asserting success like <see cref="DeliverAsync"/> does.
    /// </summary>
    /// <remarks>
    /// CLEARS THE LIMIT BEFORE RETURNING. A refused delivery leaves its deal
    /// stuck at Approved in this test's real, persisted database — the same
    /// one demo-data reseeding reuses across the whole suite — and
    /// DemoData.DeliverApprovedAsync treats any Approved deal it cannot
    /// deliver as a fatal seeding defect, which would break every later test
    /// class's first request. Clearing the cap here makes that stuck deal
    /// deliverable again without weakening what this method proved: the
    /// refusal already happened and was asserted before this runs.
    /// </remarks>
    private async Task<HttpResponseMessage> DeliverFailingAsync(decimal price, decimal cost, string buyer)
    {
        var response = (await AttemptDeliveryAsync(price, cost, buyer)).Response;

        using var cleared = await PutAsync($"/api/v1/customers/{buyer}/credit-limit", Manager,
            new { limit = (decimal?)null });
        cleared.StatusCode.Should().Be(HttpStatusCode.OK);

        return response;
    }

    /// <summary>
    /// Builds one car and takes its deal through to the final status change,
    /// exactly like an ordinary sale, and hands back that change's response
    /// along with the ids a caller needs to look the receivable up.
    /// </summary>
    private async Task<(HttpResponseMessage Response, string DealId, string CustomerId)> AttemptDeliveryAsync(
        decimal price, decimal cost, string? buyer)
    {
        var rooftopId = await RooftopIdAsync("NAG-01");
        var suffix = $"{Guid.NewGuid():N}"[..8].ToUpperInvariant();

        var customerId = buyer ?? await CreatedIdAsync("/api/v1/customers", new
        {
            kind = "Person", firstName = "Credit", lastName = $"Case{suffix}",
        });

        var vehicleId = await CreatedIdAsync("/api/v1/vehicles", new
        {
            vin = $"CREDIT{suffix}"[..13], modelYear = 2021, make = "Toyota", model = "RAV4",
            vinExceptionReason = "Synthetic VIN for a credit-limit test.",
        });

        var unitId = await CreatedIdAsync("/api/v1/inventory", new
        {
            vehicleId, rooftopId, stockNumber = $"C{suffix}",
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

        using var terms = await PostAsync($"{Deals}/{dealId}/terms", Manager,
            new { charges = new[] { new { kind = "VehiclePrice", description = "The car", amount = price } } });
        terms.StatusCode.Should().Be(HttpStatusCode.OK);

        foreach (var status in new[] { "Submitted", "Approved" })
        {
            using var moved = await PostAsync($"{Deals}/{dealId}/status", Manager, new { status });
            moved.StatusCode.Should().Be(HttpStatusCode.OK, $"moving to {status} should succeed");
        }

        var delivered = await PostAsync($"{Deals}/{dealId}/status", Manager, new { status = "Delivered" });
        return (delivered, dealId, customerId);
    }

    private async Task<string> ReceivableForAsync(string dealId)
    {
        using var response = await SendAsync(
            HttpMethod.Get, $"/api/v1/receivables/for/Deal/{dealId}", Manager);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var receivable = await response.Content.ReadFromJsonAsync<JsonElement>();
        return receivable.GetProperty("id").GetString()!;
    }

    private async Task<string> ReceivablePathAsync(string dealId) =>
        $"/api/v1/receivables/{await ReceivableForAsync(dealId)}";

    private static string UniqueSurname() => $"Case{Guid.NewGuid():N}"[..12];

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

    private async Task<HttpResponseMessage> PutAsync(string path, string email, object body)
    {
        using var client = _fixture.CreateClient();
        var session = await _fixture.SignInAsync(email, Tenant);

        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri(path, UriKind.Relative))
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

    private sealed record Sale(string DealId, string CustomerId);
}
