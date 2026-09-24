// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   OutputEncodingTests — what happens when a dealership's own data contains
//   markup, on the one surface that builds HTML out of it.
//
//   Everything else this application returns is JSON, where a value is a value
//   and a serializer that got this wrong would be the news. The printed
//   documents are the exception: DocumentService assembles a page out of
//   customer names, vehicle descriptions, line descriptions and notes, all of
//   which a person types. That makes it the one place where a stored value can
//   become an executed script, and it is reached by exactly the people who must
//   be able to trust it — the customer it is handed to, and the office that
//   files it.
//
// Usage:
//   Runs with the normal test suite; needs a reachable SQL engine.
//
// Coding Instructions:
//   THE PAYLOAD IS STORED, NOT REFLECTED. It is written through the ordinary
//   API, read back through the ordinary document route, and the test asserts on
//   what a browser would receive — so it fails for an encoder applied at the
//   wrong end as well as for one missing altogether.
//
//   Do not weaken this to "does not contain <script>". A document that dropped
//   the name entirely would pass that and be a different bug. It asserts the
//   name survives ENCODED, which is the only outcome that is both safe and
//   correct: a customer really called "Bob & Sons <Motors>" must read that way
//   on their own paperwork.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class OutputEncodingTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string Manager = DevelopmentSeeder.DevUsers.OrganizationWideEmail;

    /// <summary>
    /// Angle brackets, an ampersand and a quote — between them they cover every
    /// character that changes the meaning of surrounding markup.
    /// </summary>
    private const string Markup = "<script>alert('x')</script> & \"Sons\"";

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task Markup_in_a_customers_name_is_printed_as_text_not_run_as_script()
    {
        var made = await AJobForACustomerCalledAsync(Markup);

        var invoice = await DocumentAsync($"/api/v1/documents/repair-orders/{made.JobId}");

        invoice.Should().NotContain("<script>",
            because: "a name is a value on this page, and a value that opens a tag has become code");

        invoice.Should().Contain("&lt;script&gt;",
            because: "and it must still be THERE — a document that silently dropped the customer's "
                + "name would pass a test that only looked for the tag");

        invoice.Should().Contain("&amp;",
            because: "a dealership genuinely called 'Bob & Sons' has to read correctly on its own paperwork");
    }

    [Fact]
    public async Task Markup_in_what_the_customer_said_is_printed_as_text_too()
    {
        // The complaint is free text typed at a service counter, which makes it
        // the likeliest field in the system to contain whatever somebody pasted.
        var made = await AJobForACustomerCalledAsync("Okonkwo", complaint: $"Noise on turning {Markup}");

        var invoice = await DocumentAsync($"/api/v1/documents/repair-orders/{made.JobId}");

        invoice.Should().NotContain("<script>");
        invoice.Should().Contain("&lt;script&gt;");
    }

    /// <summary>
    /// The header every response carries, on the document route as well. The
    /// encoder above is the control; this is the second line, and it is the one
    /// that still holds if a future field is interpolated without it.
    /// </summary>
    [Fact]
    public async Task The_printed_page_is_served_under_a_policy_that_forbids_inline_script()
    {
        var made = await AJobForACustomerCalledAsync("Halvorsen");

        using var response = await SendAsync(
            HttpMethod.Get, $"/api/v1/documents/repair-orders/{made.JobId}", Manager);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var policy = response.Headers.GetValues("Content-Security-Policy").Single();
        policy.Should().Contain("script-src 'self'");
        policy.Should().NotContain("script-src 'self' 'unsafe-inline'");

        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff",
            because: "a document served as HTML must not also be sniffable as something else");
    }

    // --- building something to print ----------------------------------------

    private sealed record Made(Guid CustomerId, Guid JobId);

    private async Task<Made> AJobForACustomerCalledAsync(string surname, string? complaint = null)
    {
        var rooftop = await RooftopAsync("NAG-01");
        var tag = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var customer = (await WriteAsync("/api/v1/customers", new
        {
            kind = "Person",
            firstName = "Ingrid",
            lastName = surname,
            email = $"i.{tag}@example.test",
            phone = "555-0170",
        }, HttpStatusCode.Created)).GetProperty("id").GetGuid();

        var vehicle = (await WriteAsync("/api/v1/vehicles", new
        {
            vin = $"YV1RS58D{tag}2",   // 8 + 8 + 1 = the 17 a real VIN has
            modelYear = 2020,
            make = "Saab",
            model = "9-3",
            bodyStyle = "Saloon",
            exteriorColor = "Black",
        }, HttpStatusCode.Created)).GetProperty("id").GetGuid();

        var job = (await WriteAsync("/api/v1/repair-orders", new
        {
            rooftopId = rooftop,
            customerId = customer,
            vehicleId = vehicle,
            complaint = complaint ?? "Service due.",
            currency = "USD",
            odometerReading = 44100,
        }, HttpStatusCode.Created)).GetProperty("id").GetGuid();

        await WriteAsync($"/api/v1/repair-orders/{job}/lines", new
        {
            kind = "Labour", description = "Annual service", hours = 1m, rate = 120m,
        }, HttpStatusCode.OK);

        return new Made(customer, job);
    }

    // --- plumbing ------------------------------------------------------------

    private async Task<string> DocumentAsync(string path)
    {
        using var response = await SendAsync(HttpMethod.Get, path, Manager);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: await response.Content.ReadAsStringAsync());

        return await response.Content.ReadAsStringAsync();
    }

    private async Task<Guid> RooftopAsync(string code)
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/v1/organization", Manager);

        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("legalEntities").EnumerateArray()
            .SelectMany(e => e.GetProperty("rooftops").EnumerateArray())
            .Single(r => r.GetProperty("code").GetString() == code)
            .GetProperty("id").GetGuid();
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
