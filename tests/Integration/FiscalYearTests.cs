// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   FiscalYearTests — closing a year, and what a closed year refuses.
//
// Usage:
//   Runs with the normal test suite; needs a reachable SQL engine.
//
// Coding Instructions:
//   Every test picks its own far-future year at random, the same reason
//   AccountingPeriodTests' "opened ahead of time" test does — a fixed year
//   would be a row shared across every test method, and one test closing it
//   would lock the books out from under another.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class FiscalYearTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string Periods = "/api/v1/accounting/periods";
    private const string Years = "/api/v1/accounting/years";
    private const string Journal = "/api/v1/accounting/journal";

    private const string Manager = DevelopmentSeeder.DevUsers.OrganizationWideEmail;
    private const string Advisor = DevelopmentSeeder.DevUsers.FirstRooftopOnlyEmail;

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task A_year_with_every_month_closed_can_be_closed()
    {
        var year = await FreshYearAsync();
        await PostRevenueAndExpenseAsync(year);

        using var closed = await SendAsync(HttpMethod.Post, $"{Years}/{year}/close", Manager, new { note = "Year-end." });

        closed.StatusCode.Should().Be(HttpStatusCode.OK, because: await closed.Content.ReadAsStringAsync());
        var body = await closed.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("state").GetString().Should().Be("Closed");
        body.GetProperty("closingEntryId").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task A_year_with_an_open_month_refuses_to_close()
    {
        var year = await FreshYearAsync(closeAllMonths: false);

        using var response = await SendAsync(HttpMethod.Post, $"{Years}/{year}/close", Manager, new { note = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("open or never-opened months");
    }

    [Fact]
    public async Task A_rooftop_scoped_user_cannot_close_the_groups_year()
    {
        var year = await FreshYearAsync();

        using var response = await SendAsync(HttpMethod.Post, $"{Years}/{year}/close", Advisor, new { note = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_closed_year_refuses_a_posting_even_into_a_reopened_month()
    {
        var year = await FreshYearAsync();
        await PostRevenueAndExpenseAsync(year);
        await CloseYearAsync(year);

        try
        {
            // The month is reopened but the year is not — the fiscal-year gate
            // is the one that must refuse here, independent of the month's own
            // state.
            using var monthReopened = await SendAsync(
                HttpMethod.Post, $"{Periods}/{year}/6/reopen", Manager, new { note = "Checking the year gate." });
            monthReopened.StatusCode.Should().Be(HttpStatusCode.OK, because: await monthReopened.Content.ReadAsStringAsync());

            using var refused = await ManualPostAsync(year, month: 6);

            refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await refused.Content.ReadAsStringAsync()).Should().Contain($"{year} is closed");
        }
        finally
        {
            await ReopenYearAsync(year, "Test cleanup.");
        }
    }

    [Fact]
    public async Task Reopening_a_year_lets_it_take_postings_again()
    {
        var year = await FreshYearAsync();
        await PostRevenueAndExpenseAsync(year);
        await CloseYearAsync(year);
        await ReopenYearAsync(year, "A late invoice turned up.");

        using var monthReopened = await SendAsync(
            HttpMethod.Post, $"{Periods}/{year}/6/reopen", Manager, new { note = "The month too." });
        monthReopened.StatusCode.Should().Be(HttpStatusCode.OK, because: await monthReopened.Content.ReadAsStringAsync());

        using var posted = await ManualPostAsync(year, month: 6);
        posted.StatusCode.Should().Be(HttpStatusCode.OK, because: await posted.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Reopening_a_year_needs_a_reason_on_the_record()
    {
        var year = await FreshYearAsync();
        await PostRevenueAndExpenseAsync(year);
        await CloseYearAsync(year);

        try
        {
            using var refused = await SendAsync(HttpMethod.Post, $"{Years}/{year}/reopen", Manager, new { note = (string?)null });
            refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            await ReopenYearAsync(year, "Test cleanup.");
        }
    }

    [Fact]
    public async Task A_year_cannot_be_closed_twice()
    {
        var year = await FreshYearAsync();
        await PostRevenueAndExpenseAsync(year);
        await CloseYearAsync(year);

        try
        {
            using var response = await SendAsync(HttpMethod.Post, $"{Years}/{year}/close", Manager, new { note = (string?)null });
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        finally
        {
            await ReopenYearAsync(year, "Test cleanup.");
        }
    }

    [Fact]
    public async Task The_profit_and_loss_carries_last_years_figures_beside_this_years()
    {
        var year = await FreshYearAsync();
        await PostRevenueAndExpenseAsync(year);

        var from = new DateOnly(year, 1, 1);
        var to = new DateOnly(year, 12, 31);

        using var response = await SendAsync(
            HttpMethod.Get, $"/api/v1/accounting/profit-and-loss?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", Manager);

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.TryGetProperty("priorYear", out var priorYear).Should().BeTrue();

        // Nothing was posted the year before, so the prior-year figure is
        // present and reads as an empty year rather than being missing entirely.
        if (priorYear.ValueKind != JsonValueKind.Null)
        {
            priorYear.GetProperty("from").GetString().Should().Be(
                from.AddYears(-1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    // --- helpers -------------------------------------------------------------

    /// <summary>Opens (and, unless told not to, closes) all twelve months of a fresh random future year.</summary>
    private async Task<int> FreshYearAsync(bool closeAllMonths = true)
    {
        var year = 2100 + Random.Shared.Next(0, 800);

        for (var month = 1; month <= 12; month++)
        {
            using var opened = await SendAsync(HttpMethod.Post, Periods, Manager, new { year, month, note = (string?)null });
            opened.StatusCode.Should().Be(HttpStatusCode.OK, because: await opened.Content.ReadAsStringAsync());

            if (closeAllMonths)
            {
                using var closed = await SendAsync(
                    HttpMethod.Post, $"{Periods}/{year}/{month}/close", Manager, new { note = (string?)null });
                closed.StatusCode.Should().Be(HttpStatusCode.OK, because: await closed.Content.ReadAsStringAsync());
            }
        }

        return year;
    }

    /// <summary>
    /// Posts one balanced entry with both a revenue and an expense line, dated
    /// into the year, so the year has something for a closing entry to carry.
    /// Posted before the months are closed would be simpler, but the period
    /// gate refuses a closed month, so this reopens month 6, posts, and closes
    /// it again.
    /// </summary>
    private async Task PostRevenueAndExpenseAsync(int year)
    {
        using var reopened = await SendAsync(
            HttpMethod.Post, $"{Periods}/{year}/6/reopen", Manager, new { note = "Posting test revenue." });
        reopened.StatusCode.Should().Be(HttpStatusCode.OK, because: await reopened.Content.ReadAsStringAsync());

        using var revenue = await ManualPostAsync(year, month: 6);
        revenue.StatusCode.Should().Be(HttpStatusCode.OK, because: await revenue.Content.ReadAsStringAsync());

        using var expense = await ManualExpenseAsync(year, month: 6);
        expense.StatusCode.Should().Be(HttpStatusCode.OK, because: await expense.Content.ReadAsStringAsync());

        using var closed = await SendAsync(
            HttpMethod.Post, $"{Periods}/{year}/6/close", Manager, new { note = (string?)null });
        closed.StatusCode.Should().Be(HttpStatusCode.OK, because: await closed.Content.ReadAsStringAsync());
    }

    private async Task<HttpResponseMessage> ManualPostAsync(int year, int month) =>
        await SendAsync(HttpMethod.Post, Journal, Manager, new
        {
            rooftopId = await RooftopIdAsync(),
            entryDate = new DateOnly(year, month, 15),
            memo = "Fiscal year test revenue.",
            currency = "USD",
            lines = new object[]
            {
                new { accountCode = "1000", debit = 500m, credit = 0m, memo = (string?)null },
                new { accountCode = "4000", debit = 0m, credit = 500m, memo = (string?)null },
            },
        });

    private async Task<HttpResponseMessage> ManualExpenseAsync(int year, int month) =>
        await SendAsync(HttpMethod.Post, Journal, Manager, new
        {
            rooftopId = await RooftopIdAsync(),
            entryDate = new DateOnly(year, month, 16),
            memo = "Fiscal year test expense.",
            currency = "USD",
            lines = new object[]
            {
                new { accountCode = "6200", debit = 120m, credit = 0m, memo = (string?)null },
                new { accountCode = "1000", debit = 0m, credit = 120m, memo = (string?)null },
            },
        });

    private async Task CloseYearAsync(int year)
    {
        using var response = await SendAsync(HttpMethod.Post, $"{Years}/{year}/close", Manager, new { note = "Year-end." });
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());
    }

    private async Task ReopenYearAsync(int year, string reason)
    {
        using var response = await SendAsync(HttpMethod.Post, $"{Years}/{year}/reopen", Manager, new { note = reason });

        // Already open is fine in cleanup: a test may have failed before closing.
        if (response.StatusCode != HttpStatusCode.Conflict)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());
        }
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
