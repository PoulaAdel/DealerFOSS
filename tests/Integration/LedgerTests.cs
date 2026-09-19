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
        summaries.Rows().Should().BeEmpty(
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
    public async Task A_reconditioned_car_leaves_nothing_behind_on_inventory_either()
    {
        // The same arithmetic as the test above, with a workshop visit in the
        // middle — and until 2026-09-19 it did not hold.
        //
        // Invoicing internal work debited 1300 by the recon spend, correctly.
        // Delivery credited 1300 by the unit's ACQUISITION cost, because nothing
        // had recorded which car absorbed the recon. So the recon stayed in
        // vehicle inventory after the car had gone, and used-vehicle gross was
        // overstated by exactly that amount — the failure the posting's own
        // comment in AccountingService says it exists to prevent.
        //
        // Neither balancing check could see it: every entry balanced on its own.
        // It was an account that never came back to zero.
        var rooftopId = await RooftopIdAsync("NAG-01");
        var suffix = $"{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var stockNumber = $"C{suffix}";
        const decimal Cost = 14_500m;
        const decimal Recon = 3m * 60m;
        const decimal Price = 21_000m;

        var customerId = await CreatedIdAsync("/api/v1/customers", new
        {
            kind = "Person", firstName = "Recon", lastName = $"Trip{suffix}",
        });

        var vehicleId = await CreatedIdAsync("/api/v1/vehicles", new
        {
            vin = $"RCNTRP{suffix}"[..13], modelYear = 2023, make = "Toyota", model = "RAV4",
            vinExceptionReason = "Synthetic VIN for a ledger test.",
        });

        var unitId = await CreatedIdAsync("/api/v1/inventory", new
        {
            vehicleId, rooftopId, stockNumber, costAmount = Cost, costCurrency = "USD",
        });

        // Through the workshop on the dealership's own money, which is what
        // makes the work capitalise rather than become a charge.
        var jobId = await CreatedIdAsync("/api/v1/repair-orders", new
        {
            rooftopId, customerId, vehicleId, currency = "USD",
            complaint = "Make it saleable.", odometerReading = 40_000,
        });

        using (var line = await PostAsync($"/api/v1/repair-orders/{jobId}/lines", Manager, new
        {
            kind = "Labour", description = "Recon before it goes on the lot",
            hours = 3m, rate = 60m, payType = "Internal",
        }))
        {
            line.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        foreach (var status in new[] { "InProgress", "Completed", "Invoiced" })
        {
            using var moved = await PostAsync($"/api/v1/repair-orders/{jobId}/status", Manager, new { status });
            moved.StatusCode.Should().Be(HttpStatusCode.OK, because: await moved.Content.ReadAsStringAsync());
        }

        // The car now says what it is carried at, rather than only what it cost.
        using (var carried = await SendAsync(HttpMethod.Get, $"/api/v1/inventory/{unitId}", Manager))
        {
            var unit = await carried.Content.ReadFromJsonAsync<JsonElement>();

            unit.GetProperty("reconditioningAmount").GetDecimal().Should().Be(Recon,
                because: "the work went onto this car, not merely into account 1300");
            unit.GetProperty("bookValueAmount").GetDecimal().Should().Be(Cost + Recon);
            unit.GetProperty("costAmount").GetDecimal().Should().Be(Cost,
                because: "acquisition cost is still acquisition cost; the book value is the sum");

            // And where it came from, which a running total could not answer.
            var charges = unit.GetProperty("reconditioning").EnumerateArray().ToList();
            charges.Should().ContainSingle();
            charges[0].GetProperty("sourceRepairOrderId").GetString().Should().Be(jobId);
        }

        using (var available = await PostAsync($"/api/v1/inventory/{unitId}/status", Manager,
            new { status = "Available" }))
        {
            available.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var dealId = await CreatedIdAsync(Deals, new
        {
            rooftopId, customerId, inventoryUnitId = unitId, currency = "USD",
            salespersonUserId = DevelopmentSeeder.DevUsers.Salesperson,
        });

        using (var terms = await PostAsync($"{Deals}/{dealId}/terms", Manager, new
        {
            charges = new object[]
            {
                new { kind = "VehiclePrice", description = "The car", amount = Price },
            },
        }))
        {
            terms.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        foreach (var status in new[] { "Submitted", "Approved", "Delivered" })
        {
            using var moved = await PostAsync($"{Deals}/{dealId}/status", Manager, new { status });
            moved.StatusCode.Should().Be(HttpStatusCode.OK, because: await moved.Content.ReadAsStringAsync());
        }

        var purchase = await EntryForReferenceAsync(stockNumber);
        var recon = await EntryForReferenceAsync(jobId);
        var delivery = await EntryForAsync(dealId);

        var onto = SumFor(purchase, "1300", "debit") + SumFor(recon, "1300", "debit");
        var off = SumFor(delivery, "1300", "credit");

        onto.Should().Be(Cost + Recon, because: "both the purchase and the recon went onto the car");
        off.Should().Be(Cost + Recon,
            because: "delivery relieves what the car is CARRIED at, not what it was bought for");
        (onto - off).Should().Be(0m,
            because: "a reconditioned car that came and went must leave inventory exactly as it "
                + "found it. Before 2026-09-19 this was short by the recon, every time, forever");

        // The number a manager is actually looking at.
        SumFor(delivery, "5000", "debit").Should().Be(Cost + Recon);
        (Price - SumFor(delivery, "5000", "debit")).Should().Be(Price - Cost - Recon,
            because: "gross is the price less everything the car cost to make saleable");
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
    public async Task Paying_more_than_is_owed_settles_the_bill_and_owes_the_rest_back()
    {
        // Until 2026-09-14 this was REFUSED, and the refusal was honest and
        // useless: a customer paying a $20,000 car with $20,500 has not made a
        // mistake, and "we cannot accept that" is not an answer a counter can
        // give. What was missing was somewhere to put the difference.
        var sale = await DeliverAsync(price: 20000m, cost: 15000m);
        var receivable = await ReceivableForAsync(sale.DealId);

        using var paid = await PostAsync(
            $"/api/v1/receivables/{receivable}/payments", Manager,
            new { amount = 20500m, method = "Cash", note = "Paid with five hundred over" });

        paid.StatusCode.Should().Be(HttpStatusCode.OK, because: await paid.Content.ReadAsStringAsync());

        var after = await paid.Content.ReadFromJsonAsync<JsonElement>();

        // The bill takes what it can hold and no more. Outstanding must never go
        // negative, because every report that reads it assumes it cannot.
        after.GetProperty("paid").GetDecimal().Should().Be(20000m);
        after.GetProperty("outstanding").GetDecimal().Should().Be(0m);
        after.GetProperty("isSettled").GetBoolean().Should().BeTrue();

        var credit = after.GetProperty("creditsRaised").EnumerateArray().Single();
        credit.GetProperty("amount").GetDecimal().Should().Be(500m);
        credit.GetProperty("remaining").GetDecimal().Should().Be(500m);
        credit.GetProperty("isSpent").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task An_overpayment_is_one_entry_with_the_cash_split_two_ways()
    {
        // ONE entry, because the customer performed one act. Somebody reading the
        // journal sees the whole amount arrive, the bill's share clear the
        // receivable, and the rest become a liability, on one date.
        //
        // 2200 is credited rather than 1100 being over-credited into a negative,
        // and that is the point of the account: netting would let one customer's
        // credit hide another customer's debt and make the figure the dealership
        // chases quietly too small.
        var sale = await DeliverAsync(price: 20000m, cost: 15000m);
        var receivable = await ReceivableForAsync(sale.DealId);

        using var paid = await PostAsync(
            $"/api/v1/receivables/{receivable}/payments", Manager,
            new { amount = 20500m, method = "Cash" });

        paid.StatusCode.Should().Be(HttpStatusCode.OK);

        var entries = await EntriesForAsync(sale.DealId);
        var payment = entries.Single(e => e.GetProperty("source").GetString() == "Payment");
        var detail = await EntryDetailAsync(payment.GetProperty("id").GetString()!);

        SumFor(detail, "1000", "debit").Should().Be(20500m, because: "all of it went in the till");
        SumFor(detail, "1100", "credit").Should().Be(20000m, because: "the bill's share, and no more");
        SumFor(detail, "2200", "credit").Should().Be(500m,
            because: "the rest is the customer's money and the dealership owes it back");

        detail.GetProperty("totalDebits").GetDecimal().Should()
            .Be(detail.GetProperty("totalCredits").GetDecimal());
    }

    [Fact]
    public async Task A_credit_pays_another_bill_the_same_customer_owes_and_no_cash_moves()
    {
        var first = await DeliverAsync(price: 20000m, cost: 15000m);

        using var overpaid = await PostAsync(
            $"/api/v1/receivables/{await ReceivableForAsync(first.DealId)}/payments", Manager,
            new { amount = 20500m, method = "Cash" });

        overpaid.StatusCode.Should().Be(HttpStatusCode.OK);

        var credit = (await overpaid.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("creditsRaised").EnumerateArray().Single();

        var creditId = credit.GetProperty("id").GetString()!;
        var buyer = credit.GetProperty("customerId").GetString()!;

        // A second car, to the same person.
        var second = await DeliverAsync(price: 8000m, cost: 6000m, buyer: buyer);
        var secondBill = await ReceivableForAsync(second.DealId);

        using var applied = await PostAsync(
            $"/api/v1/receivables/credits/{creditId}/apply", Manager,
            new { receivableId = secondBill, amount = 500m, note = "Off the second car" });

        applied.StatusCode.Should().Be(HttpStatusCode.OK, because: await applied.Content.ReadAsStringAsync());

        var spent = await applied.Content.ReadFromJsonAsync<JsonElement>();
        spent.GetProperty("remaining").GetDecimal().Should().Be(0m);
        spent.GetProperty("isSpent").GetBoolean().Should().BeTrue();
        spent.GetProperty("uses").EnumerateArray().Single()
            .GetProperty("kind").GetString().Should().Be("AppliedToBill");

        using var bill = await SendAsync(HttpMethod.Get, $"/api/v1/receivables/{secondBill}", Manager);
        var billed = await bill.Content.ReadFromJsonAsync<JsonElement>();

        billed.GetProperty("paid").GetDecimal().Should().Be(500m);
        billed.GetProperty("payments").EnumerateArray().Single()
            .GetProperty("method").GetString().Should().Be("CustomerCredit");

        // AND NO CASH MOVED. The money arrived when they overpaid; counting it
        // again here would book the same five hundred dollars into the bank
        // twice, and the bank statement would be the only thing that noticed.
        var entry = (await EntriesForAsync(second.DealId))
            .Single(e => e.GetProperty("source").GetString() == "CreditApplied");

        var detail = await EntryDetailAsync(entry.GetProperty("id").GetString()!);

        SumFor(detail, "2200", "debit").Should().Be(500m, because: "we owe them that much less");
        SumFor(detail, "1100", "credit").Should().Be(500m, because: "and they owe us that much less");
        SumFor(detail, "1000", "debit").Should().Be(0m, because: "no money arrived today");
        SumFor(detail, "1000", "credit").Should().Be(0m, because: "and none left either");
    }

    [Fact]
    public async Task One_customers_credit_cannot_pay_another_customers_bill()
    {
        // The check that matters most in the whole feature. Without it a credit
        // is a way to move money between people who never agreed to it, and the
        // ledger balances perfectly the entire time.
        var mine = await DeliverAsync(price: 20000m, cost: 15000m);

        using var overpaid = await PostAsync(
            $"/api/v1/receivables/{await ReceivableForAsync(mine.DealId)}/payments", Manager,
            new { amount = 20500m, method = "Cash" });

        var creditId = (await overpaid.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("creditsRaised").EnumerateArray().Single()
            .GetProperty("id").GetString()!;

        // Somebody else entirely, with their own bill.
        var theirs = await DeliverAsync(price: 9000m, cost: 7000m);

        using var refused = await PostAsync(
            $"/api/v1/receivables/credits/{creditId}/apply", Manager,
            new { receivableId = await ReceivableForAsync(theirs.DealId), amount = 500m });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // And the credit is untouched: a refused application must not spend half
        // of itself, which is what the transaction around it is for.
        using var still = await SendAsync(
            HttpMethod.Get, $"/api/v1/receivables/credits?customerId={mine.CustomerId}", Manager);

        (await still.Content.ReadFromJsonAsync<JsonElement>())
            .Rows().Single(c => c.GetProperty("id").GetString() == creditId)
            .GetProperty("remaining").GetDecimal().Should().Be(500m);
    }

    [Fact]
    public async Task Refunding_a_credit_takes_the_money_back_out_of_the_bank()
    {
        var sale = await DeliverAsync(price: 20000m, cost: 15000m);

        using var overpaid = await PostAsync(
            $"/api/v1/receivables/{await ReceivableForAsync(sale.DealId)}/payments", Manager,
            new { amount = 20500m, method = "Cash" });

        var creditId = (await overpaid.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("creditsRaised").EnumerateArray().Single()
            .GetProperty("id").GetString()!;

        using var refunded = await PostAsync(
            $"/api/v1/receivables/credits/{creditId}/refund", Manager,
            new { amount = 500m, method = "BankTransfer", note = "Sent back the same day" });

        refunded.StatusCode.Should().Be(HttpStatusCode.OK, because: await refunded.Content.ReadAsStringAsync());

        var after = await refunded.Content.ReadFromJsonAsync<JsonElement>();
        after.GetProperty("remaining").GetDecimal().Should().Be(0m);
        after.GetProperty("uses").EnumerateArray().Single()
            .GetProperty("kind").GetString().Should().Be("Refunded");

        var entry = (await EntriesForAsync(sale.DealId))
            .Single(e => e.GetProperty("source").GetString() == "CreditRefunded");

        var detail = await EntryDetailAsync(entry.GetProperty("id").GetString()!);

        SumFor(detail, "2200", "debit").Should().Be(500m, because: "we no longer owe it");
        SumFor(detail, "1000", "credit").Should().Be(500m, because: "and it has left the bank");
    }

    [Fact]
    public async Task Taking_money_and_handing_it_back_are_different_rights()
    {
        // A salesperson holds Accounting.Post because delivering a car posts the
        // sale, so they can take money all day — taking it is not the risk. A
        // refund is the classic way a retail business is quietly stolen from:
        // raise a credit, pay it to yourself, and the books balance throughout.
        var sale = await DeliverAsync(price: 20000m, cost: 15000m);

        using var overpaid = await PostAsync(
            $"/api/v1/receivables/{await ReceivableForAsync(sale.DealId)}/payments", Manager,
            new { amount = 20500m, method = "Cash" });

        var creditId = (await overpaid.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("creditsRaised").EnumerateArray().Single()
            .GetProperty("id").GetString()!;

        using var refused = await PostAsync(
            $"/api/v1/receivables/credits/{creditId}/refund", Sales,
            new { amount = 500m, method = "Cash" });

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "Accounting.Post is not Accounting.Refund");

        using var untouched = await SendAsync(
            HttpMethod.Get, $"/api/v1/receivables/credits?customerId={sale.CustomerId}", Manager);

        (await untouched.Content.ReadFromJsonAsync<JsonElement>())
            .Rows().Single(c => c.GetProperty("id").GetString() == creditId)
            .GetProperty("remaining").GetDecimal().Should().Be(500m);
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
        before.Rows().Select(r => r.GetProperty("id").GetString())
            .Should().Contain(receivable);

        using var paid = await PostAsync(
            $"/api/v1/receivables/{receivable}/payments", Manager,
            new { amount = 20000m, method = "Cash" });
        paid.StatusCode.Should().Be(HttpStatusCode.OK);

        using var stillOpen = await SendAsync(HttpMethod.Get, "/api/v1/receivables?limit=200", Manager);
        var after = await stillOpen.Content.ReadFromJsonAsync<JsonElement>();

        after.Rows().Select(r => r.GetProperty("id").GetString())
            .Should().NotContain(receivable, because: "the question is who still owes us");
    }

    [Fact]
    public async Task An_overhead_can_be_recorded_at_all()
    {
        // Before 2026-09-11 the chart held no expense account of any kind - not
        // wages, not rent, not advertising - so a dealership could record
        // everything it earned and nothing it spent. Its own permission, because
        // choosing the accounts and the amounts is the most powerful thing
        // anybody can do to a set of books.
        var rooftopId = await RooftopIdAsync("NAG-01");

        using var posted = await PostAsync($"{Journal}", Manager, new
        {
            rooftopId,
            entryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            memo = "September rent",
            currency = "USD",
            lines = new[]
            {
                new { accountCode = "6100", debit = 4500m, credit = 0m, memo = "Premises" },
                new { accountCode = "1000", debit = 0m, credit = 4500m, memo = "Paid from the bank" },
            },
        });

        posted.StatusCode.Should().Be(HttpStatusCode.OK, because: await posted.Content.ReadAsStringAsync());

        var entry = await posted.Content.ReadFromJsonAsync<JsonElement>();
        entry.GetProperty("source").GetString().Should().Be("Manual");
        SumFor(entry, "6100", "debit").Should().Be(4500m);
        SumFor(entry, "1000", "credit").Should().Be(4500m);
    }

    [Fact]
    public async Task Writing_an_entry_by_hand_is_not_something_a_salesperson_may_do()
    {
        // They hold Accounting.Post, because delivering a car posts the sale.
        // That is posting a consequence of work they did; this is choosing the
        // accounts, and it is a different right on purpose.
        var rooftopId = await RooftopIdAsync("NAG-01");

        using var refused = await PostAsync($"{Journal}", Sales, new
        {
            rooftopId,
            entryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            memo = "Nothing to see here",
            currency = "USD",
            lines = new[]
            {
                new { accountCode = "1000", debit = 5000m, credit = 0m, memo = (string?)null },
                new { accountCode = "3000", debit = 0m, credit = 5000m, memo = (string?)null },
            },
        });

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_hand_written_entry_that_does_not_balance_is_refused_with_a_reason()
    {
        var rooftopId = await RooftopIdAsync("NAG-01");

        using var refused = await PostAsync($"{Journal}", Manager, new
        {
            rooftopId,
            entryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            memo = "Wrong on purpose",
            currency = "USD",
            lines = new[]
            {
                new { accountCode = "6000", debit = 1000m, credit = 0m, memo = (string?)null },
                new { accountCode = "1000", debit = 0m, credit = 900m, memo = (string?)null },
            },
        });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_entry_naming_an_account_that_does_not_exist_says_which()
    {
        var rooftopId = await RooftopIdAsync("NAG-01");

        using var refused = await PostAsync($"{Journal}", Manager, new
        {
            rooftopId,
            entryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            memo = "Typo",
            currency = "USD",
            lines = new[]
            {
                new { accountCode = "9999", debit = 10m, credit = 0m, memo = (string?)null },
                new { accountCode = "1000", debit = 0m, credit = 10m, memo = (string?)null },
            },
        });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("9999");
    }

    [Fact]
    public async Task An_entry_with_no_explanation_is_refused()
    {
        // The one somebody will be asked about in a year and nobody will be able
        // to answer.
        var rooftopId = await RooftopIdAsync("NAG-01");

        using var refused = await PostAsync($"{Journal}", Manager, new
        {
            rooftopId,
            entryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            memo = "   ",
            currency = "USD",
            lines = new[]
            {
                new { accountCode = "6000", debit = 10m, credit = 0m, memo = (string?)null },
                new { accountCode = "1000", debit = 0m, credit = 10m, memo = (string?)null },
            },
        });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_profit_and_loss_takes_overheads_off_the_gross()
    {
        // The report this system could not produce at all before 2026-09-11.
        // Gross comes from the same method the dashboard reads, so the two cannot
        // disagree; what is new is everything below the gross line.
        var rooftopId = await RooftopIdAsync("NAG-01");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using var wages = await PostAsync($"{Journal}", Manager, new
        {
            rooftopId,
            entryDate = today,
            memo = "Wages for the P&L test",
            currency = "USD",
            lines = new[]
            {
                new { accountCode = "6000", debit = 1200m, credit = 0m, memo = (string?)null },
                new { accountCode = "1000", debit = 0m, credit = 1200m, memo = (string?)null },
            },
        });
        wages.StatusCode.Should().Be(HttpStatusCode.OK);

        using var response = await SendAsync(
            HttpMethod.Get, $"/api/v1/accounting/profit-and-loss?from={today}&to={today}", Manager);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<JsonElement>();

        var gross = report.GetProperty("grossProfit").GetDecimal();
        var expenses = report.GetProperty("totalExpenses").GetDecimal();
        var net = report.GetProperty("netProfit").GetDecimal();

        expenses.Should().BeGreaterThanOrEqualTo(1200m, because: "the wages just posted are in there");
        net.Should().Be(gross - expenses, because: "net is gross less what it costs to run the place");

        // Every overhead account is listed, including the ones at nothing, so a
        // missing figure and no spending do not look the same.
        report.GetProperty("expenses").EnumerateArray().Select(e => e.GetProperty("code").GetString())
            .Should().Contain(["6000", "6100", "6200", "6300", "6900"]);
    }

    [Fact]
    public async Task Cost_of_sales_is_not_subtracted_twice()
    {
        // The mistake that reads as a plausible net profit about a million
        // dollars too low: cost of sales is an expense account, so a report that
        // took every AccountKind.Expense as an overhead would subtract it once
        // inside the departmental gross and again below the line.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using var response = await SendAsync(
            HttpMethod.Get, $"/api/v1/accounting/profit-and-loss?from={today}&to={today}", Manager);

        var report = await response.Content.ReadFromJsonAsync<JsonElement>();
        var codes = report.GetProperty("expenses").EnumerateArray()
            .Select(e => e.GetProperty("code").GetString()).ToList();

        codes.Should().NotContain("5000", because: "cost of vehicle sales is already inside the gross");
        codes.Should().NotContain("5300", because: "so is cost of parts sales");
        codes.Should().NotContain("5500", because: "and the cost of F&I products");
    }

    [Fact]
    public async Task A_balance_sheet_balances()
    {
        // Assets = liabilities + equity + what has been earned. If it ever does
        // not, something has been posted this report cannot classify, and saying
        // so is more useful than printing a plausible page with a hole in it.
        using var response = await SendAsync(HttpMethod.Get, "/api/v1/accounting/balance-sheet", Manager);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var sheet = await response.Content.ReadFromJsonAsync<JsonElement>();

        var assets = sheet.GetProperty("totalAssets").GetDecimal();
        var liabilities = sheet.GetProperty("totalLiabilities").GetDecimal();
        var equity = sheet.GetProperty("totalEquity").GetDecimal();
        var earnings = sheet.GetProperty("earningsToDate").GetDecimal();

        assets.Should().Be(liabilities + equity + earnings);
        sheet.GetProperty("balances").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Capital_put_in_shows_as_equity_and_keeps_the_sheet_balanced()
    {
        // The other half of what the stock-purchase posting exposed: a business
        // with no capital cannot buy anything, and a balance sheet with no equity
        // section does not balance in any form a person would recognise.
        var rooftopId = await RooftopIdAsync("NAG-01");

        using var opening = await PostAsync($"{Journal}", Manager, new
        {
            rooftopId,
            entryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            memo = "Capital introduced",
            currency = "USD",
            lines = new[]
            {
                new { accountCode = "1000", debit = 50000m, credit = 0m, memo = (string?)null },
                new { accountCode = "3000", debit = 0m, credit = 50000m, memo = (string?)null },
            },
        });
        opening.StatusCode.Should().Be(HttpStatusCode.OK);

        using var response = await SendAsync(HttpMethod.Get, "/api/v1/accounting/balance-sheet", Manager);
        var sheet = await response.Content.ReadFromJsonAsync<JsonElement>();

        sheet.GetProperty("equity").EnumerateArray()
            .Select(e => e.GetProperty("code").GetString())
            .Should().Contain("3000");

        sheet.GetProperty("totalEquity").GetDecimal().Should().BeGreaterThanOrEqualTo(50000m);
        sheet.GetProperty("balances").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task A_floorplanned_car_is_owed_to_the_lender_rather_than_taken_from_the_bank()
    {
        // Most dealers floorplan their stock. Until 2026-09-11 every purchase
        // credited Cash, which is what drove the seeded dealership's bank to
        // minus $2.5M the moment stock became a real asset.
        var rooftopId = await RooftopIdAsync("NAG-01");
        var suffix = $"{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var stockNumber = $"F{suffix}";

        var vehicleId = await CreatedIdAsync("/api/v1/vehicles", new
        {
            vin = $"FLRPLN{suffix}"[..13], modelYear = 2024, make = "Skoda", model = "Octavia",
            vinExceptionReason = "Synthetic VIN for a ledger test.",
        });

        await CreatedIdAsync("/api/v1/inventory", new
        {
            vehicleId, rooftopId, stockNumber,
            costAmount = 21000m, costCurrency = "USD",
            floorplanned = true,
        });

        var purchase = await EntryForReferenceAsync(stockNumber);

        SumFor(purchase, "1300", "debit").Should().Be(21000m, because: "the car is still an asset");
        SumFor(purchase, "2000", "credit").Should().Be(21000m,
            because: "the lender paid for it, and is owed until it sells");
        SumFor(purchase, "1000", "credit").Should().Be(0m, because: "the bank was never touched");
    }


    [Fact]
    public async Task Every_expense_account_appears_on_the_profit_and_loss()
    {
        // The hole this closes was found by adding the two reports up by hand and
        // noticing they disagreed by $663.60. The first version of the report
        // named five overhead accounts explicitly, so 5400 Internal service
        // charge - an expense, and not a department's cost of sales - appeared on
        // no part of the profit and loss at all. The seeded dealership had $1,196
        // in it and the report did not mention it.
        //
        // Asked of the CHART rather than of a list, so a new expense account is on
        // the report the day somebody adds it.
        using var chart = await SendAsync(HttpMethod.Get, "/api/v1/accounting/accounts", Manager);
        var expenseCodes = (await chart.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray()
            .Where(a => a.GetProperty("kind").GetString() == "Expense")
            .Select(a => a.GetProperty("code").GetString()!)
            .ToList();

        using var response = await SendAsync(
            HttpMethod.Get, "/api/v1/accounting/profit-and-loss", Manager);

        var report = await response.Content.ReadFromJsonAsync<JsonElement>();
        var onReport = report.GetProperty("expenses").EnumerateArray()
            .Select(e => e.GetProperty("code").GetString()!)
            .ToList();

        // Cost of sales is inside the departmental gross and must not be below the
        // line as well. Everything else has to be somewhere a person can see it.
        var costOfSales = new[] { "5000", "5300", "5500" };
        var shouldBeListed = expenseCodes.Where(c => !costOfSales.Contains(c)).ToList();

        onReport.Should().BeEquivalentTo(shouldBeListed,
            because: "an expense account on no report is money nobody can account for");
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

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Rows().ToList();
    }

    private async Task<JsonElement> EntryDetailAsync(string entryId)
    {
        using var response = await SendAsync(HttpMethod.Get, $"{Journal}/{entryId}", Manager);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private sealed record Sale(string DealId, string CustomerId);

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
        var rows = summaries.Rows();
        rows.Should().NotBeEmpty(because: $"{reference} should have posted");

        var id = rows[0].GetProperty("id").GetString()!;

        using var detail = await SendAsync(HttpMethod.Get, $"{Journal}/{id}", Manager);
        return await detail.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> EntryForAsync(string dealId)
    {
        using var response = await SendAsync(HttpMethod.Get, $"{Journal}?reference={dealId}", Manager);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var summaries = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = summaries.Rows()[0].GetProperty("id").GetString()!;

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
        decimal cost = 18000m,

        // Given, when a test needs TWO bills for the SAME person — which is what
        // a credit from one bill paying another one requires. Left null the
        // helper invents a fresh customer, so every other test stays isolated.
        string? buyer = null)
    {
        var rooftopId = await RooftopIdAsync("NAG-01");
        var suffix = $"{Guid.NewGuid():N}"[..8].ToUpperInvariant();

        var customerId = buyer ?? await CreatedIdAsync("/api/v1/customers", new
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

        return new Sale(dealId, customerId);
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
        return results.Rows().Select(e => e.GetProperty("id").GetString()!).ToList();
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
