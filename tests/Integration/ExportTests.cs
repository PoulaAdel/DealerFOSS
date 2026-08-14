// ExportTests — a dealership taking its own records away, and the round trip
// that proves the file is worth something.
//
// Use:  runs with the normal test suite; needs a reachable SQL engine.
// Edit: the headline test is Records_survive_a_round_trip_into_another_dealership.
//       It exports from one tenant and imports into a different one through the
//       ordinary API, then compares. That is the promise an open DMS makes — you
//       can leave, and take your data — and it is worth nothing as a sentence in
//       a README. If it ever fails, the export has stopped being an export.

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class ExportTests(HostFixture fixture)
{
    private const string Manager = DevelopmentSeeder.DevUsers.OrganizationWideEmail;
    private const string Advisor = DevelopmentSeeder.DevUsers.FirstRooftopOnlyEmail;

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task Exporting_needs_permission_across_the_whole_group()
    {
        using var response = await SendAsync(
            HttpMethod.Get, "/api/v1/migration/exports/Vehicles", Advisor, "northgroup");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("code").GetString().Should().Be("migration.forbidden_export");
    }

    [Fact]
    public async Task An_export_arrives_as_a_file_with_a_checksum_that_matches()
    {
        using var response = await SendAsync(
            HttpMethod.Get, "/api/v1/migration/exports/Vehicles", Manager, "northgroup");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        response.Content.Headers.ContentDisposition!.FileName.Should().Be("vehicles.csv");

        var content = await response.Content.ReadAsStringAsync();
        var published = response.Headers.GetValues("X-Content-SHA256").Single();

        // A file truncated in transit is worse than one that failed, because it
        // looks like data. The checksum is how the far end can tell.
        Sha256(content).Should().Be(published);

        var rows = int.Parse(
            response.Headers.GetValues("X-Row-Count").Single(), CultureInfo.InvariantCulture);
        content.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(rows + 1);
    }

    [Fact]
    public async Task An_unknown_kind_is_refused()
    {
        using var response = await SendAsync(
            HttpMethod.Get, "/api/v1/migration/exports/Spaceships", Manager, "northgroup");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("code").GetString().Should().Be("migration.unknown_kind");
    }

    [Fact]
    public async Task An_export_carries_the_columns_the_importer_reads()
    {
        // Not a formatting preference. The header line is the contract between
        // the two halves, and it is what makes the round trip below possible.
        using var customers = await SendAsync(
            HttpMethod.Get, "/api/v1/migration/exports/Customers", Manager, "northgroup");

        var header = (await customers.Content.ReadAsStringAsync()).Split('\n')[0];

        header.Should().Contain("externalid").And.Contain("lastname").And.Contain("kind");

        using var vehicles = await SendAsync(
            HttpMethod.Get, "/api/v1/migration/exports/Vehicles", Manager, "northgroup");

        (await vehicles.Content.ReadAsStringAsync()).Split('\n')[0]
            .Should().Contain("vin").And.Contain("modelyear")
            .And.Contain("make").And.Contain("model");
    }

    [Fact]
    public async Task Records_survive_a_round_trip_into_another_dealership()
    {
        // Put two known cars into northgroup, one of them with a comma and a
        // quote in its text, because that is where a CSV round trip breaks.
        //
        // Real 17-character VINs, not short synthetic ones: the importer refuses
        // a non-standard VIN that carries no written reason, so a shorter stem
        // would test the refusal rather than the round trip.
        var stem = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var vin = $"1HGBH41JXM{stem}";
        var awkward = "Bob \"Big Bob\" Special, Ltd";

        using var seeded = await SendAsync(
            HttpMethod.Post, "/api/v1/migration/imports", Manager, "northgroup",
            new
            {
                kind = "Vehicles",
                mode = "Apply",
                sourceName = "roundtrip.csv",
                content =
                    "vin,modelyear,make,model,trim\n"
                    + $"{vin}A,2021,Toyota,RAV4,XLE\n"
                    + $"{vin}B,2019,Ford,F-150,\"{awkward.Replace("\"", "\"\"", StringComparison.Ordinal)}\"",
            });

        seeded.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var seedJobId = (await seeded.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();
        var seedJob = await WaitForImportAsync(seedJobId, "northgroup");

        // Asserted, not assumed. Without this the export below would be of
        // whatever happened to be there and the round trip would prove nothing.
        seedJob.GetProperty("rowsCreated").GetInt32().Should().Be(2,
            because: "the two cars must exist before they can be exported — "
                + await ProblemsAsync(seedJobId, "northgroup"));

        // Export the whole of northgroup.
        using var exported = await SendAsync(
            HttpMethod.Get, "/api/v1/migration/exports/Vehicles", Manager, "northgroup");

        exported.StatusCode.Should().Be(HttpStatusCode.OK);
        var file = await exported.Content.ReadAsStringAsync();

        // Now feed that exact file to a *different* dealership, through the
        // ordinary import endpoint. No converter, no special handling.
        using var reimported = await SendAsync(
            HttpMethod.Post, "/api/v1/migration/imports", Manager, "citymotors",
            new { kind = "Vehicles", mode = "Apply", sourceName = "from-northgroup.csv", content = file });

        reimported.StatusCode.Should().Be(HttpStatusCode.Accepted,
            because: "an export this API produced must be a file it accepts: "
                + await reimported.Content.ReadAsStringAsync());

        var job = await WaitForImportAsync(
            (await reimported.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(),
            "citymotors");

        job.GetProperty("rowsFailed").GetInt32().Should().Be(0,
            because: "every row we wrote, we must be able to read — refused rows: "
                + await ProblemsAsync(job.GetProperty("id").GetGuid(), "citymotors"));

        // And the awkward one arrived intact rather than as two columns.
        using var found = await SendAsync(
            HttpMethod.Get, $"/api/v1/vehicles?search={vin}B", Manager, "citymotors");

        var matches = await found.Content.ReadFromJsonAsync<JsonElement>();
        matches.GetArrayLength().Should().Be(1,
            because: $"the car should have arrived. Job: created="
                + $"{job.GetProperty("rowsCreated").GetInt32()} "
                + $"updated={job.GetProperty("rowsUpdated").GetInt32()} "
                + $"skipped={job.GetProperty("rowsSkipped").GetInt32()} "
                + $"of {job.GetProperty("rowsTotal").GetInt32()}. "
                + $"Export had the VIN: {file.Contains(vin + "B", StringComparison.Ordinal)}. "
                + $"Export bytes: {file.Length}.");

        matches[0].GetProperty("trim").GetString().Should().Be(awkward);
    }

    [Fact]
    public async Task A_customer_typed_in_by_hand_still_exports_and_imports()
    {
        // Created through the ordinary endpoint, so it has no external reference
        // at all. Without one, an export of it would be refused on import — an
        // export that cannot be imported is not an export.
        // Letters only, and that is load-bearing rather than tidiness. Customer
        // search pulls the digits out of the term and matches them against phone
        // numbers, so a hex suffix like "A3F91C2E" also searches for "3912" and
        // drags in anybody whose phone happens to contain it — making the "exactly
        // one result" assertion at the end depend on what other tests had already
        // created. It failed intermittently in a full run and passed alone, which
        // is how this was found. Same defect and same fix as CustomerTests.
        var surname = "Roundtrip" + new string([.. Guid.NewGuid().ToString("N")[..8]
            .Select(c => char.IsAsciiDigit(c) ? (char)('q' + (c - '0')) : c)]).ToUpperInvariant();

        using var created = await SendAsync(
            HttpMethod.Post, "/api/v1/customers", Manager, "northgroup",
            new { kind = "Person", firstName = "Hand", lastName = surname });

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        using var exported = await SendAsync(
            HttpMethod.Get, "/api/v1/migration/exports/Customers", Manager, "northgroup");

        var file = await exported.Content.ReadAsStringAsync();
        file.Should().Contain(surname);

        using var reimported = await SendAsync(
            HttpMethod.Post, "/api/v1/migration/imports", Manager, "citymotors",
            new { kind = "Customers", mode = "Apply", sourceName = "people.csv", content = file });

        var job = await WaitForImportAsync(
            (await reimported.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(),
            "citymotors");

        job.GetProperty("rowsFailed").GetInt32().Should().Be(0);

        using var found = await SendAsync(
            HttpMethod.Get, $"/api/v1/customers?search={surname}", Manager, "citymotors");

        (await found.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Exporting_twice_without_changes_produces_the_same_checksum()
    {
        using var first = await SendAsync(
            HttpMethod.Get, "/api/v1/migration/exports/Customers", Manager, "citymotors");
        using var second = await SendAsync(
            HttpMethod.Get, "/api/v1/migration/exports/Customers", Manager, "citymotors");

        // Stable ordering, or a dealership comparing two exports to see what
        // changed would get a diff of the whole file every time.
        first.Headers.GetValues("X-Content-SHA256").Single()
            .Should().Be(second.Headers.GetValues("X-Content-SHA256").Single());
    }

    // --- helpers ---

    /// <summary>
    /// The rows an import refused, with the reason. A round-trip failure that
    /// says only "expected 0 but found 1" sends somebody hunting; naming the row
    /// and the reason is the difference between a minute and an afternoon.
    /// </summary>
    private async Task<string> ProblemsAsync(Guid jobId, string tenant)
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            $"/api/v1/migration/imports/{jobId}/rows?problemsOnly=true&limit=5",
            Manager,
            tenant);

        var rows = await response.Content.ReadFromJsonAsync<JsonElement>();

        return string.Join(" | ", rows.EnumerateArray().Select(r =>
            $"row {r.GetProperty("rowNumber").GetInt32()}: "
            + $"{r.GetProperty("message").GetString()} <<{r.GetProperty("raw").GetString()}>>"));
    }

    private async Task<JsonElement> WaitForImportAsync(Guid jobId, string tenant)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);

        while (DateTime.UtcNow < deadline)
        {
            using var response = await SendAsync(
                HttpMethod.Get, $"/api/v1/migration/imports/{jobId}", Manager, tenant);

            var job = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (job.GetProperty("status").GetString() is "Completed" or "Failed")
            {
                return job;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Import {jobId} did not finish in {tenant}.");
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, string email, string tenant, object? body = null)
    {
        var session = await _fixture.SignInAsync(email, tenant);
        var client = _fixture.CreateClient();

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

        try
        {
            return await client.SendAsync(request);
        }
        finally
        {
            client.Dispose();
        }
    }

    private static string Sha256(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)))
            .ToLower(CultureInfo.InvariantCulture);
}
