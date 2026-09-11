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
    private const string Technician = DevelopmentSeeder.DevUsers.TechnicianEmail;

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task The_chart_of_accounts_is_there_to_label_the_numbers()
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/v1/accounting/accounts", Manager);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var accounts = await response.Content.ReadFromJsonAsync<JsonElement>();
        var codes = accounts.EnumerateArray().Select(a => a.GetProperty("code").GetString()).ToList();

        codes.Should().Contain(["1000", "1100", "1300", "4000", "5000"]);
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
        // an entry.
        decimal Debit(string code) => SumFor(entry, code, "debit");
        decimal Credit(string code) => SumFor(entry, code, "credit");

        // 1100 and not 1000. Until 2026-09-10 this line debited Cash and asserted
        // that the customer paid in full the moment the car left, which made a
        // deposit, a fleet account and a lender paying it off all unrepresentable.
        // Delivering raises a DEBT; money arriving is a separate entry.
        Debit("1100").Should().Be(22100m, because: "that is what the customer owes");
        Debit("1310").Should().Be(3000m, because: "we now own their old car at the allowance");
        Debit("4900").Should().Be(300m, because: "a discount is a debit against revenue");
        Credit("4000").Should().Be(24000m);
        Credit("4100").Should().Be(400m);
        // Still cash, and rightly: settling the finance on a trade-in is money the
        // dealership actually pays out on the day, not something it owes itself.
        Credit("1000").Should().Be(1000m, because: "we settle what they still owed on the trade");
        Debit("5000").Should().Be(19000m, because: "the car cost that, and gross profit needs it");
        Credit("1300").Should().Be(19000m, because: "the car is off the lot");
    }

    [Fact]
    public async Task A_car_sold_with_tax_on_it_can_actually_be_delivered()
    {
        // The regression this guards against shipped on 2026-09-09 and lasted
        // half a day. Tax landed on the deal that morning and went into
        // AmountDue; the delivery posting debited the full amount and credited
        // nothing against the tax, so every delivery of a taxed car was refused
        // with "an entry must balance", out by exactly the tax. No test caught
        // it because no test delivered a deal that had any.
        var rooftopId = await RooftopIdAsync("NAG-01");
        var suffix = $"{Guid.NewGuid():N}"[..8].ToUpperInvariant();

        var customerId = await CreatedIdAsync("/api/v1/customers", new
        {
            kind = "Person", firstName = "Taxed", lastName = $"Delivery{suffix}",
        });

        var vehicleId = await CreatedIdAsync("/api/v1/vehicles", new
        {
            vin = $"TAXDEL{suffix}"[..13], modelYear = 2021, make = "Toyota", model = "Corolla",
            vinExceptionReason = "Synthetic VIN for a ledger test.",
        });

        var unitId = await CreatedIdAsync("/api/v1/inventory", new
        {
            vehicleId, rooftopId, stockNumber = $"T{suffix}",
            costAmount = 16000m, costCurrency = "USD",
        });

        using var available = await PostAsync($"/api/v1/inventory/{unitId}/status", Manager,
            new { status = "Available" });
        available.StatusCode.Should().Be(HttpStatusCode.OK);

        var dealId = await CreatedIdAsync(Deals, new
        {
            rooftopId, customerId, inventoryUnitId = unitId, currency = "USD",
            salespersonUserId = DevelopmentSeeder.DevUsers.Salesperson,
        });

        // A documentation fee as well as the car, because DocumentationFee became
        // its own ChargeKind on the same day tax arrived and was left out of the
        // posting's fee mapping — a second imbalance, out by exactly the fee, and
        // missed for exactly the same reason: no test used the new kind.
        using var terms = await PostAsync($"{Deals}/{dealId}/terms", Manager, new
        {
            charges = new object[]
            {
                new { kind = "VehiclePrice", description = "The car", amount = 20000m },
                new { kind = "DocumentationFee", description = "Documentation", amount = 499m },
            },
        });
        terms.StatusCode.Should().Be(HttpStatusCode.OK);

        using var taxed = await PostAsync($"{Deals}/{dealId}/tax", Manager, new
        {
            lines = new[]
            {
                new
                {
                    description = "Sales tax", jurisdiction = "US-IL",
                    basis = 20000m, rate = 0.0725m, amount = 1450m,
                    provenance = "EnteredByPerson",
                },
            },
            taxedAt = new
            {
                administrativeArea = "IL", county = "Sangamon", postalCode = "62704", country = "US",
            },
        });
        taxed.StatusCode.Should().Be(HttpStatusCode.OK);

        foreach (var status in new[] { "Submitted", "Approved", "Delivered" })
        {
            using var moved = await PostAsync($"{Deals}/{dealId}/status", Manager, new { status });
            moved.StatusCode.Should().Be(HttpStatusCode.OK,
                because: $"{status} must succeed on a taxed deal: "
                    + await moved.Content.ReadAsStringAsync());
        }

        var entry = await EntryForAsync(dealId);

        entry.GetProperty("totalDebits").GetDecimal().Should()
            .Be(entry.GetProperty("totalCredits").GetDecimal());

        // Credited to a liability. The dealership is holding this for the state,
        // not earning it — booking it as revenue would inflate the top line by
        // the tax on every car sold.
        var taxLine = entry.GetProperty("lines").EnumerateArray()
            .Single(l => l.GetProperty("accountCode").GetString() == "2100");

        taxLine.GetProperty("credit").GetDecimal().Should().Be(1450m);
    }

    [Fact]
    public async Task Taking_a_car_into_stock_puts_it_on_the_balance_sheet()
    {
        // Until 2026-09-10 this entry did not exist. Delivery credited 1300 at
        // cost for every car sold and nothing anywhere debited it, so a seeded
        // dealership that had sold thirty cars showed vehicle inventory at minus
        // $993,190 — an asset account nearly a million dollars negative. Every
        // entry balanced; the purchase simply had no entry.
        //
        // No test caught it because every test posted deliveries against stock a
        // seeder had written straight into the table, so the missing half was
        // never on the path anything exercised. Found by walking a day at the
        // dealership: docs/implementation/DEALER-DAY.md.
        var rooftopId = await RooftopIdAsync("NAG-01");
        var suffix = $"{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var stockNumber = $"P{suffix}";

        var vehicleId = await CreatedIdAsync("/api/v1/vehicles", new
        {
            vin = $"STKBUY{suffix}"[..13], modelYear = 2022, make = "Kia", model = "Sportage",
            vinExceptionReason = "Synthetic VIN for a ledger test.",
        });

        await CreatedIdAsync("/api/v1/inventory", new
        {
            vehicleId, rooftopId, stockNumber,
            costAmount = 14500m, costCurrency = "USD",
        });

        var purchase = await EntryForReferenceAsync(stockNumber);

        purchase.GetProperty("totalDebits").GetDecimal().Should()
            .Be(purchase.GetProperty("totalCredits").GetDecimal());

        SumFor(purchase, "1300", "debit").Should().Be(14500m,
            because: "the car is an asset the moment it is on the lot");

        SumFor(purchase, "1000", "credit").Should().Be(14500m,
            because: "something paid for it, and until floorplan exists that is cash");
    }

    [Fact]
    public async Task A_car_received_without_a_cost_is_recorded_and_not_posted()
    {
        // A part-exchange still being appraised, or stock that arrives before its
        // invoice does. Both are ordinary. Posting them at zero would assert the
        // car was free, which is a different claim from not knowing yet — so the
        // unit stands and the ledger says nothing.
        var rooftopId = await RooftopIdAsync("NAG-01");
        var suffix = $"{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var stockNumber = $"N{suffix}";

        var vehicleId = await CreatedIdAsync("/api/v1/vehicles", new
        {
            vin = $"NOCOST{suffix}"[..13], modelYear = 2019, make = "Ford", model = "Focus",
            vinExceptionReason = "Synthetic VIN for a ledger test.",
        });

        var unitId = await CreatedIdAsync("/api/v1/inventory", new
        {
            vehicleId, rooftopId, stockNumber,
        });

        unitId.Should().NotBeNullOrWhiteSpace(because: "the car really is on the lot");

        using var response = await SendAsync(
            HttpMethod.Get, $"{Journal}?reference={stockNumber}", Manager);

        var summaries = await response.Content.ReadFromJsonAsync<JsonElement>();
        summaries.EnumerateArray().Should().BeEmpty(
            because: "a cost we do not know is not a cost of nothing");
    }

    [Fact]
    public async Task A_car_bought_and_sold_leaves_nothing_behind_on_inventory()
    {
        // The arithmetic that matters, and the one the walk's finding reduces to:
        // received at cost and delivered at the same cost, account 1300 must come
        // back to where it started. Either half alone looks fine in isolation —
        // it is the pair that has to net.
        var rooftopId = await RooftopIdAsync("NAG-01");
        var suffix = $"{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var stockNumber = $"R{suffix}";
        const decimal Cost = 17250m;

        var customerId = await CreatedIdAsync("/api/v1/customers", new
        {
            kind = "Person", firstName = "Round", lastName = $"Trip{suffix}",
        });

        var vehicleId = await CreatedIdAsync("/api/v1/vehicles", new
        {
            vin = $"RNDTRP{suffix}"[..13], modelYear = 2023, make = "Honda", model = "CR-V",
            vinExceptionReason = "Synthetic VIN for a ledger test.",
        });

        var unitId = await CreatedIdAsync("/api/v1/inventory", new
        {
            vehicleId, rooftopId, stockNumber,
            costAmount = Cost, costCurrency = "USD",
        });

        using var available = await PostAsync($"/api/v1/inventory/{unitId}/status", Manager,
            new { status = "Available" });
        available.StatusCode.Should().Be(HttpStatusCode.OK);

        var dealId = await CreatedIdAsync(Deals, new
        {
            rooftopId, customerId, inventoryUnitId = unitId, currency = "USD",
            salespersonUserId = DevelopmentSeeder.DevUsers.Salesperson,
        });

        using var terms = await PostAsync($"{Deals}/{dealId}/terms", Manager, new
        {
            charges = new object[]
            {
                new { kind = "VehiclePrice", description = "The car", amount = 21000m },
            },
        });
        terms.StatusCode.Should().Be(HttpStatusCode.OK);

        foreach (var status in new[] { "Submitted", "Approved", "Delivered" })
        {
            using var moved = await PostAsync($"{Deals}/{dealId}/status", Manager, new { status });
            moved.StatusCode.Should().Be(HttpStatusCode.OK,
                because: await moved.Content.ReadAsStringAsync());
        }

        var purchase = await EntryForReferenceAsync(stockNumber);
        var delivery = await EntryForAsync(dealId);

        var onto = SumFor(purchase, "1300", "debit");
        var off = SumFor(delivery, "1300", "credit");

        onto.Should().Be(Cost);
        off.Should().Be(Cost);
        (onto - off).Should().Be(0m,
            because: "a car that came and went must leave inventory exactly as it found it");
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
        SumFor(reversal, "1100", "credit").Should().BeGreaterThan(0m);
        reversal.GetProperty("totalDebits").GetDecimal().Should()
            .Be(reversal.GetProperty("totalCredits").GetDecimal());

        // The original is exactly as it was.
        using var originalNow = await SendAsync(HttpMethod.Get, $"{Journal}/{entryId}", Manager);
        var original = await originalNow.Content.ReadFromJsonAsync<JsonElement>();
        SumFor(original, "1100", "debit").Should().BeGreaterThan(0m);
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


    [Fact]
    public async Task Delivering_a_car_raises_a_debt_somebody_is_recorded_as_owing()
    {
        // The ledger knowing it is owed $22,100 is not enough to chase anybody.
        // Before 2026-09-10 there was no sub-ledger at all and no receivable in
        // the chart, so delivering debited Cash and asserted the customer had
        // already paid — which is true of almost no car ever sold.
        var sale = await DeliverAsync(price: 20000m, cost: 15000m);

        using var found = await SendAsync(
            HttpMethod.Get, $"/api/v1/receivables/for/Deal/{sale.DealId}", Manager);

        found.StatusCode.Should().Be(HttpStatusCode.OK, because: "the car has gone and the money has not");

        var owed = await found.Content.ReadFromJsonAsync<JsonElement>();

        owed.GetProperty("amount").GetDecimal().Should().Be(20000m);
        owed.GetProperty("outstanding").GetDecimal().Should().Be(20000m);
        owed.GetProperty("paid").GetDecimal().Should().Be(0m);
        owed.GetProperty("isSettled").GetBoolean().Should().BeFalse();
        owed.GetProperty("customerName").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task A_deposit_is_a_part_payment_and_leaves_the_rest_owing()
    {
        // The ordinary case, and the one a stored balance would eventually get
        // wrong: outstanding is derived from the payments, never written.
        var sale = await DeliverAsync(price: 20000m, cost: 15000m);
        var receivable = await ReceivableForAsync(sale.DealId);

        using var deposit = await PostAsync(
            $"/api/v1/receivables/{receivable}/payments", Manager,
            new { amount = 500m, method = "Card", note = "Deposit taken on the day" });

        deposit.StatusCode.Should().Be(HttpStatusCode.OK,
            because: await deposit.Content.ReadAsStringAsync());

        var after = await deposit.Content.ReadFromJsonAsync<JsonElement>();

        after.GetProperty("paid").GetDecimal().Should().Be(500m);
        after.GetProperty("outstanding").GetDecimal().Should().Be(19500m);
        after.GetProperty("isSettled").GetBoolean().Should().BeFalse();
        after.GetProperty("payments").EnumerateArray().Should().HaveCount(1);
    }

    [Fact]
    public async Task Paying_moves_the_money_from_owed_to_the_bank()
    {
        // The arithmetic that keeps the sub-ledger and the ledger honest: what a
        // customer owes goes down by exactly what the bank goes up by. If these
        // two ever disagree, "who owes us" and the trial balance are telling
        // different stories and only an auditor will find out.
        var sale = await DeliverAsync(price: 20000m, cost: 15000m);
        var receivable = await ReceivableForAsync(sale.DealId);

        using var paid = await PostAsync(
            $"/api/v1/receivables/{receivable}/payments", Manager,
            new { amount = 20000m, method = "BankTransfer", note = "Settled in full" });

        paid.StatusCode.Should().Be(HttpStatusCode.OK, because: await paid.Content.ReadAsStringAsync());

        var settled = await paid.Content.ReadFromJsonAsync<JsonElement>();
        settled.GetProperty("outstanding").GetDecimal().Should().Be(0m);
        settled.GetProperty("isSettled").GetBoolean().Should().BeTrue();

        // The entry the payment posted, filed under the same reference as the
        // bill so the two read together in the journal.
        var entries = await EntriesForAsync(sale.DealId);
        var payment = entries.Single(e => e.GetProperty("source").GetString() == "Payment");

        var detail = await EntryDetailAsync(payment.GetProperty("id").GetString()!);

        SumFor(detail, "1000", "debit").Should().Be(20000m, because: "the money is in the bank now");
        SumFor(detail, "1100", "credit").Should().Be(20000m, because: "and off what they owed");

        detail.GetProperty("totalDebits").GetDecimal().Should()
            .Be(detail.GetProperty("totalCredits").GetDecimal());
    }

    [Fact]
    public async Task A_lender_settling_a_financed_car_is_a_payment_like_any_other()
    {
        // The customer signed for the total and a finance house sends the money.
        // Recorded as a method rather than a different kind of debt, because what
        // the dealership is owed does not change with who hands it over.
        var sale = await DeliverAsync(price: 20000m, cost: 15000m);
        var receivable = await ReceivableForAsync(sale.DealId);

        using var settled = await PostAsync(
            $"/api/v1/receivables/{receivable}/payments", Manager,
            new { amount = 20000m, method = "Finance", note = "Northgate, agreement 88213" });

        settled.StatusCode.Should().Be(HttpStatusCode.OK);

        var after = await settled.Content.ReadFromJsonAsync<JsonElement>();
        after.GetProperty("isSettled").GetBoolean().Should().BeTrue();
        after.GetProperty("payments").EnumerateArray().Single()
            .GetProperty("method").GetString().Should().Be("Finance");
    }

    [Fact]
    public async Task Taking_more_than_is_owed_is_refused_rather_than_absorbed()
    {
        // Quietly showing zero would lose real money: the difference belongs to
        // the customer and somebody has to give it back. Credit balances are not
        // built, so the honest answer is to refuse and say why.
        var sale = await DeliverAsync(price: 20000m, cost: 15000m);
        var receivable = await ReceivableForAsync(sale.DealId);

        using var tooMuch = await PostAsync(
            $"/api/v1/receivables/{receivable}/payments", Manager,
            new { amount = 25000m, method = "Cash" });

        tooMuch.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // And nothing was taken: a refused payment must not leave half of itself
        // behind, which is what the transaction around it is for.
        using var unchanged = await SendAsync(
            HttpMethod.Get, $"/api/v1/receivables/{receivable}", Manager);

        (await unchanged.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("paid").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task A_settled_account_refuses_a_second_payment()
    {
        var sale = await DeliverAsync(price: 20000m, cost: 15000m);
        var receivable = await ReceivableForAsync(sale.DealId);

        using var first = await PostAsync(
            $"/api/v1/receivables/{receivable}/payments", Manager,
            new { amount = 20000m, method = "Cash" });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        using var again = await PostAsync(
            $"/api/v1/receivables/{receivable}/payments", Manager,
            new { amount = 100m, method = "Cash" });

        again.StatusCode.Should().Be(HttpStatusCode.Conflict,
            because: "paying a settled bill again is a mistake, not a credit");
    }

    [Fact]
    public async Task Whoever_may_not_post_may_not_take_money()
    {
        // Taking money moves 1100 to 1000, so it is posting. A technician writes
        // up work and cannot invoice it; they cannot take the payment either.
        var sale = await DeliverAsync(price: 20000m, cost: 15000m);
        var receivable = await ReceivableForAsync(sale.DealId);

        using var refused = await PostAsync(
            $"/api/v1/receivables/{receivable}/payments", Technician,
            new { amount = 100m, method = "Cash" });

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task What_is_owed_can_be_listed_without_the_bills_already_paid()
    {
        var sale = await DeliverAsync(price: 20000m, cost: 15000m);
        var receivable = await ReceivableForAsync(sale.DealId);

        using var open = await SendAsync(HttpMethod.Get, "/api/v1/receivables?limit=200", Manager);
        open.StatusCode.Should().Be(HttpStatusCode.OK);

        var before = await open.Content.ReadFromJsonAsync<JsonElement>();
        before.EnumerateArray().Select(r => r.GetProperty("id").GetString())
            .Should().Contain(receivable);

        using var paid = await PostAsync(
            $"/api/v1/receivables/{receivable}/payments", Manager,
            new { amount = 20000m, method = "Cash" });
        paid.StatusCode.Should().Be(HttpStatusCode.OK);

        using var stillOpen = await SendAsync(HttpMethod.Get, "/api/v1/receivables?limit=200", Manager);
        var after = await stillOpen.Content.ReadFromJsonAsync<JsonElement>();

        after.EnumerateArray().Select(r => r.GetProperty("id").GetString())
            .Should().NotContain(receivable, because: "the question is who still owes us");
    }
    // --- helpers -----------------------------------------------------------

    /// <summary>The receivable id for a delivered deal, which delivery opened.</summary>
    private async Task<string> ReceivableForAsync(string dealId)
    {
        using var response = await SendAsync(
            HttpMethod.Get, $"/api/v1/receivables/for/Deal/{dealId}", Manager);

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: $"{dealId} should be owed");

        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString()!;
    }

    /// <summary>Every entry filed under one reference — a bill and its payments.</summary>
    private async Task<List<JsonElement>> EntriesForAsync(string reference)
    {
        using var response = await SendAsync(HttpMethod.Get, $"{Journal}?reference={reference}", Manager);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
    }

    private async Task<JsonElement> EntryDetailAsync(string entryId)
    {
        using var response = await SendAsync(HttpMethod.Get, $"{Journal}/{entryId}", Manager);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private sealed record Sale(string DealId);

    private static JsonElement AccountIn(JsonElement balance, string code) =>
        balance.GetProperty("accounts").EnumerateArray()
            .Single(a => a.GetProperty("code").GetString() == code);

    private static decimal SumFor(JsonElement entry, string code, string side) =>
        entry.GetProperty("lines").EnumerateArray()
            .Where(l => l.GetProperty("accountCode").GetString() == code)
            .Sum(l => l.GetProperty(side).GetDecimal());

    /// <summary>
    /// The entry filed under a reference that is not a deal — a stock number, for
    /// a purchase. Same shape as <see cref="EntryForAsync"/>; kept separate so the
    /// name says which kind of reference is being looked up.
    /// </summary>
    private async Task<JsonElement> EntryForReferenceAsync(string reference)
    {
        using var response = await SendAsync(HttpMethod.Get, $"{Journal}?reference={reference}", Manager);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var summaries = await response.Content.ReadFromJsonAsync<JsonElement>();
        summaries.EnumerateArray().Should().NotBeEmpty(because: $"{reference} should have posted");

        var id = summaries.EnumerateArray().First().GetProperty("id").GetString()!;

        using var detail = await SendAsync(HttpMethod.Get, $"{Journal}/{id}", Manager);
        return await detail.Content.ReadFromJsonAsync<JsonElement>();
    }

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
