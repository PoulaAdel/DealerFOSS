// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   SafetyRecallTests — proves the one outbound call in the product behaves when
//   the other end does not.
//
// Usage:
//   Runs with the normal test suite.
//
// Coding Instructions:
//   THESE TESTS MUST NEVER REACH THE REAL REGULATOR. Every case swaps in a
//   stub handler. A suite that quietly depends on a public service being up
//   fails on a train, and a suite that hammers a government endpoint on
//   every CI run deserves to be blocked.
//
//   The case that matters most is Unreachable: a lookup failure must read as
//   "we do not know", never as "this car is clear". Somebody decides whether
//   to hand a car over on the strength of it.

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using DealerFOSS.App;
using DealerFOSS.Vehicles;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class SafetyRecallTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string Vehicles = "/api/v1/vehicles";
    private const string Manager = DevelopmentSeeder.DevUsers.OrganizationWideEmail;

    /// <summary>Two campaigns, in the regulator's own shape and casing.</summary>
    private const string TwoCampaigns = """
        {
          "Count": 2,
          "results": [
            {
              "Manufacturer": "Honda (American Honda Motor Co.)",
              "NHTSACampaignNumber": "19V182000",
              "parkIt": false,
              "parkOutSide": false,
              "ReportReceivedDate": "06/03/2019",
              "Component": "AIR BAGS:FRONTAL:DRIVER SIDE:INFLATOR MODULE",
              "Summary": "The driver frontal air bag inflator may explode.",
              "Remedy": "Honda will replace the inflator free of charge."
            },
            {
              "Manufacturer": "Honda (American Honda Motor Co.)",
              "NHTSACampaignNumber": "23V101000",
              "parkIt": true,
              "parkOutSide": true,
              "ReportReceivedDate": "02/17/2023",
              "Component": "ELECTRICAL SYSTEM",
              "Summary": "An electrical fault may cause a fire.",
              "Remedy": "Dealers will inspect and repair free of charge."
            }
          ]
        }
        """;

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task A_vehicle_reports_the_campaigns_published_for_its_model()
    {
        var vehicleId = await AddVehicleAsync();

        using var response = await RecallsAsync(vehicleId, Respond(HttpStatusCode.OK, TwoCampaigns));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<JsonElement>();

        var campaigns = report.GetProperty("campaigns").EnumerateArray().ToList();
        campaigns.Should().HaveCount(2);

        // Newest first: a person scanning the list wants the recent one.
        campaigns[0].GetProperty("campaignNumber").GetString().Should().Be("23V101000");
        campaigns[0].GetProperty("doNotDrive").GetBoolean().Should().BeTrue();
        campaigns[0].GetProperty("parkOutside").GetBoolean().Should().BeTrue();
        campaigns[1].GetProperty("campaignNumber").GetString().Should().Be("19V182000");
    }

    /// <summary>
    /// The caveat travels with the data. The regulator indexes by model, so a
    /// campaign listed here may well have been carried out on this particular car
    /// years ago — only the manufacturer knows. A caller must be handed that fact
    /// rather than be expected to remember it.
    /// </summary>
    [Fact]
    public async Task The_answer_says_it_is_about_the_model_rather_than_this_car()
    {
        var vehicleId = await AddVehicleAsync();

        using var response = await RecallsAsync(vehicleId, Respond(HttpStatusCode.OK, TwoCampaigns));

        var report = await response.Content.ReadFromJsonAsync<JsonElement>();
        report.GetProperty("appliesToModelNotVehicle").GetBoolean().Should().BeTrue();
    }

    /// <summary>
    /// The case this whole file exists for. "We could not ask" and "there is
    /// nothing to worry about" must never arrive looking the same, so an
    /// unreachable regulator is a 503 with its own code — not an empty list, and
    /// not a 500 that reads as our own fault.
    /// </summary>
    [Fact]
    public async Task An_unreachable_regulator_is_refused_rather_than_reported_as_no_recalls()
    {
        var vehicleId = await AddVehicleAsync();

        using var response = await RecallsAsync(
            vehicleId, _ => throw new HttpRequestException("connection refused"));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("recalls.unavailable");
        body.Should().NotContain("\"campaigns\"",
            because: "a failure must not be shaped like an all-clear");
    }

    [Fact]
    public async Task A_regulator_that_answers_with_an_error_is_treated_the_same_way()
    {
        var vehicleId = await AddVehicleAsync();

        using var response = await RecallsAsync(
            vehicleId, Respond(HttpStatusCode.InternalServerError, "upstream exploded"));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    /// <summary>
    /// A model with no campaigns is a real answer and must be distinguishable
    /// from a failure — the whole point of the test above.
    /// </summary>
    [Fact]
    public async Task A_model_with_nothing_against_it_returns_an_empty_list_and_a_200()
    {
        var vehicleId = await AddVehicleAsync();

        using var response = await RecallsAsync(
            vehicleId, Respond(HttpStatusCode.OK, """{"Count":0,"results":[]}"""));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<JsonElement>();
        report.GetProperty("campaigns").EnumerateArray().Should().BeEmpty();
    }

    /// <summary>
    /// The regulator is asked in its own terms — year, make and model — because
    /// that is what it indexes on. It is not a VIN lookup, and a caller reading
    /// the code should be able to see that from the request itself.
    /// </summary>
    [Fact]
    public async Task The_regulator_is_asked_by_year_make_and_model_not_by_vin()
    {
        var vehicleId = await AddVehicleAsync();
        Uri? asked = null;

        using var response = await RecallsAsync(vehicleId, request =>
        {
            asked = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"Count":0,"results":[]}""", Encoding.UTF8, "application/json"),
            };
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        asked.Should().NotBeNull();

        var query = asked!.Query;
        query.Should().Contain("make=Honda");
        query.Should().Contain("model=Accord");
        query.Should().Contain("modelYear=2003");
    }

    // --- helpers -------------------------------------------------------------

    private static Func<HttpRequestMessage, HttpResponseMessage> Respond(
        HttpStatusCode status, string body) =>
        _ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    /// <summary>
    /// Runs one recall request against a host whose outbound handler is the stub.
    /// A derived factory, so the shared fixture other tests use is untouched.
    /// </summary>
    private async Task<HttpResponseMessage> RecallsAsync(
        string vehicleId,
        Func<HttpRequestMessage, HttpResponseMessage> regulator)
    {
        using var factory = _fixture.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddHttpClient<ISafetyRecalls, NhtsaSafetyRecalls>()
                    .ConfigurePrimaryHttpMessageHandler(() => new StubHandler(regulator))));

        using var client = factory.CreateClient();
        var session = await _fixture.SignInAsync(Manager, Tenant);

        using var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri($"{Vehicles}/{vehicleId}/recalls", UriKind.Relative));
        request.Headers.Add("X-Tenant", Tenant);
        request.Headers.Add("Cookie", $"dfoss_session={session.SessionToken}");

        return await client.SendAsync(request);
    }

    private async Task<string> AddVehicleAsync()
    {
        using var client = _fixture.CreateClient();
        var session = await _fixture.SignInAsync(Manager, Tenant);

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Vehicles, UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                vin = UniqueVin(),
                modelYear = 2003,
                make = "Honda",
                model = "Accord",
            }),
        };

        request.Headers.Add("X-Tenant", Tenant);
        request.Headers.Add("Cookie", $"dfoss_session={session.SessionToken}");
        request.Headers.Add("X-CSRF-Token", session.AntiForgeryToken);

        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        return detail.GetProperty("id").GetString()!;
    }

    private static string UniqueVin()
    {
        var body = Guid.NewGuid().ToString("N").ToUpperInvariant()
            .Replace("I", "1", StringComparison.Ordinal)
            .Replace("O", "0", StringComparison.Ordinal)
            .Replace("Q", "9", StringComparison.Ordinal);

        return body[..17];
    }

    /// <summary>Answers every outbound request from a function, reaching nothing.</summary>
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
