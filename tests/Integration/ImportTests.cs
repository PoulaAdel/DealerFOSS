// ImportTests — a dealership's existing records arriving from a file, through
// the background worker that actually does it.
//
// Use:  runs with the normal test suite; needs a reachable SQL engine.
// Edit: the four properties worth protecting are (1) a trial changes nothing,
//       (2) importing the same file twice does not duplicate anything,
//       (3) a bad row is reported by the number a person sees in their
//       spreadsheet and does not stop the others, and (4) the counts add up.
//       A reconciliation report nobody can trust is worse than no report.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class ImportTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string Manager = DevelopmentSeeder.DevUsers.OrganizationWideEmail;
    private const string Advisor = DevelopmentSeeder.DevUsers.FirstRooftopOnlyEmail;

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task A_file_with_the_wrong_columns_is_refused_before_anything_is_queued()
    {
        using var response = await SubmitAsync(
            Manager, "Vehicles", "Trial", "vehicles.csv", "vin,make\nSOMEVIN,Toyota");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("migration.missing_columns");

        // Naming them is the difference between fixing the file in a minute and
        // guessing at it for an afternoon.
        problem.GetProperty("detail").GetString().Should().Contain("modelyear").And.Contain("model");
    }

    [Fact]
    public async Task An_empty_file_is_refused_rather_than_succeeding_having_done_nothing()
    {
        using var response = await SubmitAsync(
            Manager, "Vehicles", "Trial", "empty.csv", "vin,modelyear,make,model");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("code").GetString().Should().Be("migration.empty");
    }

    [Fact]
    public async Task Importing_needs_permission_across_the_whole_group()
    {
        using var response = await SubmitAsync(
            Advisor, "Vehicles", "Trial", "v.csv", VehicleFile(Unique()));

        // An import writes across every rooftop at once, so a one-lot user
        // cannot express the authority it needs.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_trial_says_what_would_happen_and_changes_nothing()
    {
        var vin = Unique();
        var job = await RunToCompletionAsync("Vehicles", "Trial", VehicleFile(vin));

        job.GetProperty("status").GetString().Should().Be("Completed");
        job.GetProperty("rowsTotal").GetInt32().Should().Be(2);
        job.GetProperty("rowsCreated").GetInt32().Should().Be(2);
        job.GetProperty("rowsFailed").GetInt32().Should().Be(0);

        // Nothing was written: the car is still not in the system.
        (await FindVehicleAsync(Vin17(vin, 'A'))).Should().BeNull(
            because: "a trial run answers a question; it does not do the work");
    }

    [Fact]
    public async Task A_trial_and_the_real_run_agree_about_an_unusual_vin()
    {
        // The property this whole design rests on: a trial's report is what the
        // real run will do. A short VIN is the case where that is easiest to get
        // wrong, because only the write would otherwise notice.
        var file = $"vin,modelyear,make,model\nSHORT{Unique()},1974,Ford,Escort";

        var trial = await RunToCompletionAsync("Vehicles", "Trial", file);
        var apply = await RunToCompletionAsync("Vehicles", "Apply", file);

        trial.GetProperty("rowsFailed").GetInt32().Should().Be(1);
        apply.GetProperty("rowsFailed").GetInt32().Should().Be(1);

        var rows = await RowsAsync(trial.GetProperty("id").GetGuid());
        rows.EnumerateArray().Single().GetProperty("message").GetString()
            .Should().Contain("vinexceptionreason");
    }

    [Fact]
    public async Task An_unusual_vin_imports_once_the_file_says_why()
    {
        var vin = $"SHORT{Unique()}";
        var file =
            "vin,modelyear,make,model,vinexceptionreason\n"
            + $"{vin},1974,Ford,Escort,Pre-1981 vehicle with a factory number of 14 characters.";

        var job = await RunToCompletionAsync("Vehicles", "Apply", file);

        job.GetProperty("rowsCreated").GetInt32().Should().Be(1);
        (await FindVehicleAsync(vin)).Should().NotBeNull();
    }

    [Fact]
    public async Task Applying_creates_the_records_and_the_counts_add_up()
    {
        var vin = Unique();
        var job = await RunToCompletionAsync("Vehicles", "Apply", VehicleFile(vin));

        job.GetProperty("rowsCreated").GetInt32().Should().Be(2);
        Sums(job).Should().Be(job.GetProperty("rowsTotal").GetInt32());

        (await FindVehicleAsync(Vin17(vin, 'A'))).Should().NotBeNull();
    }

    [Fact]
    public async Task Importing_the_same_file_twice_does_not_create_a_second_copy()
    {
        var vin = Unique();
        var file = VehicleFile(vin);

        var first = await RunToCompletionAsync("Vehicles", "Apply", file);
        first.GetProperty("rowsCreated").GetInt32().Should().Be(2);

        var second = await RunToCompletionAsync("Vehicles", "Apply", file);

        // The VIN is the car's identity, so the second run recognises both.
        second.GetProperty("rowsCreated").GetInt32().Should().Be(0);
        second.GetProperty("rowsSkipped").GetInt32().Should().Be(2);
        Sums(second).Should().Be(2);
    }

    [Fact]
    public async Task A_bad_row_is_reported_by_its_line_number_and_does_not_stop_the_others()
    {
        var vin = Unique();
        var file =
            "vin,modelyear,make,model\n"
            + $"{Vin17(vin, 'A')},2021,Toyota,RAV4\n"
            + $"{Vin17(vin, 'B')},not-a-year,Ford,F-150\n"
            + $"{Vin17(vin, 'C')},2019,Honda,Civic";

        var job = await RunToCompletionAsync("Vehicles", "Apply", file);

        job.GetProperty("rowsCreated").GetInt32().Should().Be(2);
        job.GetProperty("rowsFailed").GetInt32().Should().Be(1);

        var rows = await RowsAsync(job.GetProperty("id").GetGuid());
        var bad = rows.EnumerateArray().Single();

        // Line 3 as a spreadsheet counts: the header is line 1.
        bad.GetProperty("rowNumber").GetInt32().Should().Be(3);
        bad.GetProperty("outcome").GetString().Should().Be("Failed");
        bad.GetProperty("message").GetString().Should().Contain("not a model year");

        // And the row is readable exactly as it was sent, because exceptions are
        // resolved by fixing the source, not by editing what we received.
        bad.GetProperty("raw").GetString().Should().Contain("not-a-year");
    }

    [Fact]
    public async Task A_customer_import_matches_on_the_id_from_the_old_system()
    {
        var reference = Unique();
        var file =
            "externalid,kind,firstname,lastname,email,addressline1,city\n"
            + $"{reference}-1,Person,Ada,Lovelace,ada@example.test,\"12 High Street, Apt 4\",Manchester\n"
            + $"{reference}-2,Business,,\"Bob \"\"Big Bob\"\" Motors\",,,";

        var first = await RunToCompletionAsync("Customers", "Apply", file);
        first.GetProperty("rowsCreated").GetInt32().Should().Be(2);

        // The quoted comma and the doubled quotes have to have survived, or the
        // address ended up in the wrong column and nobody would notice.
        var ada = await FindCustomerAsync("Lovelace");
        ada.Should().NotBeNull();

        var second = await RunToCompletionAsync("Customers", "Apply", file);
        second.GetProperty("rowsCreated").GetInt32().Should().Be(0);
        second.GetProperty("rowsUpdated").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task A_customer_row_with_no_external_id_is_refused_with_the_reason()
    {
        var reference = Unique();
        var file =
            "externalid,lastname\n"
            + $"{reference},Keeper\n"
            + ",Nameless";

        var job = await RunToCompletionAsync("Customers", "Apply", file);

        job.GetProperty("rowsCreated").GetInt32().Should().Be(1);
        job.GetProperty("rowsFailed").GetInt32().Should().Be(1);

        var rows = await RowsAsync(job.GetProperty("id").GetGuid());
        rows.EnumerateArray().Single().GetProperty("message").GetString()
            .Should().Contain("no external id");
    }

    [Fact]
    public async Task The_file_is_hashed_so_a_trial_and_its_real_run_are_about_the_same_data()
    {
        var file = VehicleFile(Unique());

        var trial = await RunToCompletionAsync("Vehicles", "Trial", file);
        var apply = await RunToCompletionAsync("Vehicles", "Apply", file);

        apply.GetProperty("sourceHash").GetString()
            .Should().Be(trial.GetProperty("sourceHash").GetString());
    }

    // --- helpers ---

    /// <summary>
    /// Submits a file and waits for the background worker to finish it. The
    /// worker is a real hosted service in this host, so this exercises the
    /// tenant-scoped background path rather than calling the runner directly.
    /// </summary>
    private async Task<JsonElement> RunToCompletionAsync(string kind, string mode, string content)
    {
        using var submitted = await SubmitAsync(Manager, kind, mode, $"{kind}.csv", content);
        submitted.StatusCode.Should().Be(HttpStatusCode.Accepted,
            because: await submitted.Content.ReadAsStringAsync());

        var id = (await submitted.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            using var response = await SendAsync(HttpMethod.Get, $"/api/v1/migration/imports/{id}", Manager);
            var job = await response.Content.ReadFromJsonAsync<JsonElement>();
            var status = job.GetProperty("status").GetString();

            if (status is "Completed" or "Failed")
            {
                job.GetProperty("failureReason").ValueKind.Should().Be(JsonValueKind.Null,
                    because: "the job itself should not have broken");
                return job;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Import {id} did not finish. Is the worker running?");
    }

    private async Task<JsonElement> RowsAsync(Guid jobId)
    {
        using var response = await SendAsync(
            HttpMethod.Get, $"/api/v1/migration/imports/{jobId}/rows?problemsOnly=true", Manager);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement?> FindVehicleAsync(string vin)
    {
        // "search", not "term". An unbound query parameter reads as an empty
        // search here, which returns the first page of everything — and a test
        // asserting on that would be quietly meaningless.
        using var response = await SendAsync(
            HttpMethod.Get, $"/api/v1/vehicles?search={Uri.EscapeDataString(vin)}", Manager);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetArrayLength() == 0 ? null : body[0];
    }

    private async Task<JsonElement?> FindCustomerAsync(string term)
    {
        using var response = await SendAsync(
            HttpMethod.Get, $"/api/v1/customers?search={Uri.EscapeDataString(term)}", Manager);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetArrayLength() == 0 ? null : body[0];
    }

    private Task<HttpResponseMessage> SubmitAsync(
        string email, string kind, string mode, string sourceName, string content) =>
        SendAsync(HttpMethod.Post, "/api/v1/migration/imports", email,
            new { kind, mode, sourceName, content });

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, string email, object? body = null)
    {
        var session = await _fixture.SignInAsync(email, Tenant);
        var client = _fixture.CreateClient();

        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.Headers.Add("Cookie", $"dfoss_session={session.SessionToken}");
        request.Headers.Add("X-Tenant", Tenant);

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

    /// <summary>
    /// Two vehicles sharing a prefix, so a run cannot collide with the rows any
    /// other test left behind in the shared development database.
    /// </summary>
    private static string VehicleFile(string prefix) =>
        "vin,modelyear,make,model,trim\n"
        + $"{Vin17(prefix, 'A')},2021,Toyota,RAV4,XLE\n"
        + $"{Vin17(prefix, 'B')},2019,Ford,F-150,";

    /// <summary>
    /// A full seventeen characters, because anything shorter needs a written
    /// reason and these rows are testing the ordinary path.
    /// </summary>
    private static string Vin17(string prefix, char suffix) =>
        $"{prefix}{suffix}".PadRight(16, '0') + suffix;

    /// <summary>
    /// Ten characters from the VIN alphabet — no I, O, or Q, which a VIN never
    /// contains so they cannot be confused with 1 and 0.
    /// </summary>
    private static string Unique() =>
        new(Guid.NewGuid().ToString("N")[..10].ToUpperInvariant().ToCharArray());

    private static int Sums(JsonElement job) =>
        job.GetProperty("rowsCreated").GetInt32()
        + job.GetProperty("rowsUpdated").GetInt32()
        + job.GetProperty("rowsSkipped").GetInt32()
        + job.GetProperty("rowsFailed").GetInt32();
}
