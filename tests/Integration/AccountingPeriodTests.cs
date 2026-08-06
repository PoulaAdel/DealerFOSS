// AccountingPeriodTests — closing the month, and what a closed month refuses.
//
// Use:  runs with the normal test suite; needs a reachable SQL engine.
// Edit: every test that closes the current month reopens it in a finally block.
//       The period is a row shared by the whole suite, and a test that left this
//       month closed would fail every other test's postings with a message about
//       the books being locked — a confusing way to learn that cleanup was
//       skipped. The same trap as SecondFactorPolicyTests.
//
//       Note what is NOT tested here: that a date rule locks the month. There
//       isn't one, deliberately. Locking is an act somebody performs, because the
//       close runs over however many business days the work takes.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class AccountingPeriodTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string Periods = "/api/v1/accounting/periods";

    private const string Manager = DevelopmentSeeder.DevUsers.OrganizationWideEmail;
    private const string Advisor = DevelopmentSeeder.DevUsers.FirstRooftopOnlyEmail;

    private readonly HostFixture _fixture = fixture;

    private static (int Year, int Month) ThisMonth =>
        (DateTime.UtcNow.Year, DateTime.UtcNow.Month);

    [Fact]
    public async Task The_books_show_which_months_are_open()
    {
        using var response = await SendAsync(HttpMethod.Get, Periods, Manager);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var months = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        months.Should().NotBeEmpty(because: "setting a dealership up opens its books");

        var current = months.Single(m =>
            m.GetProperty("year").GetInt32() == ThisMonth.Year
            && m.GetProperty("month").GetInt32() == ThisMonth.Month);

        current.GetProperty("state").GetString().Should().Be("Open");
    }

    [Fact]
    public async Task A_period_states_its_cutoff_as_the_last_day_of_the_month()
    {
        using var response = await SendAsync(HttpMethod.Get, Periods, Manager);

        var current = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray()
            .First(m => m.GetProperty("month").GetInt32() == ThisMonth.Month);

        var endsOn = DateOnly.Parse(current.GetProperty("endsOn").GetString()!, null);
        endsOn.Day.Should().Be(DateTime.DaysInMonth(ThisMonth.Year, ThisMonth.Month),
            because: "the cutoff is the 30th or the 31st, whichever the month has");
    }

    [Fact]
    public async Task A_rooftop_scoped_user_cannot_close_the_groups_month()
    {
        // The books close as a whole. One lot does not close the group's month.
        using var response = await SendAsync(
            HttpMethod.Post, $"{Periods}/{ThisMonth.Year}/{ThisMonth.Month}/close", Advisor, new { note = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Closing_a_month_stops_anything_posting_into_it()
    {
        try
        {
            await CloseAsync();

            // A delivery would post into today's month, which is now closed.
            using var refused = await PostSomethingAsync();

            refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await refused.Content.ReadAsStringAsync())
                .Should().Contain("is closed",
                    because: "the refusal should say the month is locked, not fail obscurely");
        }
        finally
        {
            await ReopenAsync("Test cleanup.");
        }
    }

    [Fact]
    public async Task Reopening_a_month_lets_it_take_postings_again()
    {
        await CloseAsync();
        await ReopenAsync("An invoice turned up late.");

        using var posted = await PostSomethingAsync();
        posted.StatusCode.Should().Be(HttpStatusCode.OK, because: await posted.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Reopening_needs_a_reason_on_the_record()
    {
        try
        {
            await CloseAsync();

            using var refused = await SendAsync(
                HttpMethod.Post, $"{Periods}/{ThisMonth.Year}/{ThisMonth.Month}/reopen", Manager,
                new { note = (string?)null });

            refused.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                because: "a figure somebody reported is about to be able to move");
        }
        finally
        {
            await ReopenAsync("Test cleanup.");
        }
    }

    [Fact]
    public async Task Every_open_and_close_is_kept()
    {
        try
        {
            await CloseAsync("Month-end done.");
            await ReopenAsync("A supplier invoice arrived on the 4th.");

            using var response = await SendAsync(HttpMethod.Get, Periods, Manager);
            var current = (await response.Content.ReadFromJsonAsync<JsonElement>())
                .EnumerateArray()
                .First(m => m.GetProperty("month").GetInt32() == ThisMonth.Month);

            var history = current.GetProperty("history").EnumerateArray()
                .Select(h => h.GetProperty("note").GetString())
                .ToList();

            // Closed, reopened, and changed is exactly the sequence somebody will
            // later need to reconstruct.
            history.Should().Contain("Month-end done.");
            history.Should().Contain("A supplier invoice arrived on the 4th.");
        }
        finally
        {
            await ReopenAsync("Test cleanup.");
        }
    }

    [Fact]
    public async Task A_month_nobody_has_opened_refuses_differently_from_a_closed_one()
    {
        // Two different problems needing two different actions: open the books,
        // versus reopen a month you closed.
        using var response = await SendAsync(
            HttpMethod.Post, $"{Periods}/2031/7/close", Manager, new { note = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().Contain("never been opened");
    }

    [Fact]
    public async Task The_same_month_cannot_be_opened_twice()
    {
        using var response = await SendAsync(
            HttpMethod.Post, Periods, Manager,
            new { year = ThisMonth.Year, month = ThisMonth.Month, note = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict,
            because: "two rows for one month would mean two answers to 'is it closed?'");
    }

    [Fact]
    public async Task A_month_can_be_opened_ahead_of_time()
    {
        var year = 2032 + Random.Shared.Next(0, 500);

        using var opened = await SendAsync(
            HttpMethod.Post, Periods, Manager, new { year, month = 3, note = "Opened early." });

        opened.StatusCode.Should().Be(HttpStatusCode.OK, because: await opened.Content.ReadAsStringAsync());
        (await opened.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("state").GetString().Should().Be("Open");
    }

    [Fact]
    public async Task A_nonsense_month_is_refused_rather_than_stored()
    {
        using var response = await SendAsync(
            HttpMethod.Post, Periods, Manager, new { year = 2030, month = 13, note = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- helpers -----------------------------------------------------------

    private async Task CloseAsync(string? note = null)
    {
        using var response = await SendAsync(
            HttpMethod.Post, $"{Periods}/{ThisMonth.Year}/{ThisMonth.Month}/close", Manager, new { note });

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());
    }

    private async Task ReopenAsync(string reason)
    {
        using var response = await SendAsync(
            HttpMethod.Post, $"{Periods}/{ThisMonth.Year}/{ThisMonth.Month}/reopen", Manager, new { note = reason });

        // Already open is fine in cleanup: a test may have failed before closing.
        if (response.StatusCode != HttpStatusCode.Conflict)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());
        }
    }

    /// <summary>
    /// Invoices a repair order, which posts to the ledger.
    ///
    /// A delivered deal would post too, but it needs a car that is Available —
    /// and other tests hold those, so it fails for reasons that have nothing to
    /// do with periods. A job needs only a customer and a vehicle, both of which
    /// are shared and never consumed.
    /// </summary>
    private async Task<HttpResponseMessage> PostSomethingAsync()
    {
        var job = await CreatedAsync("/api/v1/repair-orders", new
        {
            rooftopId = await RooftopIdAsync(),
            customerId = await FirstAsync("/api/v1/customers?query=a&limit=1", "id"),
            vehicleId = await FirstAsync("/api/v1/vehicles?limit=1", "id"),
            complaint = "Something that posts.",
            currency = "USD",
        });

        await PostAsync($"/api/v1/repair-orders/{job}/lines", new
        {
            kind = "Labour", description = "An hour", hours = 1m, rate = 100m,
        });

        await PostAsync($"/api/v1/repair-orders/{job}/status", new { status = "InProgress", note = (string?)null });
        await PostAsync($"/api/v1/repair-orders/{job}/status", new { status = "Completed", note = (string?)null });

        return await SendAsync(
            HttpMethod.Post, $"/api/v1/repair-orders/{job}/status", Manager,
            new { status = "Invoiced", note = (string?)null });
    }

    private async Task<Guid> CreatedAsync(string path, object body)
    {
        using var response = await SendAsync(HttpMethod.Post, path, Manager, body);
        response.StatusCode.Should().Be(HttpStatusCode.Created, because: await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task PostAsync(string path, object body)
    {
        using var response = await SendAsync(HttpMethod.Post, path, Manager, body);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());
    }

    private async Task<Guid> FirstAsync(string path, string property)
    {
        using var response = await SendAsync(HttpMethod.Get, path, Manager);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray().First().GetProperty(property).GetGuid();
    }

    private async Task<Guid> RooftopIdAsync()
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/v1/organization", Manager);

        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("legalEntities").EnumerateArray()
            .SelectMany(e => e.GetProperty("rooftops").EnumerateArray())
            .Single(r => r.GetProperty("code").GetString() == "NAG-01")
            .GetProperty("id").GetGuid();
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, string email, object? body = null)
    {
        var session = await _fixture.SignInAsync(email, Tenant);

        using var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Headers.Add("X-Tenant", Tenant);
        request.Headers.Add("Cookie", $"dfoss_session={session.SessionToken}");

        if (method != HttpMethod.Get)
        {
            request.Headers.Add("X-CSRF-Token", session.AntiForgeryToken);
            request.Content = JsonContent.Create(body ?? new { });
        }

        return await client.SendAsync(request);
    }
}
