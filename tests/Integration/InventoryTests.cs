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

        using var response = await PostAsync($"{Inventory}/{unitId}/status", Manager,
            new { status = "Sold" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Available",
            because: "the message must tell the user what they can do instead");
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
