// AppointmentTests (integration) — proves a car can be booked in before it
// arrives, that arriving produces exactly one job linked to the booking, and that
// one workshop cannot read another's diary.
//
// Use:  runs with the normal test suite; needs a reachable SQL engine.
// Edit: the exit criterion this file exists for is roadmap I5's "Appointment -> RO
//       visibility reconciles to its source". Reconciling means two things and
//       both are tested: the booking names the job it produced, and the job
//       exists with the booking's customer and car on it. A test that only
//       checked the status moved to Arrived would pass with no job at all.

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class AppointmentTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string Diary = "/api/v1/appointments";
    private const string Jobs = "/api/v1/repair-orders";
    private const string Customers = "/api/v1/customers";
    private const string Vehicles = "/api/v1/vehicles";

    private const string Manager = DevelopmentSeeder.DevUsers.OrganizationWideEmail;
    private const string Advisor = DevelopmentSeeder.DevUsers.FirstRooftopOnlyEmail;
    private const string Sales = DevelopmentSeeder.DevUsers.SalespersonEmail;

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task A_car_can_be_booked_in_before_it_arrives()
    {
        var booking = await BookAsync(Manager, await RooftopIdAsync("NAG-01"));

        var view = await GetBookingAsync(booking, Manager);

        view.GetProperty("status").GetString().Should().Be("Scheduled");
        view.GetProperty("isOpen").GetBoolean().Should().BeTrue();
        view.GetProperty("estimatedHours").GetDecimal().Should().Be(2.5m);

        // No job yet. This is the whole difference between a diary and a
        // workshop: the job starts when the car is actually here.
        view.GetProperty("repairOrderId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Arriving_opens_the_job_and_the_booking_names_it()
    {
        var booking = await BookAsync(Manager, await RooftopIdAsync("NAG-01"));

        using var arrived = await PostAsync($"{Diary}/{booking}/arrive", Manager, new
        {
            complaint = "Also a warning light since Tuesday.",
            odometerReading = 61_400,
            currency = "USD",
        });

        arrived.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await arrived.Content.ReadFromJsonAsync<JsonElement>();

        var jobId = result.GetProperty("repairOrder").GetProperty("id").GetString()!;
        var jobNumber = result.GetProperty("repairOrder").GetProperty("number").GetString()!;

        // Half one: the booking points at the job, by id and by the number
        // everybody says out loud.
        var view = result.GetProperty("appointment");
        view.GetProperty("status").GetString().Should().Be("Arrived");
        view.GetProperty("repairOrderId").GetString().Should().Be(jobId);
        view.GetProperty("repairOrderNumber").GetString().Should().Be(jobNumber);

        // Half two: the job is really there, against the same customer and car.
        var job = await GetJobAsync(jobId, Manager);
        job.GetProperty("status").GetString().Should().Be("Booked");
        job.GetProperty("customerId").GetString().Should().Be(view.GetProperty("customerId").GetString());
        job.GetProperty("vehicleId").GetString().Should().Be(view.GetProperty("vehicleId").GetString());
        job.GetProperty("odometerReading").GetInt32().Should().Be(61_400);

        // What the customer said at the counter beats what they said on the
        // phone a fortnight ago.
        job.GetProperty("complaint").GetString().Should().Be("Also a warning light since Tuesday.");
    }

    [Fact]
    public async Task The_booking_reason_carries_over_when_nobody_corrects_it()
    {
        var booking = await BookAsync(Manager, await RooftopIdAsync("NAG-01"));

        using var arrived = await PostAsync($"{Diary}/{booking}/arrive", Manager, new { currency = "USD" });
        arrived.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await arrived.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("repairOrder").GetProperty("complaint").GetString()
            .Should().Be("Service and brake check");
    }

    [Fact]
    public async Task A_car_that_arrives_once_produces_one_job()
    {
        var booking = await BookAsync(Manager, await RooftopIdAsync("NAG-01"));

        using var first = await PostAsync($"{Diary}/{booking}/arrive", Manager, new { currency = "USD" });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var jobId = (await first.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("repairOrder").GetProperty("id").GetString()!;

        using var again = await PostAsync($"{Diary}/{booking}/arrive", Manager, new { currency = "USD" });

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await again.Content.ReadAsStringAsync()).Should().Contain("appointments.already_arrived");

        // And the refusal left nothing behind: the booking still names the first
        // job, and no second one was opened against that car.
        var view = await GetBookingAsync(booking, Manager);
        view.GetProperty("repairOrderId").GetString().Should().Be(jobId);

        var forThatCar = await ListAsync(
            $"{Jobs}?vehicleId={view.GetProperty("vehicleId").GetString()}", Manager);

        forThatCar.EnumerateArray().Should().ContainSingle(
            because: "a second arrival must not leave an orphaned job behind");
    }

    [Fact]
    public async Task A_car_that_never_came_is_recorded_rather_than_deleted()
    {
        var booking = await BookAsync(Manager, await RooftopIdAsync("NAG-01"));

        using var closed = await PostAsync($"{Diary}/{booking}/close", Manager, new
        {
            cancelled = false,
            note = "No answer on either number.",
        });

        closed.StatusCode.Should().Be(HttpStatusCode.OK);

        // Still there, and still says what happened. A diary you can empty
        // cannot tell a service manager who to ring the day before.
        var view = await GetBookingAsync(booking, Manager);
        view.GetProperty("status").GetString().Should().Be("NoShow");
        view.GetProperty("outcome").GetString().Should().Be("No answer on either number.");
        view.GetProperty("isOpen").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task The_diary_reports_what_each_day_is_carrying()
    {
        var rooftop = await RooftopIdAsync("NAG-01");
        var day = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));

        await BookAsync(Manager, rooftop, at: day.ToDateTime(new TimeOnly(9, 0)), hours: 2m);
        await BookAsync(Manager, rooftop, at: day.ToDateTime(new TimeOnly(11, 0)), hours: 3.5m);

        var diary = await ListAsync($"{Diary}?rooftopId={rooftop}&from={Iso(day)}&to={Iso(day)}", Manager);

        var load = diary.GetProperty("load").EnumerateArray().Single();
        load.GetProperty("expected").GetInt32().Should().Be(2);
        load.GetProperty("bookedHours").GetDecimal().Should().Be(5.5m);
    }

    [Fact]
    public async Task An_arrived_car_stops_counting_against_the_day_because_its_hours_are_the_jobs()
    {
        var rooftop = await RooftopIdAsync("NAG-01");
        var day = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(45));

        var arriving = await BookAsync(Manager, rooftop, at: day.ToDateTime(new TimeOnly(9, 0)), hours: 2m);
        await BookAsync(Manager, rooftop, at: day.ToDateTime(new TimeOnly(14, 0)), hours: 4m);

        using var arrived = await PostAsync($"{Diary}/{arriving}/arrive", Manager, new { currency = "USD" });
        arrived.StatusCode.Should().Be(HttpStatusCode.OK);

        var diary = await ListAsync($"{Diary}?rooftopId={rooftop}&from={Iso(day)}&to={Iso(day)}", Manager);

        // Four, not six: counting a car that is already in the workshop would
        // show the shop as twice as committed as it is.
        var load = diary.GetProperty("load").EnumerateArray().Single();
        load.GetProperty("expected").GetInt32().Should().Be(1);
        load.GetProperty("bookedHours").GetDecimal().Should().Be(4m);
    }

    [Fact]
    public async Task A_full_workshop_still_takes_the_booking()
    {
        var rooftop = await RooftopIdAsync("NAG-01");
        var day = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60));

        // Twenty hours into one day. Real shops overbook on purpose, and a diary
        // that refused would be worked around within a week by booking everything
        // as an estimate of zero — which would make the figure useless.
        for (var i = 0; i < 4; i++)
        {
            await BookAsync(Manager, rooftop, at: day.ToDateTime(new TimeOnly(8, 0)), hours: 5m);
        }

        var diary = await ListAsync($"{Diary}?rooftopId={rooftop}&from={Iso(day)}&to={Iso(day)}", Manager);
        diary.GetProperty("load").EnumerateArray().Single()
            .GetProperty("bookedHours").GetDecimal().Should().Be(20m);
    }

    [Fact]
    public async Task One_workshops_diary_is_not_readable_from_another()
    {
        // The advisor covers NAG-01 only. A diary is a list of customers' names,
        // cars and phone-call reasons, so an unscoped read here leaks exactly
        // what the rooftop boundary exists to protect.
        var other = await RooftopIdAsync("NAG-02");

        using var refused = await SendAsync(HttpMethod.Get, $"{Diary}?rooftopId={other}", Advisor);

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("appointments.forbidden");
    }

    [Fact]
    public async Task Booking_a_car_in_needs_the_same_right_as_opening_a_job()
    {
        // Arriving a car IS opening a job, so a weaker booking permission would be
        // a route to the stronger one. Sales holds neither.
        using var refused = await PostAsync(Diary, Sales, new
        {
            rooftopId = await RooftopIdAsync("NAG-01"),
            customerId = await AddCustomerAsync(),
            vehicleId = await AddVehicleAsync(),
            scheduledFor = DateTime.UtcNow.AddDays(3),
            reason = "Service",
        });

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_booking_in_the_past_is_refused()
    {
        using var refused = await PostAsync(Diary, Manager, new
        {
            rooftopId = await RooftopIdAsync("NAG-01"),
            customerId = await AddCustomerAsync(),
            vehicleId = await AddVehicleAsync(),
            scheduledFor = DateTime.UtcNow.AddDays(-1),
            reason = "Service",
        });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("appointments.invalid");
    }

    [Fact]
    public async Task A_backwards_date_range_is_refused_rather_than_returning_nothing()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using var refused = await SendAsync(
            HttpMethod.Get, $"{Diary}?from={Iso(today.AddDays(7))}&to={Iso(today)}", Manager);

        // Silently answering "no bookings" would read as an empty diary, which is
        // the one answer a service manager must never be given by mistake.
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("appointments.backwards_range");
    }

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task<string> BookAsync(
        string email,
        string rooftopId,
        DateTime? at = null,
        decimal hours = 2.5m)
    {
        using var response = await PostAsync(Diary, email, new
        {
            rooftopId,
            customerId = await AddCustomerAsync(),
            vehicleId = await AddVehicleAsync(),
            scheduledFor = at ?? DateTime.UtcNow.AddDays(2),
            reason = "Service and brake check",
            estimatedHours = hours,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private async Task<JsonElement> GetBookingAsync(string appointmentId, string email)
    {
        using var response = await SendAsync(HttpMethod.Get, $"{Diary}/{appointmentId}", email);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> GetJobAsync(string jobId, string email)
    {
        using var response = await SendAsync(HttpMethod.Get, $"{Jobs}/{jobId}", email);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> ListAsync(string path, string email)
    {
        using var response = await SendAsync(HttpMethod.Get, path, email);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<string> AddCustomerAsync()
    {
        using var response = await PostAsync(Customers, Manager, new
        {
            kind = "Person", firstName = "Diary", lastName = $"Test{Guid.NewGuid():N}"[..12],
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private async Task<string> AddVehicleAsync()
    {
        using var response = await PostAsync(Vehicles, Manager, new
        {
            vin = UniqueVin(), modelYear = 2021, make = "Vauxhall", model = "Corsa",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private static string UniqueVin()
    {
        var body = Guid.NewGuid().ToString("N").ToUpperInvariant()
            .Replace("I", "1", StringComparison.Ordinal)
            .Replace("O", "0", StringComparison.Ordinal)
            .Replace("Q", "9", StringComparison.Ordinal);

        return body[..17];
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
