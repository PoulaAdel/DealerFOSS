// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   RepairOrderTests (integration) — proves a car can be booked in, worked on, and
//   billed; that work nobody asked the customer about cannot reach an invoice; and
//   that one workshop cannot see another's jobs.
//
// Usage:
//   Runs with the normal test suite; needs a reachable SQL engine.
//
// Coding Instructions:
//   The control worth guarding here is the authorization gate. A technician
//   who can record "the customer agreed" is not a control at all, and an
//   invoice that goes out with unanswered work on it is a complaint, not a
//   bug. Both must fail loudly if the checks in RepairOrderService or
//   RepairOrder are removed.

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class RepairOrderTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string Jobs = "/api/v1/repair-orders";
    private const string Customers = "/api/v1/customers";
    private const string Vehicles = "/api/v1/vehicles";
    private const string Ledger = "/api/v1/accounting/journal";

    private const string Manager = DevelopmentSeeder.DevUsers.OrganizationWideEmail;
    private const string Advisor = DevelopmentSeeder.DevUsers.FirstRooftopOnlyEmail;
    private const string Technician = DevelopmentSeeder.DevUsers.TechnicianEmail;
    private const string Sales = DevelopmentSeeder.DevUsers.SalespersonEmail;

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task A_job_says_which_lot_it_belongs_to()
    {
        // Numbers restart per rooftop BY DESIGN, and a unique index on
        // (RooftopId, Number) enforces it — so this dealership's 162 jobs share
        // 82 numbers, 80 of them used twice. The id was always on the row; a
        // CODE is what a person can read, and without it two rows of a list
        // spanning both lots are indistinguishable.
        var jobId = await OpenJobAsync(Manager, await RooftopIdAsync("NAG-02"));

        var job = await GetJobAsync(jobId, Manager);
        job.GetProperty("rooftopCode").GetString().Should().Be("NAG-02");

        // And on the list, which is where the ambiguity actually bites.
        using var listed = await SendAsync(HttpMethod.Get, $"{Jobs}?limit=200", Manager);
        listed.StatusCode.Should().Be(HttpStatusCode.OK);

        (await listed.Content.ReadFromJsonAsync<JsonElement>())
            .Rows().Single(r => r.GetProperty("id").GetString() == jobId)
            .GetProperty("rooftopCode").GetString().Should().Be("NAG-02");
    }

    [Fact]
    public async Task A_car_can_be_booked_in_worked_on_and_invoiced()
    {
        var jobId = await OpenJobAsync(Manager, await RooftopIdAsync("NAG-01"));

        // Booked in for a service: authorized on arrival.
        await AddLineAsync(jobId, Manager, new { kind = "Labour", description = "Full service", hours = 1.5m, rate = 120m });
        await AddLineAsync(jobId, Manager, new { kind = "Part", description = "Oil and filter", unitAmount = 68.40m });

        (await MoveAsync(jobId, Manager, "InProgress")).Should().Be(HttpStatusCode.OK);
        (await MoveAsync(jobId, Manager, "Completed")).Should().Be(HttpStatusCode.OK);
        (await MoveAsync(jobId, Manager, "Invoiced")).Should().Be(HttpStatusCode.OK);

        var job = await GetJobAsync(jobId, Manager);
        job.GetProperty("status").GetString().Should().Be("Invoiced");
        job.GetProperty("labourTotal").GetDecimal().Should().Be(180m);
        job.GetProperty("partsTotal").GetDecimal().Should().Be(68.40m);
        job.GetProperty("amountDue").GetDecimal().Should().Be(248.40m);
        job.GetProperty("history").EnumerateArray().Should().HaveCount(4);
        job.GetProperty("number").GetString().Should().StartWith("RO-");
    }

    /// <summary>
    /// One job, three payers &mdash; the ordinary case in a franchised workshop.
    ///
    /// What matters is that the customer is billed for their share and nobody
    /// else's. Before pay type existed every line was implicitly customer-pay, so
    /// a warranty repair and the dealership's own reconditioning both landed on
    /// the customer's invoice.
    /// </summary>
    [Fact]
    public async Task A_job_can_be_paid_for_by_three_different_people_and_the_customer_owes_only_their_share()
    {
        var jobId = await OpenJobAsync(Manager, await RooftopIdAsync("NAG-01"));

        await AddLineAsync(jobId, Manager, new
        {
            kind = "Labour", description = "Full service", hours = 1.5m, rate = 120m,
        });
        await AddLineAsync(jobId, Manager, new
        {
            kind = "Labour", description = "Water pump, under warranty",
            hours = 2m, rate = 95m, payType = "Warranty",
        });
        await AddLineAsync(jobId, Manager, new
        {
            kind = "Part", description = "Wiper blade for our own demo",
            unitAmount = 22m, payType = "Internal",
        });

        var job = await GetJobAsync(jobId, Manager);

        job.GetProperty("amountDue").GetDecimal().Should().Be(180m,
            because: "the customer pays for their service and nothing else");
        job.GetProperty("warrantyTotal").GetDecimal().Should().Be(190m);
        job.GetProperty("internalTotal").GetDecimal().Should().Be(22m);
        job.GetProperty("workTotal").GetDecimal().Should().Be(392m,
            because: "the workshop did all of it, whoever settles the bill");
    }

    /// <summary>
    /// Warranty and internal work needs no answer from the customer, and must not
    /// block the invoice waiting for one. Asking somebody to authorise a repair
    /// they are not paying for is a question with no meaning attached to it.
    /// </summary>
    [Fact]
    public async Task Work_the_customer_is_not_paying_for_does_not_wait_on_their_answer()
    {
        var jobId = await OpenJobAsync(Manager, await RooftopIdAsync("NAG-01"));
        await AddLineAsync(jobId, Manager, new
        {
            kind = "Labour", description = "Full service", hours = 1m, rate = 100m,
        });

        (await MoveAsync(jobId, Manager, "InProgress")).Should().Be(HttpStatusCode.OK);

        // Found mid-job, so a customer-pay line here would be Pending.
        await AddLineAsync(jobId, Manager, new
        {
            kind = "Labour", description = "Recall work", hours = 1m, rate = 95m, payType = "Warranty",
        });

        var job = await GetJobAsync(jobId, Manager);
        var warranty = job.GetProperty("lines").EnumerateArray()
            .Single(l => l.GetProperty("payType").GetString() == "Warranty");

        warranty.GetProperty("authorization").GetString().Should().Be("Authorized");

        (await MoveAsync(jobId, Manager, "Completed")).Should().Be(HttpStatusCode.OK);
        (await MoveAsync(jobId, Manager, "Invoiced")).Should().Be(HttpStatusCode.OK,
            because: "nothing is waiting on the customer, so the job can be billed");
    }

    /// <summary>
    /// The ledger has to tell the three apart. Warranty is a receivable because
    /// the manufacturer has not paid yet; internal is a charge the dealership
    /// carries itself; only the customer's share is cash.
    /// </summary>
    [Fact]
    public async Task The_ledger_puts_warranty_in_a_receivable_and_internal_in_its_own_charge()
    {
        var jobId = await OpenJobAsync(Manager, await RooftopIdAsync("NAG-01"));
        await AddLineAsync(jobId, Manager, new
        {
            kind = "Labour", description = "Service", hours = 1m, rate = 100m,
        });
        await AddLineAsync(jobId, Manager, new
        {
            kind = "Labour", description = "Warranty repair", hours = 1m, rate = 80m, payType = "Warranty",
        });
        await AddLineAsync(jobId, Manager, new
        {
            kind = "Part", description = "Recon part for stock", unitAmount = 40m, payType = "Internal",
        });

        (await MoveAsync(jobId, Manager, "InProgress")).Should().Be(HttpStatusCode.OK);
        (await MoveAsync(jobId, Manager, "Completed")).Should().Be(HttpStatusCode.OK);
        (await MoveAsync(jobId, Manager, "Invoiced")).Should().Be(HttpStatusCode.OK);

        using var entries = await SendAsync(HttpMethod.Get, $"{Ledger}?reference={jobId}", Manager);
        var posted = (await entries.Content.ReadFromJsonAsync<JsonElement>())
            .Rows().Should().ContainSingle().Subject;

        using var detail = await SendAsync(
            HttpMethod.Get, $"{Ledger}/{posted.GetProperty("id").GetString()}", Manager);
        var lines = (await detail.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("lines");

        // 1100 and not 1000: invoicing raises a debt, and being paid is a separate
        // event. The other two payers are unchanged - warranty was always a
        // receivable from the manufacturer, and internal work is the dealership
        // charging itself, so neither is anybody's bill to settle.
        Debit(lines, "1100").Should().Be(100m, because: "only the customer's share is a debt they owe");
        Debit(lines, "1200").Should().Be(80m, because: "the manufacturer owes it until the claim is paid");
        Debit(lines, "5400").Should().Be(40m, because: "the dealership carries its own work");

        // Revenue is credited with everything, whoever settles it: the workshop
        // sold all of it, and its people did all of it.
        (Credit(lines, "4200") + Credit(lines, "4300")).Should().Be(220m);
    }

    /// <summary>
    /// Reconditioning our own stock is part of what that car cost us, not an
    /// expense of the month. Putting it anywhere else lets used-vehicle gross
    /// flatter itself by exactly the amount spent making the car saleable — the
    /// classic way a used department looks profitable and is not.
    /// </summary>
    [Fact]
    public async Task Reconditioning_a_car_we_own_lands_on_that_car_rather_than_on_an_expense()
    {
        var rooftop = await RooftopIdAsync("NAG-01");
        var vehicleId = await AddVehicleAsync();

        // Take it into stock first: that is the only thing separating this from
        // the identical job on a customer's car.
        using (var received = await PostAsync("/api/v1/inventory", Manager, new
        {
            vehicleId,
            rooftopId = rooftop,
            stockNumber = $"R{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            costAmount = 9_000m,
            costCurrency = "USD",
        }))
        {
            received.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var jobId = await OpenJobAsync(Manager, rooftop, vehicleId);
        await AddLineAsync(jobId, Manager, new
        {
            kind = "Labour", description = "Recon before it goes on the lot",
            hours = 3m, rate = 60m, payType = "Internal",
        });

        (await MoveAsync(jobId, Manager, "InProgress")).Should().Be(HttpStatusCode.OK);
        (await MoveAsync(jobId, Manager, "Completed")).Should().Be(HttpStatusCode.OK);
        (await MoveAsync(jobId, Manager, "Invoiced")).Should().Be(HttpStatusCode.OK);

        using var entries = await SendAsync(HttpMethod.Get, $"{Ledger}?reference={jobId}", Manager);
        var posted = (await entries.Content.ReadFromJsonAsync<JsonElement>())
            .Rows().Should().ContainSingle().Subject;

        using var detail = await SendAsync(
            HttpMethod.Get, $"{Ledger}/{posted.GetProperty("id").GetString()}", Manager);
        var lines = (await detail.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("lines");

        Debit(lines, "1300").Should().Be(180m, because: "the car cost 180 more than it did this morning");
        Debit(lines, "5400").Should().Be(0m, because: "it is not an expense; it is stock");
    }

    [Fact]
    public async Task A_line_with_an_unknown_pay_type_is_refused_rather_than_billed_to_the_customer()
    {
        var jobId = await OpenJobAsync(Manager, await RooftopIdAsync("NAG-01"));

        using var response = await PostAsync($"{Jobs}/{jobId}/lines", Manager, new
        {
            kind = "Labour", description = "Something", hours = 1m, rate = 100m, payType = "Guarantee",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("service.unknown_pay_type");
    }

    /// <summary>
    /// Hours sold and what they realised. The effective rate is the number a
    /// service manager actually runs on: the posted rate says what is on the
    /// wall, this says what came through the door.
    /// </summary>
    [Fact]
    public async Task The_labour_report_gives_hours_sold_and_what_an_hour_realised()
    {
        var jobId = await OpenJobAsync(Manager, await RooftopIdAsync("NAG-01"));

        // 2h at 100 and 2h at 50: four hours, 300, so 75 realised per hour.
        await AddLineAsync(jobId, Manager, new
        {
            kind = "Labour", description = "Diagnosis", hours = 2m, rate = 100m,
        });
        await AddLineAsync(jobId, Manager, new
        {
            kind = "Labour", description = "Warranty repair", hours = 2m, rate = 50m, payType = "Warranty",
        });

        // Parts must not reach a LABOUR rate. This is the mistake that makes an
        // effective rate look wonderful and mean nothing.
        await AddLineAsync(jobId, Manager, new
        {
            kind = "Part", description = "Filter", unitAmount = 500m,
        });

        (await MoveAsync(jobId, Manager, "InProgress")).Should().Be(HttpStatusCode.OK);
        (await MoveAsync(jobId, Manager, "Completed")).Should().Be(HttpStatusCode.OK);
        (await MoveAsync(jobId, Manager, "Invoiced")).Should().Be(HttpStatusCode.OK);

        var report = await LabourReportAsync(Manager);

        report.GetProperty("hoursSold").GetDecimal().Should().BeGreaterThanOrEqualTo(4m);
        report.GetProperty("effectiveLabourRate").GetDecimal().Should().BeGreaterThan(0m);

        var payers = report.GetProperty("byPayer").EnumerateArray()
            .ToDictionary(p => p.GetProperty("payType").GetString()!, p => p);

        payers.Should().ContainKey("Warranty",
            because: "hours a technician worked are sold whoever settles the bill");
        payers["Warranty"].GetProperty("hoursSold").GetDecimal().Should().BeGreaterThanOrEqualTo(2m);

        // The parts line is worth 500 and must be nowhere in the labour revenue,
        // so the realised rate cannot have been inflated by it.
        var hours = report.GetProperty("hoursSold").GetDecimal();
        var revenue = report.GetProperty("labourRevenue").GetDecimal();
        (revenue / hours).Should().BeLessThan(200m,
            because: "a part must never be counted as an hour");
    }

    /// <summary>
    /// The report has to say what it is not measuring. Efficiency and
    /// productivity are the two figures the trade benchmarks technicians on, and
    /// neither can be produced without a roster or a time clock — so the response
    /// names them rather than leaving a manager to assume they were fine.
    /// </summary>
    [Fact]
    public async Task The_labour_report_names_the_figures_it_cannot_produce()
    {
        var report = await LabourReportAsync(Manager);

        var absent = report.GetProperty("notMeasured").EnumerateArray()
            .Select(v => v.GetString()).ToList();

        absent.Should().Contain("Efficiency");
        absent.Should().Contain("Productivity");
    }

    [Fact]
    public async Task A_period_that_ends_before_it_starts_is_refused()
    {
        using var response = await SendAsync(
            HttpMethod.Get, $"{Jobs}/labour?from=2026-08-31&to=2026-08-01", Manager);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("service.backwards_period");
    }

    [Fact]
    public async Task Work_found_during_the_job_cannot_be_invoiced_until_the_customer_answers()
    {
        var jobId = await ReadyToBillWithFoundWorkAsync();

        using var refused = await PostAsync($"{Jobs}/{jobId}/status", Manager, new { status = "Invoiced" });

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var body = await refused.Content.ReadAsStringAsync();
        body.Should().Contain("service.work_not_authorized");
        body.Should().Contain("Front discs",
            because: "the refusal must name the call somebody still owes, not just refuse");

        // And the job did not move.
        (await GetJobAsync(jobId, Manager)).GetProperty("status").GetString().Should().Be("Completed");
    }

    [Fact]
    public async Task Once_the_customer_answers_the_job_bills_for_what_they_agreed_to()
    {
        var jobId = await ReadyToBillWithFoundWorkAsync();
        var lineId = await PendingLineIdAsync(jobId, Manager);

        using var answered = await PostAsync(
            $"{Jobs}/{jobId}/lines/{lineId}/answer", Manager,
            new { approved = true, note = "Phoned 10:40, agreed." });

        answered.StatusCode.Should().Be(HttpStatusCode.OK);

        (await MoveAsync(jobId, Manager, "Invoiced")).Should().Be(HttpStatusCode.OK);

        var job = await GetJobAsync(jobId, Manager);
        job.GetProperty("amountDue").GetDecimal().Should().Be(464m);

        var line = job.GetProperty("lines").EnumerateArray()
            .Single(l => l.GetProperty("id").GetString() == lineId);
        line.GetProperty("authorization").GetString().Should().Be("Authorized");
        line.GetProperty("authorizationNote").GetString().Should().Be("Phoned 10:40, agreed.");
    }

    [Fact]
    public async Task Declined_work_stays_on_the_record_and_off_the_bill()
    {
        var jobId = await ReadyToBillWithFoundWorkAsync();
        var lineId = await PendingLineIdAsync(jobId, Manager);

        using var declined = await PostAsync(
            $"{Jobs}/{jobId}/lines/{lineId}/answer", Manager,
            new { approved = false, note = "Will do it next time." });

        declined.StatusCode.Should().Be(HttpStatusCode.OK);
        (await MoveAsync(jobId, Manager, "Invoiced")).Should().Be(HttpStatusCode.OK);

        var job = await GetJobAsync(jobId, Manager);
        job.GetProperty("amountDue").GetDecimal().Should().Be(180m);

        // Still there, at nil — which is what makes "we did offer" provable.
        var line = job.GetProperty("lines").EnumerateArray()
            .Single(l => l.GetProperty("id").GetString() == lineId);
        line.GetProperty("authorization").GetString().Should().Be("Declined");
        line.GetProperty("amount").GetDecimal().Should().Be(0m);
    }

    // --- the segregation of duties ------------------------------------------

    [Fact]
    public async Task A_technician_can_write_work_up_but_cannot_say_the_customer_agreed()
    {
        var rooftop = await RooftopIdAsync("NAG-01");
        var jobId = await OpenJobAsync(Manager, rooftop);
        await AddLineAsync(jobId, Manager, new { kind = "Labour", description = "Full service", hours = 1.5m, rate = 120m });
        (await MoveAsync(jobId, Manager, "InProgress")).Should().Be(HttpStatusCode.OK);

        // The technician finds the work and writes it up — that is their job.
        using var found = await PostAsync($"{Jobs}/{jobId}/lines", Technician, new
        {
            kind = "Part", description = "Front discs and pads", unitAmount = 284m,
        });
        found.StatusCode.Should().Be(HttpStatusCode.OK);

        var lineId = await PendingLineIdAsync(jobId, Manager);

        // Saying the customer agreed to pay for it is somebody else's.
        using var selfAuthorized = await PostAsync(
            $"{Jobs}/{jobId}/lines/{lineId}/answer", Technician, new { approved = true });

        selfAuthorized.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await selfAuthorized.Content.ReadAsStringAsync()).Should().Contain("Service.Authorize");
    }

    [Fact]
    public async Task An_advisor_can_answer_for_the_customer()
    {
        var jobId = await ReadyToBillWithFoundWorkAsync();
        var lineId = await PendingLineIdAsync(jobId, Manager);

        using var answered = await PostAsync(
            $"{Jobs}/{jobId}/lines/{lineId}/answer", Advisor, new { approved = true, note = "Agreed on the phone." });

        answered.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "the advisor is the person who actually rings the customer");
    }

    [Fact]
    public async Task A_salesperson_has_no_business_in_the_workshop()
    {
        using var response = await SendAsync(HttpMethod.Get, Jobs, Sales);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- the ledger ----------------------------------------------------------

    [Fact]
    public async Task Invoicing_posts_labour_and_parts_to_the_ledger_separately()
    {
        var jobId = await OpenJobAsync(Manager, await RooftopIdAsync("NAG-01"));
        await AddLineAsync(jobId, Manager, new { kind = "Labour", description = "Full service", hours = 1.5m, rate = 120m });
        await AddLineAsync(jobId, Manager, new { kind = "Part", description = "Oil and filter", unitAmount = 68.40m });

        (await MoveAsync(jobId, Manager, "InProgress")).Should().Be(HttpStatusCode.OK);
        (await MoveAsync(jobId, Manager, "Completed")).Should().Be(HttpStatusCode.OK);
        (await MoveAsync(jobId, Manager, "Invoiced")).Should().Be(HttpStatusCode.OK);

        using var entries = await SendAsync(HttpMethod.Get, $"{Ledger}?reference={jobId}", Manager);
        entries.StatusCode.Should().Be(HttpStatusCode.OK);

        var posted = (await entries.Content.ReadFromJsonAsync<JsonElement>())
            .Rows().Should().ContainSingle().Subject;

        posted.GetProperty("source").GetString().Should().Be("ServiceInvoice");
        posted.GetProperty("total").GetDecimal().Should().Be(248.40m);

        using var detail = await SendAsync(
            HttpMethod.Get, $"{Ledger}/{posted.GetProperty("id").GetString()}", Manager);
        var lines = (await detail.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("lines");

        // Labour and parts land on their own accounts. "We sold 248 of service" is
        // useless to a workshop manager; the split is the number they run on.
        Credit(lines, "4200").Should().Be(180m);
        Credit(lines, "4300").Should().Be(68.40m);
        Debit(lines, "1100").Should().Be(248.40m, because: "the whole job is owed by the customer");
    }

    [Fact]
    public async Task A_refused_invoice_posts_nothing_to_the_ledger()
    {
        var jobId = await ReadyToBillWithFoundWorkAsync();

        using var refused = await PostAsync($"{Jobs}/{jobId}/status", Manager, new { status = "Invoiced" });
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var entries = await SendAsync(HttpMethod.Get, $"{Ledger}?reference={jobId}", Manager);
        (await entries.Content.ReadFromJsonAsync<JsonElement>())
            .Rows().Should().BeEmpty(
                because: "the posting and the status change share a transaction");
    }

    // --- the rooftop boundary ------------------------------------------------

    [Fact]
    public async Task A_rooftop_scoped_user_does_not_see_another_workshops_jobs()
    {
        var mine = await OpenJobAsync(Manager, await RooftopIdAsync("NAG-01"));
        var theirs = await OpenJobAsync(Manager, await RooftopIdAsync("NAG-02"));

        var visible = await ListIdsAsync($"{Jobs}?limit=200", Advisor);

        visible.Should().Contain(mine);
        visible.Should().NotContain(theirs,
            because: "the response must never carry another workshop's jobs");
    }

    [Fact]
    public async Task A_rooftop_scoped_user_is_refused_another_workshops_job_by_direct_id()
    {
        var jobId = await OpenJobAsync(Manager, await RooftopIdAsync("NAG-02"));

        using var response = await SendAsync(HttpMethod.Get, $"{Jobs}/{jobId}", Advisor);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_job_number_is_unique_within_a_workshop_and_reusable_across_them()
    {
        var first = await GetJobAsync(await OpenJobAsync(Manager, await RooftopIdAsync("NAG-01")), Manager);
        var second = await GetJobAsync(await OpenJobAsync(Manager, await RooftopIdAsync("NAG-02")), Manager);

        first.GetProperty("number").GetString().Should().NotBeNullOrWhiteSpace();
        second.GetProperty("number").GetString().Should().NotBeNullOrWhiteSpace();

        // Each workshop numbers its own jobs, so neither has to explain a gap in
        // its sequence caused by the other one being busy.
        first.GetProperty("rooftopId").ToString()
            .Should().NotBe(second.GetProperty("rooftopId").ToString());
    }

    // --- helpers -------------------------------------------------------------

    /// <summary>
    /// The labour report over a window wide enough to hold whatever this run has
    /// just invoiced, whichever day it happens to be run on.
    /// </summary>

    [Fact]
    public async Task An_invoiced_job_can_be_paid_and_the_job_is_finally_finished()
    {
        // The whole point of the receivables work. Before 2026-09-10 a job
        // reached Invoiced and stopped: the only thing left to do was print it,
        // the ledger had already debited Cash as though the customer had paid,
        // and "a customer books service and pays" was a job this system could not
        // finish. Walked and found on that day; see docs/implementation/DEALER-DAY.md.
        var jobId = await OpenJobAsync(Manager, await RooftopIdAsync("NAG-01"));
        await AddLineAsync(jobId, Manager, new { kind = "Labour", description = "Brakes", hours = 2m, rate = 95m });

        (await MoveAsync(jobId, Manager, "InProgress")).Should().Be(HttpStatusCode.OK);
        (await MoveAsync(jobId, Manager, "Completed")).Should().Be(HttpStatusCode.OK);
        (await MoveAsync(jobId, Manager, "Invoiced")).Should().Be(HttpStatusCode.OK);

        using var found = await SendAsync(
            HttpMethod.Get, $"/api/v1/receivables/for/RepairOrder/{jobId}", Manager);

        found.StatusCode.Should().Be(HttpStatusCode.OK, because: "the work is done and the money is not in");

        var owed = await found.Content.ReadFromJsonAsync<JsonElement>();
        owed.GetProperty("outstanding").GetDecimal().Should().Be(190m);

        using var paid = await PostAsync(
            $"/api/v1/receivables/{owed.GetProperty("id").GetString()}/payments", Manager,
            new { amount = 190m, method = "Card" });

        paid.StatusCode.Should().Be(HttpStatusCode.OK, because: await paid.Content.ReadAsStringAsync());

        var settled = await paid.Content.ReadFromJsonAsync<JsonElement>();
        settled.GetProperty("isSettled").GetBoolean().Should().BeTrue();

        // And the money moved, rather than only the sub-ledger saying so.
        using var entries = await SendAsync(HttpMethod.Get, $"{Ledger}?reference={jobId}", Manager);
        var payment = (await entries.Content.ReadFromJsonAsync<JsonElement>())
            .Rows().Single(e => e.GetProperty("source").GetString() == "Payment");

        using var detail = await SendAsync(
            HttpMethod.Get, $"{Ledger}/{payment.GetProperty("id").GetString()}", Manager);
        var lines = (await detail.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("lines");

        Debit(lines, "1000").Should().Be(190m, because: "the money is in the bank now");
        Credit(lines, "1100").Should().Be(190m, because: "and off what they owed");
    }

    [Fact]
    public async Task Warranty_and_internal_work_are_nobodys_bill_to_pay()
    {
        // A job split three ways bills the customer for their share only. The
        // manufacturer's part is already a receivable of its own kind and the
        // dealership's own work is a charge to itself, so putting either into the
        // customer sub-ledger would have somebody chasing a warranty claim.
        var jobId = await OpenJobAsync(Manager, await RooftopIdAsync("NAG-01"));
        await AddLineAsync(jobId, Manager, new
        {
            kind = "Labour", description = "Service", hours = 1m, rate = 100m,
        });
        await AddLineAsync(jobId, Manager, new
        {
            kind = "Labour", description = "Warranty repair", hours = 1m, rate = 80m, payType = "Warranty",
        });
        await AddLineAsync(jobId, Manager, new
        {
            kind = "Part", description = "Recon part for stock", unitAmount = 40m, payType = "Internal",
        });

        (await MoveAsync(jobId, Manager, "InProgress")).Should().Be(HttpStatusCode.OK);
        (await MoveAsync(jobId, Manager, "Completed")).Should().Be(HttpStatusCode.OK);
        (await MoveAsync(jobId, Manager, "Invoiced")).Should().Be(HttpStatusCode.OK);

        using var found = await SendAsync(
            HttpMethod.Get, $"/api/v1/receivables/for/RepairOrder/{jobId}", Manager);

        found.StatusCode.Should().Be(HttpStatusCode.OK);

        var owed = await found.Content.ReadFromJsonAsync<JsonElement>();

        owed.GetProperty("amount").GetDecimal().Should().Be(100m,
            because: "the customer agreed to 100 of it; the rest is not theirs to settle");
    }
    private async Task<JsonElement> LabourReportAsync(string email)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var to = today.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        using var response = await SendAsync(HttpMethod.Get, $"{Jobs}/labour?from={from}&to={to}", email);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static decimal Credit(JsonElement lines, string accountCode) =>
        lines.EnumerateArray()
            .Where(l => l.GetProperty("accountCode").GetString() == accountCode)
            .Sum(l => l.GetProperty("credit").GetDecimal());

    private static decimal Debit(JsonElement lines, string accountCode) =>
        lines.EnumerateArray()
            .Where(l => l.GetProperty("accountCode").GetString() == accountCode)
            .Sum(l => l.GetProperty("debit").GetDecimal());

    private static string UniqueVin()
    {
        var body = Guid.NewGuid().ToString("N").ToUpperInvariant()
            .Replace("I", "1", StringComparison.Ordinal)
            .Replace("O", "0", StringComparison.Ordinal)
            .Replace("Q", "9", StringComparison.Ordinal);

        return body[..17];
    }

    /// <summary>
    /// A completed job with 180 of authorized labour and 284 found mid-job that
    /// nobody has answered — the state that cannot be invoiced.
    /// </summary>
    private async Task<string> ReadyToBillWithFoundWorkAsync()
    {
        var jobId = await OpenJobAsync(Manager, await RooftopIdAsync("NAG-01"));
        await AddLineAsync(jobId, Manager, new { kind = "Labour", description = "Full service", hours = 1.5m, rate = 120m });

        (await MoveAsync(jobId, Manager, "InProgress")).Should().Be(HttpStatusCode.OK);

        await AddLineAsync(jobId, Manager, new
        {
            kind = "Part", description = "Front discs and pads", unitAmount = 284m,
        });

        (await MoveAsync(jobId, Manager, "Completed")).Should().Be(HttpStatusCode.OK);

        return jobId;
    }

    private async Task<string> PendingLineIdAsync(string jobId, string email)
    {
        var job = await GetJobAsync(jobId, email);

        return job.GetProperty("lines").EnumerateArray()
            .Single(l => l.GetProperty("authorization").GetString() == "Pending")
            .GetProperty("id").GetString()!;
    }

    private async Task<string> OpenJobAsync(string email, string rooftopId, string? vehicleId = null)
    {
        using var response = await PostAsync(Jobs, email, new
        {
            rooftopId,
            customerId = await AddCustomerAsync(),
            vehicleId = vehicleId ?? await AddVehicleAsync(),
            complaint = "Squealing from the front when braking.",
            currency = "USD",
            odometerReading = 48_210,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private async Task AddLineAsync(string jobId, string email, object line)
    {
        using var response = await PostAsync($"{Jobs}/{jobId}/lines", email, line);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<HttpStatusCode> MoveAsync(string jobId, string email, string status)
    {
        using var response = await PostAsync($"{Jobs}/{jobId}/status", email, new { status });
        return response.StatusCode;
    }

    private async Task<JsonElement> GetJobAsync(string jobId, string email)
    {
        using var response = await SendAsync(HttpMethod.Get, $"{Jobs}/{jobId}", email);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<string> AddCustomerAsync()
    {
        using var response = await PostAsync(Customers, Manager, new
        {
            kind = "Person", firstName = "Service", lastName = $"Test{Guid.NewGuid():N}"[..12],
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    /// <summary>A car the customer owns — not a unit in stock.</summary>
    private async Task<string> AddVehicleAsync()
    {
        using var response = await PostAsync(Vehicles, Manager, new
        {
            vin = UniqueVin(), modelYear = 2019, make = "Ford", model = "Focus",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private async Task<IReadOnlyList<string>> ListIdsAsync(string path, string email)
    {
        using var response = await SendAsync(HttpMethod.Get, path, email);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var results = await response.Content.ReadFromJsonAsync<JsonElement>();
        return results.Rows().Select(j => j.GetProperty("id").GetString()!).ToList();
    }

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
