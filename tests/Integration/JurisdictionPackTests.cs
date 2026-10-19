// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   JurisdictionPackTests — tax worked out from a pack, end to end: a deal in a
//   supported jurisdiction, the provenance that lands on the line, and the
//   property that matters most — that updating a pack does not change tax
//   already agreed.
//
// Usage:
//   dotnet test
//
// Coding Instructions:
//   THE FREEZE TEST IS THE ONE THIS FILE EXISTS FOR. Everything else here could
//   be got wrong and fixed in an afternoon; rewriting the tax on deals customers
//   have already signed is not recoverable, because the invoices are already out
//   of the building. If that test ever becomes awkward, the design is wrong, not
//   the test.
//
//   These run against the development seed, where "zz-example" is a FICTIONAL
//   jurisdiction (country ZZ, which ISO 3166 reserves for private use) that is
//   reviewed and enabled. The shipped Indiana pack is deliberately Draft and is
//   asserted to stay that way — a test that quietly marked it supported would
//   defeat the gate it is testing.

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class JurisdictionPackTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string Deals = "/api/v1/deals";
    private const string Packs = "/api/v1/admin/jurisdiction-packs";

    private const string Manager = DevelopmentSeeder.DevUsers.OrganizationWideEmail;

    private readonly HostFixture _fixture = fixture;

    /// <summary>One pack per country: the seeded ZZ example, plus the QQ pack the freeze test loads.</summary>
    private static readonly string[] EnabledForFreezeTest = ["zz-example", "qq-freeze"];

    [Fact]
    public async Task A_deal_in_a_supported_jurisdiction_is_taxed_by_the_pack_and_says_which_one()
    {
        var dealId = await APricedDealAsync();
        await RegisterAtAsync(dealId, "ZZ", "EX", "Demo");

        using var response = await PostAsync($"{Deals}/{dealId}/tax/from-pack", Manager, new { });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("outcome").GetString().Should().Be("Resolved");
        body.GetProperty("packId").GetString().Should().Be("zz-example");
        body.GetProperty("packVersion").GetInt32().Should().Be(1);

        var lines = body.GetProperty("deal").GetProperty("taxLines").EnumerateArray().ToList();

        // Two authorities taxing one car, which is the case a "most specific
        // wins" rule would silently undercharge.
        lines.Should().HaveCount(2);

        // 20,000 car + 500 documentation fee (taxable here) + 100 registration
        // fee (not taxable) - 5,000 trade-in allowance = 15,500.
        foreach (var line in lines)
        {
            line.GetProperty("basis").GetDecimal().Should().Be(15500m);
            line.GetProperty("provenance").GetString().Should().Be("Pack");
            line.GetProperty("packId").GetString().Should().Be("zz-example");
            line.GetProperty("packVersion").GetInt32().Should().Be(1);
        }

        lines[0].GetProperty("rate").GetDecimal().Should().Be(0.05m);
        lines[0].GetProperty("amount").GetDecimal().Should().Be(775m);
        lines[1].GetProperty("rate").GetDecimal().Should().Be(0.015m);
        lines[1].GetProperty("amount").GetDecimal().Should().Be(232.50m);

        body.GetProperty("deal").GetProperty("taxTotal").GetDecimal().Should().Be(1007.50m);
    }

    [Fact]
    public async Task Updating_a_pack_does_not_change_tax_already_agreed()
    {
        // Its own pack and its own country, rather than reusing the seeded
        // example, because this test deliberately loads a SECOND generation and
        // reviews it — and a pack that resolves is shared by the whole run. The
        // first version of this test bumped the example pack and the test
        // asserting version 1 elsewhere started reading version 2. That was the
        // suite leaking, not the product, but a test that can break a sibling is
        // a test worth isolating.
        using var first = await LoadPackAsync(FreezePackAtVersion(1, stateRate: "0.05"));
        first.StatusCode.Should().Be(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());

        using var reviewedFirst = await AdminPostAsync($"{Packs}/qq-freeze/1/review");
        reviewedFirst.StatusCode.Should().Be(HttpStatusCode.OK, await reviewedFirst.Content.ReadAsStringAsync());

        // The posture: one pack per country, so the fictional QQ pack sits
        // alongside the seeded ZZ one rather than replacing it.
        using var posture = await PutAsync("/api/v1/compliance/posture", Manager, new
        {
            enabledPackIds = EnabledForFreezeTest,
        });
        posture.StatusCode.Should().Be(HttpStatusCode.OK, await posture.Content.ReadAsStringAsync());

        var dealId = await APricedDealAsync();
        await RegisterAtAsync(dealId, "QQ", "FZ", null);

        using var taxed = await PostAsync($"{Deals}/{dealId}/tax/from-pack", Manager, new { });
        taxed.StatusCode.Should().Be(HttpStatusCode.OK, await taxed.Content.ReadAsStringAsync());

        var agreed = await taxed.Content.ReadFromJsonAsync<JsonElement>();
        agreed.GetProperty("packVersion").GetInt32().Should().Be(1);

        // 15,500 basis at five percent.
        agreed.GetProperty("deal").GetProperty("taxTotal").GetDecimal().Should().Be(775m);

        // The rate goes up and a new generation is loaded. This is what a
        // quarterly update is: a new version, never an edit to the old one.
        using var second = await LoadPackAsync(FreezePackAtVersion(2, stateRate: "0.09"));
        second.StatusCode.Should().Be(HttpStatusCode.OK, await second.Content.ReadAsStringAsync());

        using var reviewedSecond = await AdminPostAsync($"{Packs}/qq-freeze/2/review");
        reviewedSecond.StatusCode.Should().Be(HttpStatusCode.OK);

        // THE ASSERTION THIS FILE EXISTS FOR. The deal was taxed under version 1
        // and stays taxed under version 1, at version 1's rate — nothing
        // recomputes on read, so a customer holding an invoice still holds a
        // correct one.
        using var reread = await SendAsync(HttpMethod.Get, $"{Deals}/{dealId}", Manager);
        var deal = await reread.Content.ReadFromJsonAsync<JsonElement>();

        deal.GetProperty("taxTotal").GetDecimal().Should().Be(775m);

        var frozen = deal.GetProperty("taxLines").EnumerateArray().Single();
        frozen.GetProperty("packVersion").GetInt32().Should().Be(1);
        frozen.GetProperty("rate").GetDecimal().Should().Be(0.05m);

        // And a NEW deal gets the new rate, so the old one being untouched is
        // evidence of freezing rather than of the update having failed.
        var fresh = await APricedDealAsync();
        await RegisterAtAsync(fresh, "QQ", "FZ", null);

        using var freshTax = await PostAsync($"{Deals}/{fresh}/tax/from-pack", Manager, new { });
        var freshBody = await freshTax.Content.ReadFromJsonAsync<JsonElement>();

        freshBody.GetProperty("packVersion").GetInt32().Should().Be(2);
        freshBody.GetProperty("deal").GetProperty("taxLines").EnumerateArray().Single()
            .GetProperty("rate").GetDecimal().Should().Be(0.09m);
        freshBody.GetProperty("deal").GetProperty("taxTotal").GetDecimal().Should().Be(1395m);
    }

    [Fact]
    public async Task A_caller_cannot_claim_a_figure_came_from_a_rate_table()
    {
        var dealId = await APricedDealAsync();

        using var response = await PostAsync($"{Deals}/{dealId}/tax", Manager, new
        {
            lines = new[]
            {
                new
                {
                    description = "Definitely official tax",
                    jurisdiction = "US-XX",
                    basis = 10000m,
                    rate = 0.01m,
                    amount = 100m,
                    provenance = "Pack",
                    packId = "us-in",
                    packVersion = 1,
                },
            },
            taxedAt = new { administrativeArea = "XX", county = (string?)null, postalCode = "00000", country = "US" },
        });

        // Without this the provenance column would be decorative: anything could
        // post a figure wearing a rate table's name and nothing could tell it
        // from one the resolver wrote.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("detail").GetString().Should().Contain("rate table");
    }

    [Fact]
    public async Task An_unsupported_jurisdiction_is_a_label_and_still_works_by_hand()
    {
        var dealId = await APricedDealAsync();

        // A real country, with no pack enabled for it.
        await RegisterAtAsync(dealId, "GB", "Greater London", null);

        using var asked = await PostAsync($"{Deals}/{dealId}/tax/from-pack", Manager, new { });
        asked.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await asked.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("outcome").GetString().Should().Be("NoPackEnabled");
        body.GetProperty("packId").ValueKind.Should().Be(JsonValueKind.Null);

        // Not an error, and the deal is untouched rather than emptied.
        body.GetProperty("deal").GetProperty("taxLines").GetArrayLength().Should().Be(0);

        using var typed = await PostAsync($"{Deals}/{dealId}/tax", Manager, new
        {
            lines = new[]
            {
                new
                {
                    description = "VAT",
                    jurisdiction = "GB",
                    basis = 15500m,
                    rate = 0.20m,
                    amount = 3100m,
                    provenance = "EnteredByPerson",
                },
            },
            taxedAt = new { administrativeArea = "Greater London", county = (string?)null, postalCode = "SW1A 1AA", country = "GB" },
        });

        typed.StatusCode.Should().Be(HttpStatusCode.OK);

        var line = (await typed.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("taxLines").EnumerateArray().Single();

        line.GetProperty("provenance").GetString().Should().Be("EnteredByPerson");
        line.GetProperty("packId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task The_shipped_indiana_pack_cannot_be_marked_supported_until_somebody_decides_what_it_taxes()
    {
        using var listed = await AdminSendAsync(HttpMethod.Get, Packs);
        listed.StatusCode.Should().Be(HttpStatusCode.OK);

        var indiana = (await listed.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray()
            .Single(p => p.GetProperty("packId").GetString() == "us-in");

        indiana.GetProperty("status").GetString().Should().Be("Draft");
        indiana.GetProperty("sourceUri").GetString().Should().Contain("in.gov");
        indiana.GetProperty("rateCount").GetInt32().Should().Be(1);

        var missing = indiana.GetProperty("missingRules").EnumerateArray()
            .Select(r => r.GetString()).ToList();

        missing.Should().BeEquivalentTo(
            ["tradeInReducesBasis", "documentationFeeIsTaxable", "otherFeesAreTaxable"]);

        using var review = await AdminPostAsync($"{Packs}/us-in/1/review");

        // The gate with teeth: a reviewer who never opened the source cannot
        // bless somebody's guesses by pressing a button.
        review.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await review.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("detail").GetString().Should().Contain("tradeInReducesBasis");
    }

    /// <summary>
    /// A pack for a jurisdiction that does not exist, used only by the freeze
    /// test. QQ and the rest of the ZZ range are reserved by ISO 3166 for private
    /// use, so no real address can ever match it.
    /// </summary>
    private static byte[] FreezePackAtVersion(int version, string stateRate) =>
        Encoding.UTF8.GetBytes($$"""
            {
              "packId": "qq-freeze",
              "country": "QQ",
              "scheme": "Manual",
              "version": {{version}},
              "effectiveFrom": "2020-01-01",
              "sourceUri": "https://example.invalid/not-a-real-jurisdiction",
              "fetchedAt": "2026-10-18T00:00:00Z",
              "rules": {
                "tradeInReducesBasis": true,
                "documentationFeeIsTaxable": true,
                "otherFeesAreTaxable": false
              },
              "rates": [{
                "jurisdictionCode": "QQ-FZ",
                "description": "Freeze-test tax (not a real jurisdiction)",
                "rateKind": "State",
                "rate": {{stateRate}},
                "administrativeArea": "FZ",
                "effectiveFrom": "2020-01-01",
                "sourceUri": "https://example.invalid/not-a-real-jurisdiction"
              }]
            }
            """);

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

    private async Task<string> APricedDealAsync()
    {
        var rooftopId = await RooftopIdAsync();
        var unitId = await ReceiveAvailableUnitAsync(rooftopId);

        using var started = await PostAsync(Deals, Manager, new
        {
            rooftopId,
            customerId = await AddCustomerAsync(),
            inventoryUnitId = unitId,
            currency = "USD",
            salespersonUserId = DevelopmentSeeder.DevUsers.Salesperson,
        });

        started.StatusCode.Should().Be(HttpStatusCode.Created);
        var dealId = (await started.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        using var priced = await PostAsync($"{Deals}/{dealId}/terms", Manager, new
        {
            charges = new[]
            {
                new { kind = "VehiclePrice", description = "The car", amount = 20000m },
                new { kind = "DocumentationFee", description = "Documentation fee", amount = 500m },
                new { kind = "Fee", description = "Registration", amount = 100m },
            },
            tradeIn = new { description = "2016 Golf", allowance = 5000m, payoff = 0m },
        });

        priced.StatusCode.Should().Be(HttpStatusCode.OK);
        return dealId;
    }

    private async Task RegisterAtAsync(string dealId, string country, string? area, string? county)
    {
        using var response = await PostAsync($"{Deals}/{dealId}/registration-address", Manager, new
        {
            address = new
            {
                line1 = "1 Test Street",
                line2 = (string?)null,
                city = "Testville",
                administrativeArea = area,
                county,
                postalCode = "12345",
                country,
            },
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<string> RooftopIdAsync()
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/v1/organization", Manager);
        var root = await response.Content.ReadFromJsonAsync<JsonElement>();

        return root.GetProperty("legalEntities")
            .EnumerateArray()
            .SelectMany(entity => entity.GetProperty("rooftops").EnumerateArray())
            .Single(rooftop => rooftop.GetProperty("code").GetString() == "NAG-01")
            .GetProperty("id")
            .ToString();
    }

    private async Task<string> AddCustomerAsync()
    {
        using var response = await PostAsync("/api/v1/customers", Manager, new
        {
            kind = "Person", firstName = "Tax", lastName = $"Test{Guid.NewGuid():N}"[..12],
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    /// <summary>Records a vehicle, takes it into stock, and makes it sellable.</summary>
    private async Task<string> ReceiveAvailableUnitAsync(string rooftopId)
    {
        using var vehicle = await PostAsync("/api/v1/vehicles", Manager, new
        {
            vin = $"TAX{Guid.NewGuid():N}"[..17].ToUpperInvariant(),
            modelYear = 2021,
            make = "Toyota",
            model = "RAV4",
        });
        var vehicleId = (await vehicle.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        using var received = await PostAsync("/api/v1/inventory", Manager, new
        {
            vehicleId, rooftopId, stockNumber = $"T{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
        });
        received.StatusCode.Should().Be(HttpStatusCode.Created);
        var unitId = (await received.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        using var available = await PostAsync($"/api/v1/inventory/{unitId}/status", Manager,
            new { status = "Available" });
        available.StatusCode.Should().Be(HttpStatusCode.OK);

        return unitId;
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

    private async Task<HttpResponseMessage> LoadPackAsync(byte[] content)
    {
        using var client = _fixture.CreateClient();
        var admin = await _fixture.AdministratorAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Packs, UriKind.Relative))
        {
            Content = new ByteArrayContent(content),
        };
        request.Headers.Add("Cookie", $"dfoss_admin={admin.SessionToken}");
        request.Headers.Add("X-Admin-CSRF-Token", admin.AntiForgeryToken);

        return await client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> AdminPostAsync(string path) =>
        await AdminSendAsync(HttpMethod.Post, path);

    private async Task<HttpResponseMessage> AdminSendAsync(HttpMethod method, string path)
    {
        using var client = _fixture.CreateClient();
        var admin = await _fixture.AdministratorAsync();

        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Headers.Add("Cookie", $"dfoss_admin={admin.SessionToken}");

        if (method != HttpMethod.Get)
        {
            request.Headers.Add("X-Admin-CSRF-Token", admin.AntiForgeryToken);
        }

        return await client.SendAsync(request);
    }
}
