// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   PackageTests — a rooftop's records moved to another installation with their
//   identities, their references and their paperwork intact.
//
// Usage:
//   Runs with the normal test suite; needs a reachable SQL engine.
//
// Coding Instructions:
//   The headline test is The_paperwork_comes_out_the_same_on_the_other_side,
//   and it is the whole reason this file exists. I2's export criterion asks for
//   IDs, relationships AND documents, and documents were the hard third because
//   this system stores none — IDocuments renders a deal or a job on demand and
//   keeps nothing. So "preserving a document" cannot mean copying a file. It
//   means the renderer, pointed at the far side, produces the same page. That
//   is a strictly stronger claim than shipping HTML would have been, because it
//   fails if ANY of the fields behind the page was lost, including ones nobody
//   thought to assert.
//
//   Counting records is not evidence and no test here does it alone. A package
//   that carried the right number of deals and hung them all on the wrong
//   customer would pass a count and fail every test below.
//
//   THE DIRECTION IS citymotors -> northgroup/NAG-02, deliberately. The test
//   suite writes heavily into northgroup's first lot, so exporting from there
//   would produce a package whose contents depend on what ran first. It also
//   means the round trip crosses a real tenant boundary — two separate
//   databases — rather than moving records around inside one.

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class PackageTests(HostFixture fixture)
{
    private const string Manager = DevelopmentSeeder.DevUsers.OrganizationWideEmail;
    private const string Advisor = DevelopmentSeeder.DevUsers.FirstRooftopOnlyEmail;
    private const string From = "citymotors";
    private const string Into = "northgroup";

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task The_paperwork_comes_out_the_same_on_the_other_side()
    {
        var made = await ASoldCarAndAJobAsync();

        var before = await PaperworkAsync(From, made);
        await MoveAsync();
        var after = await PaperworkAsync(Into, made);

        // Not "contains the total" and not "has the same number of rows". The
        // whole money table, character for character. Anything that did not
        // survive the package shows up here whether or not anybody predicted it.
        after.Order.Should().Be(before.Order,
            because: "the order this customer signed is the same order in either installation");

        after.Invoice.Should().Be(before.Invoice,
            because: "so is the bill for the work, including the line they declined");

        // And a sanity check on the test itself: an empty string on both sides
        // would satisfy the two assertions above and prove nothing.
        before.Order.Should().Contain("Due from the customer");
        before.Order.Should().Contain("$20,000.00");
        before.Invoice.Should().Contain("declined");
    }

    [Fact]
    public async Task Every_record_keeps_the_id_it_arrived_with()
    {
        var made = await ASoldCarAndAJobAsync();
        await MoveAsync();

        // Asked for by the id the other installation used. A 404 here means the
        // package minted new keys, which would break every reference in it.
        await ReadAsync(Into, $"/api/v1/customers/{made.CustomerId}");
        await ReadAsync(Into, $"/api/v1/vehicles/{made.VehicleId}");
        await ReadAsync(Into, $"/api/v1/inventory/{made.UnitId}");
        await ReadAsync(Into, $"/api/v1/deals/{made.DealId}");
        await ReadAsync(Into, $"/api/v1/repair-orders/{made.JobId}");
    }

    [Fact]
    public async Task What_points_at_what_still_points_at_it()
    {
        var made = await ASoldCarAndAJobAsync();
        await MoveAsync();

        var deal = await ReadAsync(Into, $"/api/v1/deals/{made.DealId}");
        deal.GetProperty("customerId").GetGuid().Should().Be(made.CustomerId);
        deal.GetProperty("inventoryUnitId").GetGuid().Should().Be(made.UnitId);

        // One hop further out, which is the one a flat export cannot carry: the
        // deal names a unit, and the unit has to name the same car the job does.
        var unit = await ReadAsync(Into, $"/api/v1/inventory/{made.UnitId}");
        unit.GetProperty("vehicleId").GetGuid().Should().Be(made.VehicleId);

        var job = await ReadAsync(Into, $"/api/v1/repair-orders/{made.JobId}");
        job.GetProperty("customerId").GetGuid().Should().Be(made.CustomerId);
        job.GetProperty("vehicleId").GetGuid().Should().Be(made.VehicleId);

        // The lot is the RECEIVING installation's, not the one in the file. A
        // rooftop id from somewhere else names nothing here, and writing it
        // through would put the records in a lot that does not exist.
        deal.GetProperty("rooftopId").GetGuid().Should().Be(await RooftopAsync(Into, "NAG-02"));
    }

    [Fact]
    public async Task Running_the_same_package_again_changes_nothing()
    {
        // The recovery path. A package is applied without a transaction around
        // it, so "run it again" has to be safe or a half-finished import would
        // be unrecoverable.
        var made = await ASoldCarAndAJobAsync();

        var first = await MoveAsync();
        var second = await MoveAsync();

        first.GetProperty("applied").GetInt32().Should().BeGreaterThan(0);

        second.GetProperty("applied").GetInt32().Should().Be(0,
            because: "everything the first run wrote is already here the second time");

        second.GetProperty("reused").GetInt32().Should().Be(
            first.GetProperty("applied").GetInt32() + first.GetProperty("reused").GetInt32(),
            because: "what was written becomes what is already here, and nothing else moves");

        // Not "no refusals". Both installations here were seeded from the same
        // demo data, so a few customers legitimately share an external reference
        // and are refused every time — which is itself the property worth
        // asserting: a second run refuses exactly what the first run refused,
        // and finds nothing new to complain about.
        Refused(second).Should().BeEquivalentTo(Refused(first),
            because: "a second run of the same package reaches the same conclusions");

        // And the record itself was not rewritten on the way through.
        var deal = await ReadAsync(Into, $"/api/v1/deals/{made.DealId}");
        deal.GetProperty("amountDue").GetDecimal().Should().Be(20000m);
    }

    [Fact]
    public async Task A_deal_whose_customer_did_not_arrive_is_refused_by_name_and_the_rest_land()
    {
        var made = await ASoldCarAndAJobAsync();
        var package = await ExportAsync();

        // Cut the customer out of the file, the way a hand edit or a truncated
        // transfer would. Everything else still refers to them.
        var broken = Rewrite(package, root =>
        {
            root["customers"] = new JsonArray();
            return root;
        });

        var report = await ApplyAsync(broken);

        var refused = report.GetProperty("refused").EnumerateArray().ToList();

        refused.Should().Contain(r => r.GetProperty("id").GetGuid() == made.DealId
            && r.GetProperty("reasonCode").GetString() == "deals.customer_not_found");

        refused.Should().Contain(r => r.GetProperty("id").GetGuid() == made.JobId
            && r.GetProperty("reasonCode").GetString() == "service.customer_not_found");

        // The point of naming a refusal rather than failing the request: the car
        // and its stock record are unaffected by a missing customer and land.
        await ReadAsync(Into, $"/api/v1/vehicles/{made.VehicleId}");
        await ReadAsync(Into, $"/api/v1/inventory/{made.UnitId}");
    }

    [Fact]
    public async Task A_deal_that_does_not_come_to_what_it_says_is_refused()
    {
        // The check that makes a package trustworthy rather than merely
        // well-formed. A charge lost in transit leaves a file that parses
        // perfectly and is wrong about money.
        var made = await ASoldCarAndAJobAsync();
        var package = await ExportAsync();

        var tampered = Rewrite(package, root =>
        {
            foreach (var deal in root["deals"]!.AsArray())
            {
                if (deal!["id"]!.GetValue<Guid>() == made.DealId)
                {
                    deal["charges"]!.AsArray().Clear();
                }
            }

            return root;
        });

        var report = await ApplyAsync(tampered);

        var refusal = report.GetProperty("refused").EnumerateArray()
            .Should().ContainSingle(r => r.GetProperty("id").GetGuid() == made.DealId).Subject;

        refusal.GetProperty("reasonCode").GetString().Should().Be("deals.total_disagrees");

        // Both figures, because "the totals disagree" on its own sends somebody
        // diffing two files by hand.
        refusal.GetProperty("reason").GetString().Should().Contain("20000").And.Contain("0");
    }

    [Fact]
    public async Task The_order_the_records_appear_in_does_not_matter()
    {
        // The file version of the reordered-delivery property the connector
        // runtime has. A package hand-edited, merged, or written by a tool that
        // sorts its output must still apply, because the dependency order is
        // decided by the importer and not read from the file.
        var made = await ASoldCarAndAJobAsync();
        var package = await ExportAsync();

        var backwards = Rewrite(package, root =>
        {
            var reversed = new JsonObject
            {
                ["format"] = root["format"]!.DeepClone(),
                ["version"] = root["version"]!.DeepClone(),
                ["producedAt"] = root["producedAt"]!.DeepClone(),
                ["source"] = root["source"]!.DeepClone(),
                ["repairOrders"] = root["repairOrders"]!.DeepClone(),
                ["deals"] = root["deals"]!.DeepClone(),
                ["inventoryUnits"] = root["inventoryUnits"]!.DeepClone(),
                ["vehicles"] = root["vehicles"]!.DeepClone(),
                ["customers"] = root["customers"]!.DeepClone(),
            };

            return reversed;
        });

        var report = await ApplyAsync(backwards);

        report.GetProperty("refused").EnumerateArray()
            .Should().NotContain(r => r.GetProperty("id").GetGuid() == made.DealId);

        await ReadAsync(Into, $"/api/v1/deals/{made.DealId}");
    }

    [Fact]
    public async Task A_package_from_a_newer_build_is_refused_rather_than_half_read()
    {
        var package = await ExportAsync();
        var newer = Rewrite(package, root =>
        {
            root["version"] = 99;
            return root;
        });

        using var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/migration/packages/{await RooftopAsync(Into, "NAG-02")}",
            Manager,
            Into,
            new { content = newer });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("migration.package_is_newer");
    }

    [Fact]
    public async Task Something_that_is_not_a_package_says_so()
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/migration/packages/{await RooftopAsync(Into, "NAG-02")}",
            Manager,
            Into,
            new { content = "externalid,kind,firstname\n\"A\",\"Person\",\"Jo\"" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("migration.package_unreadable");
    }

    [Fact]
    public async Task Exporting_a_package_needs_permission_across_the_whole_group()
    {
        // A lot's whole record set is more personal data in one file than
        // anything else this API hands out, so a one-lot advisor cannot take it
        // even for the lot they work at.
        using var response = await SendAsync(
            HttpMethod.Get,
            $"/api/v1/migration/packages/{await RooftopAsync(Into, "NAG-01")}",
            Advisor,
            Into);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_exported_package_carries_a_checksum_that_matches_it()
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            $"/api/v1/migration/packages/{await RooftopAsync(From, "CM-01")}",
            Manager,
            From);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var content = await response.Content.ReadAsStringAsync();
        var claimed = response.Headers.GetValues("X-Content-SHA256").Single();

        // A package truncated in transit is worse than one that failed, because
        // it looks like data — the same reason the CSV export publishes one.
        Sha256(content).Should().Be(claimed);

        content.Should().Contain("\"format\": \"dealerfoss.package\"");
    }

    // --- making something worth moving ------------------------------------

    private sealed record Made(Guid CustomerId, Guid VehicleId, Guid UnitId, Guid DealId, Guid JobId);

    /// <summary>
    /// A customer with an address and a telephone number, a car, that car in
    /// stock, a delivered deal with every kind of line on it, and an invoiced
    /// job with a declined line. Between them they touch every field the two
    /// documents print, which is what makes the comparison meaningful.
    /// </summary>
    private async Task<Made> ASoldCarAndAJobAsync()
    {
        var rooftop = await RooftopAsync(From, "CM-01");
        var tag = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var customer = (await PostAsync(From, "/api/v1/customers", new
        {
            kind = "Person",
            firstName = "Marguerite",
            lastName = $"Vasseur{tag}",
            email = $"m.vasseur.{tag}@example.test",
            phone = "555-0142",
        }, HttpStatusCode.Created)).GetProperty("id").GetGuid();

        await PutAsync(From, $"/api/v1/customers/{customer}/address", new
        {
            line1 = "18 Rue Lepic",
            line2 = (string?)null,
            city = "Chicago",
            administrativeArea = "IL",
            county = "Cook",
            postalCode = "60601",
            country = "US",
        });

        var vehicle = (await PostAsync(From, "/api/v1/vehicles", new
        {
            vin = $"5YJ3E1EA{tag}9",   // 8 + 8 + 1 = the 17 a real VIN has
            modelYear = 2022,
            make = "Peugeot",
            model = "3008",
            trim = "GT Line",
            bodyStyle = "SUV",
            exteriorColor = "Nimbus Grey",
        }, HttpStatusCode.Created)).GetProperty("id").GetGuid();

        var unit = (await PostAsync(From, "/api/v1/inventory", new
        {
            vehicleId = vehicle,
            rooftopId = rooftop,
            stockNumber = $"PKG-{tag}",
            costAmount = 14250m,
            costCurrency = "USD",
        }, HttpStatusCode.Created)).GetProperty("id").GetGuid();

        await PostAsync(From, $"/api/v1/inventory/{unit}/status",
            new { status = "Available", note = (string?)null }, HttpStatusCode.OK);

        var product = (await PostAsync(From, "/api/v1/finance/products", new
        {
            name = $"Paint protection {tag}",
            kind = "Warranty",
            provider = "Lakeshore Underwriting",
            defaultPrice = 600m,
            defaultCost = 300m,
            currency = "USD",
            termMonths = 24,
        }, HttpStatusCode.Created)).GetProperty("id").GetGuid();

        var deal = (await PostAsync(From, "/api/v1/deals", new
        {
            rooftopId = rooftop, customerId = customer, inventoryUnitId = unit, currency = "USD",
        }, HttpStatusCode.Created)).GetProperty("id").GetGuid();

        // A car, a fee, a discount and a trade-in, so the money table has
        // something to lose. The figures are chosen to come to a round 20,000.
        await PostAsync(From, $"/api/v1/deals/{deal}/terms", new
        {
            charges = new object[]
            {
                new { kind = "VehiclePrice", description = "2022 Peugeot 3008 GT Line", amount = 19500m },
                new { kind = "DocumentationFee", description = "Documentation", amount = 499m },
                new { kind = "Discount", description = "Manager's discount", amount = -250m },
            },
            tradeIn = new { description = "2013 Renault Clio", allowance = 1200m, payoff = 0m },
        }, HttpStatusCode.OK);

        await PostAsync(From, $"/api/v1/deals/{deal}/products", new
        {
            products = new[] { new { financeProductId = product, price = 600m, cost = 300m } },
        }, HttpStatusCode.OK);

        await PostAsync(From, $"/api/v1/deals/{deal}/tax", new
        {
            lines = new[]
            {
                new
                {
                    description = "Sales tax",
                    jurisdiction = "IL / Cook / Chicago",
                    basis = 19749m,
                    rate = 0.0430m,
                    amount = 851m,
                    provenance = "EnteredByPerson",
                },
            },
            taxedAt = new { administrativeArea = "IL", county = "Cook", postalCode = "60601", country = "US" },
        }, HttpStatusCode.OK);

        var job = (await PostAsync(From, "/api/v1/repair-orders", new
        {
            rooftopId = rooftop,
            customerId = customer,
            vehicleId = vehicle,
            complaint = "Knocking over rough ground.",
            currency = "USD",
            odometerReading = 31400,
        }, HttpStatusCode.Created)).GetProperty("id").GetGuid();

        await PostAsync(From, $"/api/v1/repair-orders/{job}/lines", new
        {
            kind = "Labour", description = "Strip and inspect front suspension", hours = 2m, rate = 135m,
        }, HttpStatusCode.OK);

        await PostAsync(From, $"/api/v1/repair-orders/{job}/status",
            new { status = "InProgress", note = (string?)null }, HttpStatusCode.OK);

        var extra = await PostAsync(From, $"/api/v1/repair-orders/{job}/lines", new
        {
            kind = "Part", description = "Anti-roll bar links", unitAmount = 96m,
        }, HttpStatusCode.OK);

        // Declined, because declined work is printed at nothing and stays on the
        // record — and it is the one line whose authorization cannot be guessed
        // from the job's status on the way back in.
        var lineId = extra.GetProperty("lines").EnumerateArray()
            .Single(l => l.GetProperty("description").GetString() == "Anti-roll bar links")
            .GetProperty("id").GetGuid();

        await PostAsync(From, $"/api/v1/repair-orders/{job}/lines/{lineId}/answer",
            new { approved = false, note = "Will book it in next month." }, HttpStatusCode.OK);

        await PostAsync(From, $"/api/v1/repair-orders/{job}/status",
            new { status = "Completed", note = (string?)null }, HttpStatusCode.OK);

        await PostAsync(From, $"/api/v1/repair-orders/{job}/status",
            new { status = "Invoiced", note = (string?)null }, HttpStatusCode.OK);

        return new Made(customer, vehicle, unit, deal, job);
    }

    // --- moving it ---------------------------------------------------------

    private async Task<string> ExportAsync()
    {
        using var response = await SendAsync(
            HttpMethod.Get, $"/api/v1/migration/packages/{await RooftopAsync(From, "CM-01")}", Manager, From);

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());

        return await response.Content.ReadAsStringAsync();
    }

    private async Task<JsonElement> ApplyAsync(string package)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/migration/packages/{await RooftopAsync(Into, "NAG-02")}",
            Manager,
            Into,
            new { content = package });

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> MoveAsync() => await ApplyAsync(await ExportAsync());

    /// <summary>Which records a run would not write, as "kind/id: code".</summary>
    private static IReadOnlyList<string> Refused(JsonElement report) =>
        [.. report.GetProperty("refused").EnumerateArray()
            .Select(r => $"{r.GetProperty("kind").GetString()}/{r.GetProperty("id").GetGuid()}: "
                + r.GetProperty("reasonCode").GetString())
            .Order(StringComparer.Ordinal)];

    /// <summary>
    /// Re-serialised through a JsonNode so a test can break a package the way a
    /// hand edit or a truncated transfer would, rather than by constructing a
    /// package the exporter would never produce.
    /// </summary>
    private static string Rewrite(string package, Func<JsonObject, JsonObject> change)
    {
        var root = JsonNode.Parse(package)!.AsObject();

        return change(root).ToJsonString();
    }

    // --- reading it back ---------------------------------------------------

    private sealed record Paperwork(string Order, string Invoice);

    /// <summary>
    /// The money out of both documents, in the tenant given. The dealership
    /// name, the lot and the printing date are deliberately outside what is
    /// compared: they SHOULD differ, because the records really are at a
    /// different dealership now.
    /// </summary>
    private async Task<Paperwork> PaperworkAsync(string tenant, Made made)
    {
        var order = await DocumentAsync(tenant, $"/api/v1/documents/deals/{made.DealId}");
        var invoice = await DocumentAsync(tenant, $"/api/v1/documents/repair-orders/{made.JobId}");

        return new Paperwork(Substance(order, "What it comes to"), Substance(invoice, "Work done"));
    }

    private static string Substance(string html, string fromHeading)
    {
        var start = html.IndexOf($"<h2>{fromHeading}</h2>", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1, because: $"the document should have a '{fromHeading}' section");

        var end = html.LastIndexOf("</table>", StringComparison.Ordinal) + "</table>".Length;

        return html[start..end];
    }

    private async Task<string> DocumentAsync(string tenant, string path)
    {
        using var response = await SendAsync(HttpMethod.Get, path, Manager, tenant);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());

        return await response.Content.ReadAsStringAsync();
    }

    private async Task<JsonElement> ReadAsync(string tenant, string path)
    {
        using var response = await SendAsync(HttpMethod.Get, path, Manager, tenant);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"{path} should have arrived in {tenant}: " + await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<Guid> RooftopAsync(string tenant, string code)
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/v1/organization", Manager, tenant);

        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("legalEntities").EnumerateArray()
            .SelectMany(e => e.GetProperty("rooftops").EnumerateArray())
            .Single(r => r.GetProperty("code").GetString() == code)
            .GetProperty("id").GetGuid();
    }

    // --- plumbing ----------------------------------------------------------

    private async Task<JsonElement> PostAsync(
        string tenant, string path, object body, HttpStatusCode expected)
    {
        using var response = await SendAsync(HttpMethod.Post, path, Manager, tenant, body);
        response.StatusCode.Should().Be(expected, because: await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task PutAsync(string tenant, string path, object body)
    {
        using var response = await SendAsync(HttpMethod.Put, path, Manager, tenant, body);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, string email, string tenant, object? body = null)
    {
        var session = await _fixture.SignInAsync(email, tenant);
        using var client = _fixture.CreateClient();

        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.Headers.Add("Cookie", $"dfoss_session={session.SessionToken}");
        request.Headers.Add("X-Tenant", tenant);

        if (method != HttpMethod.Get)
        {
            request.Headers.Add("X-CSRF-Token", session.AntiForgeryToken);
        }

        return await client.SendAsync(request);
    }

    private static string Sha256(string content) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(content)))
            .ToLower(CultureInfo.InvariantCulture);
}
