// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   LedgerTests (integration) — proves that delivering a car writes a balanced
//   entry, that it cannot be written twice, and that a mistake is corrected by
//   reversal rather than by editing.
//
// Usage:
//   Runs with the normal test suite; needs a reachable SQL engine.
//
// Coding Instructions:
//   The arithmetic assertion is the valuable one. If the posting map in
//   AccountingService drifts, this is what catches it.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class LedgerTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string Journal = "/api/v1/accounting/journal";
    private const string Deals = "/api/v1/deals";

    private const string Manager = DevelopmentSeeder.DevUsers.OrganizationWideEmail;
    private const string Advisor = DevelopmentSeeder.DevUsers.FirstRooftopOnlyEmail;
    private const string Sales = DevelopmentSeeder.DevUsers.SalespersonEmail;

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task The_chart_of_accounts_is_there_to_label_the_numbers()
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/v1/accounting/accounts", Manager);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var accounts = await response.Content.ReadFromJsonAsync<JsonElement>();
        var codes = accounts.EnumerateArray().Select(a => a.GetProperty("code").GetString()).ToList();

        codes.Should().Contain(["1000", "1300", "4000", "5000"]);
    }

    [Fact]
    public async Task Delivering_a_car_posts_a_balanced_entry_with_the_right_numbers()
    {
        // 24,000 car + 400 fee - 300 discount = 24,100 subtotal
        // less a 3,000 trade allowance, plus 1,000 still owed on it = 22,100 due
        var sale = await DeliverAsync(price: 24000m, fee: 400m, discount: -300m,
            tradeAllowance: 3000m, tradePayoff: 1000m, cost: 19000m);

        var entry = await EntryForAsync(sale.DealId);

        entry.GetProperty("totalDebits").GetDecimal().Should()
            .Be(entry.GetProperty("totalCredits").GetDecimal(), because: "a ledger entry balances or it is not one");

        // Summed, not single: one account legitimately appears on both sides of
        // an entry. Cash comes in from the customer and goes back out to settle
        // what they still owed on the trade, and showing both is more useful than
        // netting them into one line.
        decimal Debit(string code) => SumFor(entry, code, "debit");
        decimal Credit(string code) => SumFor(entry, code, "credit");

        Debit("1000").Should().Be(22100m, because: "that is what the customer pays");
        Debit("1310").Should().Be(3000m, because: "we now own their old car at the allowance");
        Debit("4900").Should().Be(300m, because: "a discount is a debit against revenue");
        Credit("4000").Should().Be(24000m);
        Credit("4100").Should().Be(400m);
        Credit("1000").Should().Be(1000m, because: "we settle what they still owed on the trade");
        Debit("5000").Should().Be(19000m, because: "the car cost that, and gross profit needs it");
        Credit("1300").Should().Be(19000m, because: "the car is off the lot");
    }

    [Fact]
    public async Task A_delivery_is_never_posted_twice()
    {
        var sale = await DeliverAsync();

        var entries = await ListAsync($"{Journal}?reference={sale.DealId}", Manager);

        entries.Should().ContainSingle(because: "posting a sale twice would double the revenue");
    }

    [Fact]
    public async Task A_mistake_is_corrected_by_reversal_and_the_original_stays()
    {
        var sale = await DeliverAsync(price: 20000m);
        var entry = await EntryForAsync(sale.DealId);
        var entryId = entry.GetProperty("id").GetString()!;

        using var reversed = await PostAsync($"{Journal}/{entryId}/reverse", Manager,
            new { reason = "Wrong car delivered." });
        reversed.StatusCode.Should().Be(HttpStatusCode.OK);

        var reversal = await reversed.Content.ReadFromJsonAsync<JsonElement>();
        reversal.GetProperty("reversesEntryId").GetString().Should().Be(entryId);
        reversal.GetProperty("source").GetString().Should().Be("Reversal");

        // Every side is swapped, and it still balances.
        SumFor(reversal, "1000", "credit").Should().BeGreaterThan(0m);
        reversal.GetProperty("totalDebits").GetDecimal().Should()
            .Be(reversal.GetProperty("totalCredits").GetDecimal());

        // The original is exactly as it was.
        using var originalNow = await SendAsync(HttpMethod.Get, $"{Journal}/{entryId}", Manager);
        var original = await originalNow.Content.ReadFromJsonAsync<JsonElement>();
        SumFor(original, "1000", "debit").Should().BeGreaterThan(0m);
    }

    [Fact]
    public async Task The_same_entry_cannot_be_reversed_twice()
    {
        var sale = await DeliverAsync();
        var entryId = (await EntryForAsync(sale.DealId)).GetProperty("id").GetString()!;

        using var first = await PostAsync($"{Journal}/{entryId}/reverse", Manager, new { reason = "Once." });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        using var again = await PostAsync($"{Journal}/{entryId}/reverse", Manager, new { reason = "Twice." });
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_reversal_needs_a_reason()
    {
        var sale = await DeliverAsync();
        var entryId = (await EntryForAsync(sale.DealId)).GetProperty("id").GetString()!;

        using var response = await PostAsync($"{Journal}/{entryId}/reverse", Manager, new { reason = "   " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_trial_balance_totals_the_entries_and_proves_it_balances()
    {
        await DeliverAsync(price: 24000m, fee: 400m, discount: -300m,
            tradeAllowance: 3000m, tradePayoff: 1000m, cost: 19000m);

        using var response = await SendAsync(HttpMethod.Get, "/api/v1/accounting/balances", Manager);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var balance = await response.Content.ReadFromJsonAsync<JsonElement>();

        balance.GetProperty("balances").GetBoolean().Should().BeTrue(
            because: "a trial balance that does not balance means something was lost");
        balance.GetProperty("totalDebits").GetDecimal().Should()
            .Be(balance.GetProperty("totalCredits").GetDecimal());

        // Revenue is stated on its normal side, so a credit balance reads positive.
        var sales = AccountIn(balance, "4000");
        sales.GetProperty("credits").GetDecimal().Should().BeGreaterThanOrEqualTo(24000m);
        sales.GetProperty("balance").GetDecimal().Should().BeGreaterThan(0m);

        // An asset does the same on the debit side.
        var cost = AccountIn(balance, "5000");
        cost.GetProperty("kind").GetString().Should().Be("Expense");
        cost.GetProperty("balance").GetDecimal().Should().BeGreaterThan(0m);
    }

    [Fact]
    public async Task A_period_with_nothing_in_it_balances_at_zero()
    {
        using var response = await SendAsync(
            HttpMethod.Get, "/api/v1/accounting/balances?from=2000-01-01&to=2000-01-31", Manager);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var balance = await response.Content.ReadFromJsonAsync<JsonElement>();
        balance.GetProperty("totalDebits").GetDecimal().Should().Be(0m);
        balance.GetProperty("balances").GetBoolean().Should().BeTrue();
        balance.GetProperty("accounts").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task A_rooftop_scoped_user_is_refused_another_rooftops_balances()
    {
        using var response = await SendAsync(
            HttpMethod.Get, $"/api/v1/accounting/balances?rooftopId={await RooftopIdAsync("NAG-02")}", Advisor);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "a total is as revealing as the entries behind it");
    }

    [Fact]
    public async Task An_advisor_can_read_the_ledger_but_cannot_reverse_anything()
    {
        var sale = await DeliverAsync();
        var entryId = (await EntryForAsync(sale.DealId)).GetProperty("id").GetString()!;

        using var read = await SendAsync(HttpMethod.Get, Journal, Advisor);
        read.StatusCode.Should().Be(HttpStatusCode.OK);

        using var reverse = await PostAsync($"{Journal}/{entryId}/reverse", Advisor, new { reason = "No." });
        reverse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Posting_an_entry_and_undoing_one_are_different_rights()
    {
        var sale = await DeliverAsync();
        var entryId = (await EntryForAsync(sale.DealId)).GetProperty("id").GetString()!;

        // The salesperson delivered that car, which posted this entry — so they
        // demonstrably hold Accounting.Post. Reversing it is a separate right they
        // do not hold, and that separation is the point: reversing is the one
        // ledger operation that can make a mistake disappear.
        using var reverse = await PostAsync(
            $"{Journal}/{entryId}/reverse", Sales, new { reason = "Undo my own posting." });

        reverse.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "whoever finishes a sale is not automatically whoever may unwind its entry");

        using var manager = await PostAsync(
            $"{Journal}/{entryId}/reverse", Manager, new { reason = "Posted in error." });

        manager.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "somebody must still be able to correct it");
    }

    // --- helpers -----------------------------------------------------------

    private sealed record Sale(string DealId);

    private static JsonElement AccountIn(JsonElement balance, string code) =>
        balance.GetProperty("accounts").EnumerateArray()
            .Single(a => a.GetProperty("code").GetString() == code);

    private static decimal SumFor(JsonElement entry, string code, string side) =>
        entry.GetProperty("lines").EnumerateArray()
            .Where(l => l.GetProperty("accountCode").GetString() == code)
            .Sum(l => l.GetProperty(side).GetDecimal());

    private async Task<JsonElement> EntryForAsync(string dealId)
    {
        using var response = await SendAsync(HttpMethod.Get, $"{Journal}?reference={dealId}", Manager);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var summaries = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = summaries.EnumerateArray().First().GetProperty("id").GetString()!;

        using var detail = await SendAsync(HttpMethod.Get, $"{Journal}/{id}", Manager);
        return await detail.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Builds and delivers a real deal, which is what posts the entry.</summary>
    private async Task<Sale> DeliverAsync(
        decimal price = 24000m,
        decimal fee = 0m,
        decimal discount = 0m,
        decimal tradeAllowance = 0m,
        decimal tradePayoff = 0m,
        decimal cost = 18000m)
    {
        var rooftopId = await RooftopIdAsync("NAG-01");
        var suffix = $"{Guid.NewGuid():N}"[..8].ToUpperInvariant();

        var customerId = await CreatedIdAsync("/api/v1/customers", new
        {
            kind = "Person", firstName = "Ledger", lastName = $"Case{suffix}",
        });

        var vehicleId = await CreatedIdAsync("/api/v1/vehicles", new
        {
            vin = $"LEDGER{suffix}"[..13], modelYear = 2021, make = "Toyota", model = "RAV4",
            vinExceptionReason = "Synthetic VIN for a ledger test.",
        });

        var unitId = await CreatedIdAsync("/api/v1/inventory", new
        {
            vehicleId, rooftopId, stockNumber = $"L{suffix}",
            costAmount = cost, costCurrency = "USD",
        });

        using var available = await PostAsync($"/api/v1/inventory/{unitId}/status", Manager,
            new { status = "Available" });
        available.StatusCode.Should().Be(HttpStatusCode.OK);

        // The deal belongs to the salesperson so the manager can approve it —
        // nobody signs off their own numbers.
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

        object? trade = tradeAllowance == 0m && tradePayoff == 0m
            ? null
            : new { description = "2014 Civic", allowance = tradeAllowance, payoff = tradePayoff };

        using var terms = await PostAsync($"{Deals}/{dealId}/terms", Manager,
            new { charges = charges.ToArray(), tradeIn = trade });
        terms.StatusCode.Should().Be(HttpStatusCode.OK);

        foreach (var status in new[] { "Submitted", "Approved", "Delivered" })
        {
            using var moved = await PostAsync($"{Deals}/{dealId}/status", Manager, new { status });
            moved.StatusCode.Should().Be(HttpStatusCode.OK, $"moving to {status} should succeed");
        }

        return new Sale(dealId);
    }

    private async Task<string> CreatedIdAsync(string path, object body)
    {
        using var response = await PostAsync(path, Manager, body);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private async Task<IReadOnlyList<string>> ListAsync(string path, string email)
    {
        using var response = await SendAsync(HttpMethod.Get, path, email);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var results = await response.Content.ReadFromJsonAsync<JsonElement>();
        return results.EnumerateArray().Select(e => e.GetProperty("id").GetString()!).ToList();
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
