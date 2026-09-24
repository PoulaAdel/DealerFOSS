// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   RecordAuthorizationTests — the attacks a person with a real, valid session
//   would actually try, run against every capability rather than against one.
//
//   RooftopAuthorizationTests proves the rule holds in the Organization
//   capability. It says so itself: "Cover both routes when you extend them."
//   Nothing extended them, so until this file the other eleven by-id routes
//   were held only by the code being written correctly, which is not evidence.
//   The attacker here is a real seeded user — the advisor at NAG-01 — holding a
//   genuine session and every read permission their role carries. The only
//   thing they do not have is the rooftop.
//
// Usage:
//   Runs with the normal test suite; needs a reachable SQL engine.
//
// Coding Instructions:
//   THE ORACLE TEST IS THE POINT OF THIS FILE, not the refusal test. That a
//   record at another lot is refused is the easy half and was already mostly
//   true. The hard half is that it is refused in a way that does not ALSO
//   answer "…but it does exist" — because the difference between the two
//   answers is itself the disclosure. It found a real defect on the first run:
//   /receivables/for/{source}/{reference} answered 204 for a reference that
//   does not exist and 403 for one belonging to a lot the caller cannot see,
//   so any signed-in caller could confirm that a given deal or job had been
//   billed anywhere in the group. Fixed in ReceivableService.FindAsync.
//
//   Both halves of each pair must be compared, the status AND the error code.
//   A route that answered 403 to both but named a different code would still
//   be an oracle, and asserting only the status would not see it.
//
//   Add a row to TheOtherLot whenever a capability gains a by-id route. A
//   route that is not in the table is not tested, and the table is the only
//   thing that says so out loud.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

/// <summary>
/// A caller holding a valid session at one lot, reaching for records at
/// another. Every route that takes a record id is tried twice: once with the
/// id of a record that really is at the other lot, and once with an id that is
/// nowhere at all. The two answers have to be identical.
/// </summary>
[Collection(nameof(HostCollection))]
public sealed class RecordAuthorizationTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";

    /// <summary>Organization-wide. Builds the records; never the attacker.</summary>
    private const string Manager = DevelopmentSeeder.DevUsers.OrganizationWideEmail;

    /// <summary>
    /// The attacker. Assigned to NAG-01 only, and holds the READ permission for
    /// every capability below — inventory, leads, deals, service, accounting and
    /// the organization itself. So a refusal here is about the rooftop and
    /// nothing else, which is what makes the test about scope rather than about
    /// whether somebody remembered to check a permission.
    /// </summary>
    private const string Advisor = DevelopmentSeeder.DevUsers.FirstRooftopOnlyEmail;

    private readonly HostFixture _fixture = fixture;

    /// <summary>
    /// Built once and shared. Every test here only reads, so re-making the set
    /// per test would cost six writes each to prove the same thing.
    /// </summary>
    private static readonly SemaphoreSlim BuildLock = new(1, 1);
    private static OtherLot? _otherLot;

    // --- the attacks -------------------------------------------------------

    [Theory]
    [MemberData(nameof(ByIdRoutes))]
    public async Task A_record_at_another_lot_is_refused_by_direct_id(string name, string template)
    {
        var lot = await TheOtherLotAsync();

        using var response = await SendAsync(HttpMethod.Get, lot.Route(template), Advisor);

        response.StatusCode.Should().NotBe(HttpStatusCode.OK,
            because: $"{name} belongs to NAG-02 and the caller is assigned to NAG-01 only");
    }

    /// <summary>
    /// The half that matters. A refusal that differs from "there is no such
    /// record" tells the caller the record exists, which is the fact the scope
    /// rule exists to keep from them.
    /// </summary>
    [Theory]
    [MemberData(nameof(ByIdRoutes))]
    public async Task A_record_that_does_not_exist_answers_exactly_like_one_at_another_lot(
        string name, string template)
    {
        var lot = await TheOtherLotAsync();

        // A Guid nobody has ever issued. Whatever the application says about
        // this one is what it must also say about a record it is hiding.
        var nowhere = template.Replace("{id}", Guid.NewGuid().ToString(), StringComparison.Ordinal);

        using var absent = await SendAsync(HttpMethod.Get, nowhere, Advisor);
        using var hidden = await SendAsync(HttpMethod.Get, lot.Route(template), Advisor);

        hidden.StatusCode.Should().Be(absent.StatusCode,
            because: $"{name} must not reveal, by answering differently, that the record is really there");

        (await CodeOf(hidden)).Should().Be(await CodeOf(absent),
            because: $"{name} must not name a different reason either — the code is as much an answer "
                + "as the status");
    }

    /// <summary>
    /// The printed document is the route most easily forgotten, because it
    /// renders rather than returns and reads its record through another
    /// capability. It is also the one that puts a customer's name, address and
    /// figures on one page.
    /// </summary>
    [Theory]
    [InlineData("the vehicle order", "/api/v1/documents/deals/{id}", "deal")]
    [InlineData("the service invoice", "/api/v1/documents/repair-orders/{id}", "job")]
    public async Task A_document_for_a_record_at_another_lot_does_not_print(
        string name, string template, string which)
    {
        var lot = await TheOtherLotAsync();
        var id = which == "deal" ? lot.DealId : lot.JobId;

        using var response = await SendAsync(
            HttpMethod.Get, template.Replace("{id}", id.ToString(), StringComparison.Ordinal), Advisor);

        response.StatusCode.Should().NotBe(HttpStatusCode.OK, because: $"{name} is NAG-02's paperwork");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(lot.CustomerSurname,
            because: "a refusal must not print the customer it refused to print");
    }

    /// <summary>
    /// The list routes take a rooftop filter. Asking for a lot the caller
    /// cannot see must be a refusal rather than a wider answer — and it must
    /// read the same as asking for a rooftop that does not exist.
    /// </summary>
    [Theory]
    [InlineData("/api/v1/inventory?rooftopId={id}")]
    [InlineData("/api/v1/leads?rooftopId={id}")]
    [InlineData("/api/v1/deals?rooftopId={id}")]
    [InlineData("/api/v1/repair-orders?rooftopId={id}")]
    [InlineData("/api/v1/receivables?rooftopId={id}")]
    [InlineData("/api/v1/appointments?rooftopId={id}")]
    [InlineData("/api/v1/accounting/journal?rooftopId={id}")]
    public async Task A_list_cannot_be_widened_to_another_lot_by_asking_for_it(string template)
    {
        var lot = await TheOtherLotAsync();

        using var asked = await SendAsync(
            HttpMethod.Get,
            template.Replace("{id}", lot.RooftopId.ToString(), StringComparison.Ordinal),
            Advisor);

        using var nowhere = await SendAsync(
            HttpMethod.Get,
            template.Replace("{id}", Guid.NewGuid().ToString(), StringComparison.Ordinal),
            Advisor);

        asked.StatusCode.Should().NotBe(HttpStatusCode.OK,
            because: "a filter is not a grant — naming a lot must not widen what the caller may see");

        asked.StatusCode.Should().Be(nowhere.StatusCode,
            because: "and refusing a real lot differently from an imaginary one would say which lots exist");
    }

    /// <summary>
    /// The write half of the same question: not "may I read that lot" but "may
    /// I put something in it". The salesperson holds Inventory.Manage — at
    /// NAG-01. Naming NAG-02 in the body is the ordinary shape of a
    /// mass-assignment attempt, and it has to be checked at the value rather
    /// than at the route, because the route says nothing about a rooftop.
    /// </summary>
    [Fact]
    public async Task A_write_that_names_another_lot_in_its_body_is_refused()
    {
        var lot = await TheOtherLotAsync();
        var tag = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        using var response = await SendAsync(
            HttpMethod.Post,
            "/api/v1/inventory",
            DevelopmentSeeder.DevUsers.SalespersonEmail,
            new
            {
                vehicleId = lot.VehicleId,
                rooftopId = lot.RooftopId,
                stockNumber = $"XLOT-{tag}",
                costAmount = 1000m,
                costCurrency = "USD",
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "holding Inventory.Manage at one lot is not holding it at the one named in the body");
    }

    /// <summary>
    /// The records-package endpoints landed on 2026-09-21 and write across five
    /// capabilities at once. Exporting is already covered; importing was not,
    /// and it is the half that writes.
    /// </summary>
    [Fact]
    public async Task Applying_a_package_needs_the_right_across_the_whole_group()
    {
        var lot = await TheOtherLotAsync();

        using var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/migration/packages/{lot.RooftopId}",
            Advisor,
            new { content = "{\"format\":\"dealerfoss.package\",\"version\":1}" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "writing a lot's records in bulk is a group-wide right, and an advisor does not hold it");
    }

    /// <summary>
    /// And the same refusal for the caller's OWN lot, which is the version
    /// somebody would argue for. A one-lot manager who could apply a package
    /// could rewrite the group's customer list, because customers are not
    /// scoped to a lot at all.
    /// </summary>
    [Fact]
    public async Task Applying_a_package_is_refused_even_at_the_callers_own_lot()
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/migration/packages/{await RooftopAsync("NAG-01")}",
            Advisor,
            new { content = "{\"format\":\"dealerfoss.package\",\"version\":1}" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A caller holding no assignment at all. Every route must refuse, and none
    /// may fall through to an unfiltered answer — the failure mode where "no
    /// scope" is read as "no filter".
    /// </summary>
    [Theory]
    [MemberData(nameof(ByIdRoutes))]
    public async Task Somebody_with_no_assignment_reaches_nothing(string name, string template)
    {
        var lot = await TheOtherLotAsync();

        using var response = await SendAsync(
            HttpMethod.Get, lot.Route(template), DevelopmentSeeder.DevUsers.UnassignedEmail);

        response.StatusCode.Should().NotBe(HttpStatusCode.OK,
            because: $"no assignment must mean no access to {name}, never unfiltered access");
    }

    // --- the routes under test ---------------------------------------------

    /// <summary>
    /// Every route that takes a record id, with a record really at NAG-02 to
    /// put in it. The name is what a failure message says, so it is written for
    /// somebody reading the output rather than the code.
    /// </summary>
    public static TheoryData<string, string> ByIdRoutes() => new()
    {
        { "a car in stock", "/api/v1/inventory/{id}" },
        { "an enquiry", "/api/v1/leads/{id}" },
        { "a deal", "/api/v1/deals/{id}" },
        { "a job in the workshop", "/api/v1/repair-orders/{id}" },
        { "a booking", "/api/v1/appointments/{id}" },
        { "a bill", "/api/v1/receivables/{id}" },
        { "a ledger entry", "/api/v1/accounting/journal/{id}" },
        { "a lot itself", "/api/v1/organization/rooftops/{id}" },
        // The lookup that does not take a record id but a reference to one.
        // This is where the oracle was found: it answered 204 for a reference
        // nobody has and 403 for one at a lot the caller cannot see.
        { "a bill looked up by what it is for", "/api/v1/receivables/for/RepairOrder/{id}" },
    };

    // --- building something at the other lot --------------------------------

    /// <summary>
    /// Records that really are at NAG-02, and the ids of each. Held as one
    /// record so <see cref="Route"/> can fill any template in the table above
    /// without each test knowing which id belongs to which route.
    /// </summary>
    private sealed record OtherLot(
        Guid RooftopId,
        Guid CustomerId,
        string CustomerSurname,
        Guid VehicleId,
        Guid UnitId,
        Guid LeadId,
        Guid DealId,
        Guid JobId,
        Guid AppointmentId,
        Guid ReceivableId,
        Guid JournalEntryId)
    {
        public string Route(string template) => template.Replace(
            "{id}",
            template switch
            {
                var t when t.Contains("/inventory/", StringComparison.Ordinal) => UnitId.ToString(),
                var t when t.Contains("/leads/", StringComparison.Ordinal) => LeadId.ToString(),
                var t when t.Contains("/deals/", StringComparison.Ordinal) => DealId.ToString(),
                var t when t.Contains("/receivables/for/", StringComparison.Ordinal) => JobId.ToString(),
                var t when t.Contains("/repair-orders/", StringComparison.Ordinal) => JobId.ToString(),
                var t when t.Contains("/appointments/", StringComparison.Ordinal) => AppointmentId.ToString(),
                var t when t.Contains("/receivables/", StringComparison.Ordinal) => ReceivableId.ToString(),
                var t when t.Contains("/journal/", StringComparison.Ordinal) => JournalEntryId.ToString(),
                var t when t.Contains("/rooftops/", StringComparison.Ordinal) => RooftopId.ToString(),
                _ => throw new InvalidOperationException($"No record is held for {template}."),
            },
            StringComparison.Ordinal);
    }

    private async Task<OtherLot> TheOtherLotAsync()
    {
        if (_otherLot is { } already)
        {
            return already;
        }

        await BuildLock.WaitAsync();
        try
        {
            return _otherLot ??= await BuildTheOtherLotAsync();
        }
        finally
        {
            BuildLock.Release();
        }
    }

    /// <summary>
    /// One of everything at NAG-02, built through the API as the group manager
    /// — the same way a person would. Built rather than seeded because the
    /// seed puts only a car and an enquiry at the second lot, and a test that
    /// only covers what the seed happens to contain covers whatever somebody
    /// last needed for a different reason.
    /// </summary>
    private async Task<OtherLot> BuildTheOtherLotAsync()
    {
        var rooftop = await RooftopAsync("NAG-02");
        var tag = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var surname = $"Lindqvist{tag}";

        var customer = (await WriteAsync("/api/v1/customers", new
        {
            kind = "Person",
            firstName = "Annika",
            lastName = surname,
            email = $"a.lindqvist.{tag}@example.test",
            phone = "555-0188",
        }, HttpStatusCode.Created)).GetProperty("id").GetGuid();

        var vehicle = (await WriteAsync("/api/v1/vehicles", new
        {
            vin = $"WVWZZZ1K{tag}7",   // 8 + 8 + 1 = the 17 a real VIN has
            modelYear = 2021,
            make = "Volvo",
            model = "XC60",
            trim = "Momentum",
            bodyStyle = "SUV",
            exteriorColor = "Denim Blue",
        }, HttpStatusCode.Created)).GetProperty("id").GetGuid();

        var unit = (await WriteAsync("/api/v1/inventory", new
        {
            vehicleId = vehicle,
            rooftopId = rooftop,
            stockNumber = $"OTH-{tag}",
            costAmount = 21750m,
            costCurrency = "USD",
        }, HttpStatusCode.Created)).GetProperty("id").GetGuid();

        await WriteAsync($"/api/v1/inventory/{unit}/status",
            new { status = "Available", note = (string?)null }, HttpStatusCode.OK);

        var lead = (await WriteAsync("/api/v1/leads", new
        {
            rooftopId = rooftop,
            customerId = customer,
            source = "Website",
            enquiry = "Asked what the XC60 would be with the trade-in.",
        }, HttpStatusCode.Created)).GetProperty("id").GetGuid();

        var deal = (await WriteAsync("/api/v1/deals", new
        {
            rooftopId = rooftop, customerId = customer, inventoryUnitId = unit, currency = "USD",
        }, HttpStatusCode.Created)).GetProperty("id").GetGuid();

        await WriteAsync($"/api/v1/deals/{deal}/terms", new
        {
            charges = new object[]
            {
                new { kind = "VehiclePrice", description = "2021 Volvo XC60 Momentum", amount = 26500m },
            },
            tradeIn = (object?)null,
        }, HttpStatusCode.OK);

        var appointment = (await WriteAsync("/api/v1/appointments", new
        {
            rooftopId = rooftop,
            customerId = customer,
            vehicleId = vehicle,
            scheduledFor = DateTimeOffset.UtcNow.AddDays(3),
            reason = "First service.",
            estimatedHours = 1.5m,
        }, HttpStatusCode.Created)).GetProperty("id").GetGuid();

        // Invoiced, because that is what raises the bill and posts the ledger
        // entry — the two records at the end of the list that cannot be made
        // any other way.
        var job = (await WriteAsync("/api/v1/repair-orders", new
        {
            rooftopId = rooftop,
            customerId = customer,
            vehicleId = vehicle,
            complaint = "Service due.",
            currency = "USD",
            odometerReading = 18200,
        }, HttpStatusCode.Created)).GetProperty("id").GetGuid();

        await WriteAsync($"/api/v1/repair-orders/{job}/lines", new
        {
            kind = "Labour", description = "Annual service", hours = 1.5m, rate = 140m,
        }, HttpStatusCode.OK);

        foreach (var status in new[] { "InProgress", "Completed", "Invoiced" })
        {
            await WriteAsync($"/api/v1/repair-orders/{job}/status",
                new { status, note = (string?)null }, HttpStatusCode.OK);
        }

        var receivable = (await ReadAsync($"/api/v1/receivables/for/RepairOrder/{job}"))
            .GetProperty("id").GetGuid();

        var entry = (await ReadAsync($"/api/v1/accounting/journal?rooftopId={rooftop}&limit=1"))
            .GetProperty("rows").EnumerateArray().First().GetProperty("id").GetGuid();

        return new OtherLot(
            rooftop, customer, surname, vehicle, unit, lead, deal, job, appointment, receivable, entry);
    }

    // --- plumbing ----------------------------------------------------------

    /// <summary>
    /// The error code out of a problem document, or the empty string when the
    /// body is not one. An empty body is itself an answer and has to compare
    /// equal to another empty body rather than blow up.
    /// </summary>
    private static async Task<string> CodeOf(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        try
        {
            var problem = JsonDocument.Parse(body);
            return problem.RootElement.TryGetProperty("code", out var code)
                ? code.GetString() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private async Task<Guid> RooftopAsync(string code)
    {
        var structure = await ReadAsync("/api/v1/organization");

        return structure.GetProperty("legalEntities").EnumerateArray()
            .SelectMany(e => e.GetProperty("rooftops").EnumerateArray())
            .Single(r => r.GetProperty("code").GetString() == code)
            .GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> ReadAsync(string path)
    {
        using var response = await SendAsync(HttpMethod.Get, path, Manager);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"{path} should have answered: " + await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> WriteAsync(string path, object body, HttpStatusCode expected)
    {
        using var response = await SendAsync(HttpMethod.Post, path, Manager, body);

        response.StatusCode.Should().Be(expected,
            because: $"{path} should have been written: " + await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, string email, object? body = null)
    {
        var session = await _fixture.SignInAsync(email, Tenant);
        using var client = _fixture.CreateClient();

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

        return await client.SendAsync(request);
    }
}
