// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   InventoryTests — proves a vehicle can be recorded and found, and that one
//   rooftop cannot see or move another rooftop's stock.
//
// Usage:
//   Runs with the normal test suite; needs a reachable SQL engine.
//
// Coding Instructions:
//   The scope tests must fail if the rooftop check in InventoryService is
//   removed — that is the point of them. Cover both routes: filtering the
//   list is not enough if the unit can still be fetched by id.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using FluentAssertions;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class InventoryTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string Vehicles = "/api/v1/vehicles";
    private const string Inventory = "/api/v1/inventory";

    private const string Manager = DevelopmentSeeder.DevUsers.OrganizationWideEmail;
    private const string Advisor = DevelopmentSeeder.DevUsers.FirstRooftopOnlyEmail;
    private const string Nobody = DevelopmentSeeder.DevUsers.UnassignedEmail;

    /// <summary>
    /// The salesperson role at the first rooftop, narrowed with
    /// <c>OwnRecordsOnly</c> (ADR-030). Here to prove that narrowing does NOT
    /// take stock away — see <c>An_own_work_only_salesperson_can_still_move_stock</c>.
    /// </summary>
    private const string OwnWorkOnly = DevelopmentSeeder.DevUsers.OwnWorkOnlyEmail;

    private readonly HostFixture _fixture = fixture;

    // --- vehicles ----------------------------------------------------------

    [Fact]
    public async Task A_vehicle_can_be_recorded_and_read_back()
    {
        var vin = UniqueVin();

        using var created = await PostAsync(Vehicles, Manager, new
        {
            vin,
            modelYear = 2021,
            make = "Toyota",
            model = "RAV4",
            trim = "XLE",
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var detail = await created.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("displayName").GetString().Should().Be("2021 Toyota RAV4 XLE");
        detail.GetProperty("vin").GetString().Should().Be(vin);
    }

    [Fact]
    public async Task A_vehicle_can_be_found_by_part_of_its_vin()
    {
        // Staff read the last few characters off a windscreen far more often
        // than they type all seventeen.
        var vin = UniqueVin();
        await AddVehicleAsync(vin);

        var found = await SearchVehiclesAsync(vin[^6..], Manager);

        found.Should().Contain(vin);
    }

    [Fact]
    public async Task A_vin_typed_in_lower_case_finds_the_same_vehicle()
    {
        var vin = UniqueVin();
        await AddVehicleAsync(vin);

        var found = await SearchVehiclesAsync(vin.ToLowerInvariant(), Manager);

        found.Should().Contain(vin);
    }

    [Fact]
    public async Task An_unusual_vin_is_refused_until_a_reason_is_given_then_accepted()
    {
        using var refused = await PostAsync(Vehicles, Manager, new
        {
            vin = "TRAILER-1975-A",
            modelYear = 1975,
            make = "Wells Cargo",
            model = "Utility Trailer",
        });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("reason",
            because: "the message must say how to proceed, not just refuse");

        using var accepted = await PostAsync(Vehicles, Manager, new
        {
            vin = $"TRAILER-{Guid.NewGuid():N}"[..14],
            modelYear = 1975,
            make = "Wells Cargo",
            model = "Utility Trailer",
            vinExceptionReason = "Pre-1981 trailer; number read from the frame plate.",
        });

        accepted.StatusCode.Should().Be(HttpStatusCode.Created);
        var detail = await accepted.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("vinExceptionReason").GetString().Should().Contain("frame plate");
    }

    [Fact]
    public async Task An_advisor_can_read_vehicles_but_cannot_record_one()
    {
        using var read = await SendAsync(HttpMethod.Get, Vehicles, Advisor);
        read.StatusCode.Should().Be(HttpStatusCode.OK);

        using var write = await PostAsync(Vehicles, Advisor, new
        {
            vin = UniqueVin(),
            modelYear = 2020,
            make = "Honda",
            model = "Civic",
        });

        write.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "recording a vehicle is a stock action, gated by Inventory.Manage");
    }

    // --- inventory, and the rooftop boundary -------------------------------

    [Fact]
    public async Task A_unit_can_be_received_and_listed_by_stock_number()
    {
        var rooftop = await RooftopIdAsync("NAG-01");
        var stock = UniqueStock();

        using var received = await ReceiveAsync(Manager, rooftop, stock);
        received.StatusCode.Should().Be(HttpStatusCode.Created);

        var detail = await received.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("status").GetString().Should().Be("Incoming");
        detail.GetProperty("history").EnumerateArray().Should().ContainSingle();

        var listed = await ListAsync($"{Inventory}?stock={stock}", Manager);
        listed.Should().ContainSingle().Which.Should().Be(stock);
    }

    [Theory]
    [InlineData(24500, "USD")]
    [InlineData(0, "EUR")]
    [InlineData(0, "USD")]
    [InlineData(null, null)]
    public async Task The_stock_list_carries_the_recorded_cost_without_turning_missing_into_zero(
        int? amount, string? currency)
    {
        // A zero-cost EUR unit proves currency preservation without introducing
        // a second posted currency into the shared ledger fixture. A positive
        // EUR purchase made six unrelated reports correctly refuse to combine
        // currencies; the display's positive EUR case lives in the UI test.
        var stock = UniqueStock();
        using var received = await PostAsync(Inventory, Manager, new
        {
            vehicleId = await AddVehicleAsync(UniqueVin()),
            rooftopId = await RooftopIdAsync("NAG-01"),
            stockNumber = stock,
            costAmount = amount,
            costCurrency = currency,
        });
        received.StatusCode.Should().Be(HttpStatusCode.Created);

        // Read as the manager since 2026-09-29: cost is a field right now
        // (ADR-029) and the advisor no longer holds it, which the test below
        // asserts. This one is about the LIST carrying the nullable pair rather
        // than inventing a zero or making the caller open every car — so it uses
        // an identity that can see the figures at all.
        var page = await PageAsync($"{Inventory}?stock={stock}", Manager);
        var row = page.Rows().Should().ContainSingle().Subject;
        if (amount is null)
        {
            row.GetProperty("costAmount").ValueKind.Should().Be(JsonValueKind.Null);
        }
        else
        {
            row.GetProperty("costAmount").GetDecimal().Should().Be(amount.Value);
        }

        row.GetProperty("costCurrency").GetString().Should().Be(currency);
        (await PageAsync($"{Inventory}?stock={stock}&rooftopId={await RooftopIdAsync("NAG-02")}", Manager))
            .Rows().Should().BeEmpty();
    }

    /// <summary>
    /// The first field right in the product (ADR-029). The advisor may read this
    /// car — same record, same rooftop, same list — and may not read what it
    /// cost. Asserted on the LIST and the DETAIL, because they are built by two
    /// different methods and gating one and forgetting the other is the whole
    /// failure mode this is meant to catch.
    /// </summary>
    [Fact]
    public async Task What_the_lot_paid_is_withheld_from_somebody_who_may_not_see_it()
    {
        var stock = UniqueStock();
        using var received = await PostAsync(Inventory, Manager, new
        {
            vehicleId = await AddVehicleAsync(UniqueVin()),
            rooftopId = await RooftopIdAsync("NAG-01"),
            stockNumber = stock,
            costAmount = 24500,
            costCurrency = "USD",
        });
        received.StatusCode.Should().Be(HttpStatusCode.Created);

        var unitId = (await received.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        // The manager sees the figure, so the test cannot pass by the car simply
        // having no cost recorded.
        var seen = await PageAsync($"{Inventory}?stock={stock}", Manager);
        seen.Rows().Should().ContainSingle().Which
            .GetProperty("costAmount").GetDecimal().Should().Be(24500m);

        var hidden = await PageAsync($"{Inventory}?stock={stock}", Advisor);
        var row = hidden.Rows().Should().ContainSingle().Subject;

        row.GetProperty("stockNumber").GetString().Should().Be(stock,
            because: "the advisor may read the car — it is only the money that is withheld");

        foreach (var field in new[] { "costAmount", "costCurrency", "reconditioningAmount", "bookValueAmount" })
        {
            row.GetProperty(field).ValueKind.Should().Be(JsonValueKind.Null,
                because: $"{field} is what the lot paid, and the advisor does not hold Profitability.Read");
        }

        using var detail = await SendAsync(HttpMethod.Get, $"{Inventory}/{unitId}", Advisor);
        detail.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "the record is still readable; this is a field right, not a record right");

        var body = await detail.Content.ReadFromJsonAsync<JsonElement>();

        foreach (var field in new[] { "costAmount", "costCurrency", "reconditioningAmount", "bookValueAmount" })
        {
            body.GetProperty(field).ValueKind.Should().Be(JsonValueKind.Null);
        }

        body.GetProperty("reconditioning").EnumerateArray().Should().BeEmpty(
            because: "the charges behind the figure are the same disclosure as the figure");
    }

    [Fact]
    public async Task The_cars_past_the_first_page_can_actually_be_reached()
    {
        // Before 2026-09-12 this list took a limit and had no offset at all, so
        // a dealership with more cars than the cap could not see the rest of
        // them by any route the application offered.
        var rooftop = await RooftopIdAsync("NAG-01");

        using var one = await ReceiveAsync(Manager, rooftop, UniqueStock());
        using var two = await ReceiveAsync(Manager, rooftop, UniqueStock());
        one.StatusCode.Should().Be(HttpStatusCode.Created);
        two.StatusCode.Should().Be(HttpStatusCode.Created);

        var first = await PageAsync($"{Inventory}?limit=1&offset=0", Manager);
        var next = await PageAsync($"{Inventory}?limit=1&offset=1", Manager);

        first.Rows().Should().ContainSingle();
        next.Rows().Should().ContainSingle();

        first.Rows()[0].GetProperty("id").GetGuid()
            .Should().NotBe(next.Rows()[0].GetProperty("id").GetGuid(),
                because: "the second page is the next row, not the same one again");

        // The total is counted over the whole filtered set, not over the page.
        // Without it the screen can only say "the first 50, there may be more",
        // which is what it used to say.
        first.Total().Should().BeGreaterThan(1);
        first.Total().Should().Be(next.Total());
        first.Offset().Should().Be(0);
        next.Offset().Should().Be(1);
    }

    [Fact]
    public async Task Asking_for_more_rows_than_the_cap_is_answered_with_the_cap()
    {
        // A list endpoint must not be a way to ask the database for everything.
        var page = await PageAsync($"{Inventory}?limit=100000", Manager);

        page.GetProperty("limit").GetInt32().Should().Be(200);
    }

    /// <summary>One page, as the endpoint returns it, for asserting on its counts.</summary>
    private async Task<JsonElement> PageAsync(string path, string email)
    {
        using var response = await SendAsync(HttpMethod.Get, path, email);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task A_stock_number_cannot_be_used_twice_at_the_same_rooftop()
    {
        var rooftop = await RooftopIdAsync("NAG-01");
        var stock = UniqueStock();

        using var first = await ReceiveAsync(Manager, rooftop, stock);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        using var again = await ReceiveAsync(Manager, rooftop, stock);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task The_same_stock_number_is_allowed_at_a_different_rooftop()
    {
        // Two locations each number their own cars; "A1234" is not one car.
        var stock = UniqueStock();

        using var first = await ReceiveAsync(Manager, await RooftopIdAsync("NAG-01"), stock);
        using var second = await ReceiveAsync(Manager, await RooftopIdAsync("NAG-02"), stock);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_rooftop_scoped_user_does_not_see_another_rooftops_stock()
    {
        var mine = UniqueStock();
        var theirs = UniqueStock();
        await ReceiveAsync(Manager, await RooftopIdAsync("NAG-01"), mine);
        await ReceiveAsync(Manager, await RooftopIdAsync("NAG-02"), theirs);

        // Asked for by stock number rather than read off a page. A capped,
        // stock-number-ordered list makes "is it in the first 200?" depend on how
        // much data happens to exist, which is not what this test is about.
        var ownStock = await ListAsync($"{Inventory}?stock={mine}", Advisor);
        var siblingStock = await ListAsync($"{Inventory}?stock={theirs}", Advisor);

        ownStock.Should().ContainSingle().Which.Should().Be(mine);
        siblingStock.Should().BeEmpty(
            because: "the response must never carry another rooftop's stock, by any route");
    }

    [Fact]
    public async Task A_rooftop_scoped_user_is_refused_another_rooftops_unit_by_direct_id()
    {
        // Filtering the list is not enough: addressing the unit directly must
        // also be refused.
        var unitId = await ReceiveIdAsync(Manager, await RooftopIdAsync("NAG-02"), UniqueStock());

        using var response = await SendAsync(HttpMethod.Get, $"{Inventory}/{unitId}", Advisor);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_rooftop_scoped_user_is_refused_a_list_filtered_to_another_rooftop()
    {
        var sibling = await RooftopIdAsync("NAG-02");

        using var response = await SendAsync(HttpMethod.Get, $"{Inventory}?rooftopId={sibling}", Advisor);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_unknown_unit_is_refused_the_same_way_as_an_unauthorized_one()
    {
        // Identical answers, so a caller cannot probe for what exists elsewhere.
        using var unknown = await SendAsync(HttpMethod.Get, $"{Inventory}/{Guid.NewGuid()}", Advisor);
        var siblingUnit = await ReceiveIdAsync(Manager, await RooftopIdAsync("NAG-02"), UniqueStock());
        using var unauthorized = await SendAsync(HttpMethod.Get, $"{Inventory}/{siblingUnit}", Advisor);

        unknown.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        unauthorized.StatusCode.Should().Be(unknown.StatusCode);
    }

    [Fact]
    public async Task A_user_with_no_assignment_cannot_reach_inventory_at_all()
    {
        using var response = await SendAsync(HttpMethod.Get, Inventory, Nobody);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "no assignment must mean no access, never unfiltered access");
    }

    // --- status ------------------------------------------------------------

    [Fact]
    public async Task A_unit_moves_through_its_statuses_and_keeps_the_history()
    {
        var unitId = await ReceiveIdAsync(Manager, await RooftopIdAsync("NAG-01"), UniqueStock());

        using var reconditioning = await PostAsync($"{Inventory}/{unitId}/status", Manager,
            new { status = "Reconditioning", note = "Awaiting tyres." });
        reconditioning.StatusCode.Should().Be(HttpStatusCode.OK);

        using var available = await PostAsync($"{Inventory}/{unitId}/status", Manager,
            new { status = "Available" });
        available.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await available.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("status").GetString().Should().Be("Available");
        detail.GetProperty("history").EnumerateArray().Should().HaveCount(3,
            because: "every move a car makes stays on its record");
    }

    [Fact]
    public async Task A_move_the_life_cycle_does_not_allow_is_refused_with_a_reason()
    {
        var unitId = await ReceiveIdAsync(Manager, await RooftopIdAsync("NAG-01"), UniqueStock());

        // Probes with OnHold, not Sold. It used to use Sold, which stopped
        // testing this rule on 2026-10-19: Sold is now refused by its own guard
        // before the life cycle is ever consulted, so the assertion below passed
        // for the wrong reason and the life-cycle message went uncovered. An
        // Incoming car can become Reconditioning, Available or Removed — being
        // spoken for before it has arrived is not one of them.
        using var response = await PostAsync($"{Inventory}/{unitId}/status", Manager,
            new { status = "OnHold" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Available",
            because: "the message must tell the user what they can do instead");
    }

    [Fact]
    public async Task A_person_cannot_sell_a_car_by_typing_Sold_onto_it()
    {
        // The test above looks like it covers this and does not: it posts Sold on
        // an INCOMING car, which the life cycle refuses on its own. Available ->
        // Sold and OnHold -> Sold are both legal moves, so until 2026-10-19
        // anybody holding Inventory.Manage could sell a car with one request --
        // no deal, no customer, and no delivery posting.
        //
        // What that cost: the car left the stock list and its carrying value
        // stayed on the balance sheet. Nothing relieved 1300, because the thing
        // that relieves 1300 is PostDeliveryAsync, and no deal had been
        // delivered. The same failure class as the reconditioning bug found on
        // 2026-09-19 -- an account that never comes back to zero -- and equally
        // invisible to the balancing checks, because no entry was wrong. There
        // was simply no entry.
        //
        // The screen never offered the button, which is why this survived: the
        // rule lived in InventoryPage.movesFrom() and nowhere on the server. The
        // exact gap ADR-025 names -- hiding a control is a courtesy, the
        // endpoint behind it still has to refuse.
        var unitId = await ReceiveIdAsync(Manager, await RooftopIdAsync("NAG-01"), UniqueStock());

        using (var available = await PostAsync($"{Inventory}/{unitId}/status", Manager,
            new { status = "Available" }))
        {
            available.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var sold = await PostAsync($"{Inventory}/{unitId}/status", Manager,
            new { status = "Sold" });

        sold.StatusCode.Should().Be(HttpStatusCode.Conflict,
            because: "a car is sold by delivering a deal, which is what posts the sale");
        (await sold.Content.ReadAsStringAsync()).Should().Contain("deal",
            because: "the refusal has to say where to go instead, or it reads as a bug");

        // And the car is still on the lot afterwards. Asserted separately
        // because a refusal that still moved the record would be worse than one
        // that did not refuse at all.
        using var after = await SendAsync(HttpMethod.Get, $"{Inventory}/{unitId}", Manager);
        (await after.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("status").GetString().Should().Be("Available");
    }

    [Fact]
    public async Task An_advisor_can_see_stock_but_cannot_move_it()
    {
        var unitId = await ReceiveIdAsync(Manager, await RooftopIdAsync("NAG-01"), UniqueStock());

        using var read = await SendAsync(HttpMethod.Get, $"{Inventory}/{unitId}", Advisor);
        read.StatusCode.Should().Be(HttpStatusCode.OK);

        using var move = await PostAsync($"{Inventory}/{unitId}/status", Advisor,
            new { status = "Available" });

        move.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "Inventory.Read and Inventory.Manage are checked separately");
    }

    // --- availableMoves: a button a caller would be refused is never offered --

    [Fact]
    public async Task AvailableMoves_offers_what_the_life_cycle_allows_from_here()
    {
        var unitId = await ReceiveIdAsync(Manager, await RooftopIdAsync("NAG-01"), UniqueStock());

        using (var reconditioning = await PostAsync($"{Inventory}/{unitId}/status", Manager,
            new { status = "Reconditioning" }))
        {
            reconditioning.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await MovesAsync(unitId, Manager)).Should().BeEquivalentTo(["Available", "OnHold", "Removed"]);
    }

    [Fact]
    public async Task AvailableMoves_never_offers_Sold_from_any_state_a_car_can_be_moved_from()
    {
        // The browser used to decide this, in InventoryPage.movesFrom(), which is
        // why POST /status accepted Sold from anybody for months. Asserted from
        // every state that can reach Sold legally, not just one: Available and
        // OnHold are the two, and a test that only opened a Reconditioning car
        // would stay green if the subtraction were dropped.
        var unitId = await ReceiveIdAsync(Manager, await RooftopIdAsync("NAG-01"), UniqueStock());

        foreach (var state in new[] { "Available", "OnHold" })
        {
            using var moved = await PostAsync($"{Inventory}/{unitId}/status", Manager,
                new { status = state });
            moved.StatusCode.Should().Be(HttpStatusCode.OK, because: await moved.Content.ReadAsStringAsync());

            (await MovesAsync(unitId, Manager)).Should().NotContain("Sold",
                because: $"a {state} car is sold by delivering its deal, not from the stock screen");
        }
    }

    [Fact]
    public async Task AvailableMoves_is_empty_for_a_caller_who_only_holds_read()
    {
        // The advisor holds Inventory.Read and not Inventory.Manage, so the two
        // halves of this assertion are the whole point: the moves are absent
        // from the record AND the endpoint refuses one that is posted anyway.
        // Hiding a control is a courtesy; the refusal is the control (ADR-025).
        var unitId = await ReceiveIdAsync(Manager, await RooftopIdAsync("NAG-01"), UniqueStock());

        (await MovesAsync(unitId, Advisor)).Should().BeEmpty();

        using var refused = await PostAsync($"{Inventory}/{unitId}/status", Advisor,
            new { status = "Available" });
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_own_work_only_salesperson_can_still_move_stock()
    {
        // THIS TEST EXISTS TO STOP A TIDY-UP. AvailableMoves uses
        // AuthorizedScope.Covers, not Allows, and the difference is not cosmetic:
        // a car names nobody, and Allows(rooftopId, null) deliberately REFUSES a
        // caller narrowed to their own records. Switching to Allows "for
        // consistency with Deals and Leads" would hide every move button from
        // this account, for every car on the lot, and the endpoint would go on
        // allowing the move — so the screen would be wrong and silent.
        //
        // ADR-030 names receiving stock as its own example of a question with no
        // owner to ask it with. ownwork@dev.local differs from sales@dev.local by
        // the OwnRecordsOnly narrowing and nothing else, which is what makes this
        // a test of the dimension rather than of a role.
        var unitId = await ReceiveIdAsync(Manager, await RooftopIdAsync("NAG-01"), UniqueStock());

        (await MovesAsync(unitId, OwnWorkOnly)).Should().Contain("Available",
            because: "a car is not somebody's own record, so being limited to your own work "
                + "does not take stock away from you");

        using var moved = await PostAsync($"{Inventory}/{unitId}/status", OwnWorkOnly,
            new { status = "Available" });
        moved.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "the offer and the act have to agree, or one of them is a lie");
    }

    [Fact]
    public async Task AvailableMoves_is_empty_for_a_car_that_has_been_removed()
    {
        var unitId = await ReceiveIdAsync(Manager, await RooftopIdAsync("NAG-01"), UniqueStock());

        using (var removed = await PostAsync($"{Inventory}/{unitId}/status", Manager,
            new { status = "Removed" }))
        {
            removed.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await MovesAsync(unitId, Manager)).Should().BeEmpty(
            because: "Removed is the end of the line; a car that comes back is received again");
    }

    [Fact]
    public async Task A_sold_car_is_not_put_back_on_the_lot_by_hand()
    {
        // The other half of the rule, and the half nobody had written down. The
        // life cycle allows Sold -> Available, under a comment about deals
        // falling through -- but DealStatusRules has [Delivered] = [], so a
        // delivered deal can never be cancelled and Deals only ever returns a
        // car to stock from OnHold. Nothing in the solution moves a car out of
        // Sold.
        //
        // So a person doing it by hand would put the car back on the forecourt
        // while the ledger still carried its delivery: 1300 relieved, the sale
        // booked, the car for sale again. The mirror image of selling one by
        // hand, and found the same way -- the browser's movesFrom() returned
        // nothing for a Sold car, so the screen had the rule and the server did
        // not.
        var rooftop = await RooftopIdAsync("NAG-01");
        var unitId = await ReceiveIdAsync(Manager, rooftop, UniqueStock());

        using (var available = await PostAsync($"{Inventory}/{unitId}/status", Manager,
            new { status = "Available" }))
        {
            available.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Sold the only way a car can be: by delivering a deal.
        await DeliverADealForAsync(unitId, rooftop);

        (await MovesAsync(unitId, Manager)).Should().BeEmpty(
            because: "a delivered deal cannot be cancelled, so there is nothing to undo this with");

        using var back = await PostAsync($"{Inventory}/{unitId}/status", Manager,
            new { status = "Available" });

        back.StatusCode.Should().Be(HttpStatusCode.Conflict,
            because: "the car would be for sale again while the books still held the sale");

        using var after = await SendAsync(HttpMethod.Get, $"{Inventory}/{unitId}", Manager);
        (await after.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("status").GetString().Should().Be("Sold");
    }

    /// <summary>
    /// Takes a car all the way to Sold through the only door there is, so a test
    /// about a sold car is about one the system really sold.
    /// </summary>
    private async Task DeliverADealForAsync(string unitId, string rooftop)
    {
        using var customer = await PostAsync("/api/v1/customers", Manager, new
        {
            kind = "Person", firstName = "Sold", lastName = $"Car{Guid.NewGuid():N}"[..12],
        });
        var customerId = (await customer.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString()!;

        using var started = await PostAsync("/api/v1/deals", Manager, new
        {
            rooftopId = rooftop, customerId, inventoryUnitId = unitId, currency = "USD",
            salespersonUserId = DevelopmentSeeder.DevUsers.Salesperson,
        });
        started.StatusCode.Should().Be(HttpStatusCode.Created,
            because: await started.Content.ReadAsStringAsync());

        var dealId = (await started.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString()!;

        using (var terms = await PostAsync($"/api/v1/deals/{dealId}/terms", Manager, new
        {
            charges = new object[]
            {
                new { kind = "VehiclePrice", description = "The car", amount = 21_000m },
            },
        }))
        {
            terms.StatusCode.Should().Be(HttpStatusCode.OK, because: await terms.Content.ReadAsStringAsync());
        }

        foreach (var status in new[] { "Submitted", "Approved", "Delivered" })
        {
            using var moved = await PostAsync($"/api/v1/deals/{dealId}/status", Manager, new { status });
            moved.StatusCode.Should().Be(HttpStatusCode.OK, because: await moved.Content.ReadAsStringAsync());
        }
    }

    private async Task<IReadOnlyList<string>> MovesAsync(string unitId, string email)
    {
        using var response = await SendAsync(HttpMethod.Get, $"{Inventory}/{unitId}", email);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("availableMoves")
            .EnumerateArray()
            .Select(move => move.GetString()!)
            .ToList();
    }

    [Fact]
    public async Task Stock_does_not_leak_between_dealer_organizations()
    {
        var stock = UniqueStock();
        await ReceiveAsync(Manager, await RooftopIdAsync("NAG-01"), stock);

        // Same stock number, other organization, other database.
        using var client = _fixture.CreateClient();
        var token = await _fixture.TokenForAsync(Manager, "citymotors");
        using var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri($"{Inventory}?stock={stock}", UriKind.Relative));
        request.Headers.Add("X-Tenant", "citymotors");
        request.Headers.Add("Cookie", $"dfoss_session={token}");

        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var found = await response.Content.ReadFromJsonAsync<JsonElement>();
        found.Rows().Should().BeEmpty(
            because: "a unit belongs to one dealer organization's database");
    }

    [Fact]
    public async Task A_unit_cannot_be_received_for_a_vehicle_that_was_never_recorded()
    {
        var rooftop = await RooftopIdAsync("NAG-01");

        using var response = await PostAsync(Inventory, Manager, new
        {
            vehicleId = Guid.NewGuid(),
            rooftopId = rooftop,
            stockNumber = UniqueStock(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The history must read as a sequence of events even when the clock cannot
    /// tell the events apart.
    ///
    /// Two moves recorded in the same instant is not a contrived case: the seeder
    /// produced one, and it rendered a car that was available before it arrived.
    /// Ordering on OccurredAt alone leaves tied rows in whatever order the store
    /// feels like returning, so this test flattens all three timestamps to one
    /// value and then asks whether the chain still holds — each entry leaving the
    /// status the entry before it arrived at.
    ///
    /// It fails without the Sequence tiebreak.
    /// </summary>
    [Fact]
    public async Task The_history_still_reads_in_order_when_two_moves_share_an_instant()
    {
        var unitId = await ReceiveIdAsync(Manager, await RooftopIdAsync("NAG-01"), UniqueStock());

        using (var reconditioning = await PostAsync($"{Inventory}/{unitId}/status", Manager,
            new { status = "Reconditioning", note = "Awaiting tyres." }))
        {
            reconditioning.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using (var available = await PostAsync($"{Inventory}/{unitId}/status", Manager,
            new { status = "Available" }))
        {
            available.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        await FlattenHistoryTimestampsAsync(Guid.Parse(unitId));

        using var read = await SendAsync(HttpMethod.Get, $"{Inventory}/{unitId}", Manager);
        read.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await read.Content.ReadFromJsonAsync<JsonElement>();
        var history = detail.GetProperty("history").EnumerateArray().ToList();
        history.Should().HaveCount(3);

        history[0].GetProperty("fromStatus").ValueKind.Should().Be(JsonValueKind.Null,
            because: "a car has to arrive before it can move anywhere");

        for (var i = 1; i < history.Count; i++)
        {
            history[i].GetProperty("fromStatus").GetString().Should().Be(
                history[i - 1].GetProperty("toStatus").GetString(),
                because: "each move leaves the status the move before it arrived at");
        }
    }

    // --- helpers -----------------------------------------------------------

    /// <summary>
    /// Gives every history row for one unit the same OccurredAt, reproducing what
    /// two writes in a single tick produce. Raw SQL because the row is append-only
    /// and TenantDb is right to refuse the update (ADR-016).
    /// </summary>
    private static async Task FlattenHistoryTimestampsAsync(Guid unitId)
    {
        await using var connection = new SqlConnection(HostFixture.TenantConnectionString(Tenant));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = """
            UPDATE [vehicles].[InventoryStatusHistory]
               SET OccurredAt = (SELECT MIN(OccurredAt)
                                   FROM [vehicles].[InventoryStatusHistory]
                                  WHERE InventoryUnitId = @unit)
             WHERE InventoryUnitId = @unit;
            """;

        command.Parameters.AddWithValue("@unit", unitId);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>A well-formed VIN unique per run, so tests do not collide.</summary>
    private static string UniqueVin()
    {
        var body = Guid.NewGuid().ToString("N").ToUpperInvariant()
            .Replace("I", "1", StringComparison.Ordinal)
            .Replace("O", "0", StringComparison.Ordinal)
            .Replace("Q", "9", StringComparison.Ordinal);

        return body[..17];
    }

    private static string UniqueStock() => $"T{Guid.NewGuid():N}"[..10].ToUpperInvariant();

    private async Task<string> AddVehicleAsync(string vin)
    {
        using var response = await PostAsync(Vehicles, Manager, new
        {
            vin,
            modelYear = 2021,
            make = "Toyota",
            model = "RAV4",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        return detail.GetProperty("id").GetString()!;
    }

    private async Task<HttpResponseMessage> ReceiveAsync(string email, string rooftopId, string stockNumber)
    {
        var vehicleId = await AddVehicleAsync(UniqueVin());

        return await PostAsync(Inventory, email, new
        {
            vehicleId,
            rooftopId,
            stockNumber,
            costAmount = 24500m,
            costCurrency = "USD",
        });
    }

    private async Task<string> ReceiveIdAsync(string email, string rooftopId, string stockNumber)
    {
        using var response = await ReceiveAsync(email, rooftopId, stockNumber);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        return detail.GetProperty("id").GetString()!;
    }

    private async Task<IReadOnlyList<string>> ListAsync(string path, string email)
    {
        using var response = await SendAsync(HttpMethod.Get, path, email);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var results = await response.Content.ReadFromJsonAsync<JsonElement>();
        return results.Rows()
            .Select(u => u.GetProperty("stockNumber").GetString()!)
            .ToList();
    }

    private async Task<IReadOnlyList<string>> SearchVehiclesAsync(string term, string email)
    {
        using var response = await SendAsync(
            HttpMethod.Get, $"{Vehicles}?search={Uri.EscapeDataString(term)}", email);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var results = await response.Content.ReadFromJsonAsync<JsonElement>();
        return results.Rows()
            .Select(v => v.GetProperty("vin").GetString()!)
            .ToList();
    }

    /// <summary>Reads a rooftop id through the organization-wide user.</summary>
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
