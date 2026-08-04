// LeadTests (integration) — proves an enquiry can be captured and worked, and
// that one rooftop cannot see or touch another rooftop's enquiries.
//
// Use:  runs with the normal test suite; needs a reachable SQL engine.
// Edit: kept deliberately small. The rooftop-scope cases are the ones that must
//       fail if the check in LeadService is removed — everything else here is
//       just enough to show the workflow reaching the database.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class LeadTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string Leads = "/api/v1/leads";
    private const string Customers = "/api/v1/customers";

    private const string Manager = DevelopmentSeeder.DevUsers.OrganizationWideEmail;
    private const string Advisor = DevelopmentSeeder.DevUsers.FirstRooftopOnlyEmail;
    private const string Nobody = DevelopmentSeeder.DevUsers.UnassignedEmail;

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task An_enquiry_can_be_captured_and_read_back_with_the_customer_name()
    {
        var surname = Unique();
        var customerId = await AddCustomerAsync(surname);

        using var created = await PostAsync(Leads, Manager, new
        {
            rooftopId = await RooftopIdAsync("NAG-01"),
            customerId,
            source = "WalkIn",
            enquiry = "Wants something around 25k.",
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var detail = await created.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("status").GetString().Should().Be("New");
        detail.GetProperty("isOpen").GetBoolean().Should().BeTrue();
        detail.GetProperty("history").EnumerateArray().Should().ContainSingle();

        // The name is resolved through the Customers contract, not copied onto
        // the lead, so it cannot go stale.
        detail.GetProperty("customerName").GetString().Should().Contain(surname);
    }

    [Fact]
    public async Task An_enquiry_for_a_customer_who_is_not_on_file_is_refused()
    {
        using var response = await PostAsync(Leads, Manager, new
        {
            rooftopId = await RooftopIdAsync("NAG-01"),
            customerId = Guid.NewGuid(),
            source = "Phone",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().Contain("customer");
    }

    [Fact]
    public async Task A_lead_moves_through_its_life_cycle_and_keeps_the_history()
    {
        var leadId = await CaptureAsync(Manager, await RooftopIdAsync("NAG-01"));

        using var working = await PostAsync($"{Leads}/{leadId}/status", Manager,
            new { status = "Working", note = "Left a voicemail." });
        working.StatusCode.Should().Be(HttpStatusCode.OK);

        using var appointment = await PostAsync($"{Leads}/{leadId}/status", Manager,
            new { status = "Appointment", note = "Saturday at 10." });
        appointment.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await appointment.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("status").GetString().Should().Be("Appointment");
        detail.GetProperty("history").EnumerateArray().Should().HaveCount(3);
    }

    [Fact]
    public async Task A_move_the_life_cycle_does_not_allow_is_refused_with_a_reason()
    {
        var leadId = await CaptureAsync(Manager, await RooftopIdAsync("NAG-01"));

        using var response = await PostAsync($"{Leads}/{leadId}/status", Manager, new { status = "Won" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Working");
    }

    [Fact]
    public async Task A_lead_can_be_handed_to_a_salesperson()
    {
        var leadId = await CaptureAsync(Manager, await RooftopIdAsync("NAG-01"));
        var salesperson = DevelopmentSeeder.DevUsers.FirstRooftopOnly;

        using var response = await PostAsync($"{Leads}/{leadId}/assign", Manager,
            new { assignedToUserId = salesperson });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("assignedToUserId").GetString().Should().Be(salesperson.ToString());
    }

    // --- the rooftop boundary ----------------------------------------------

    [Fact]
    public async Task A_rooftop_scoped_user_does_not_see_another_rooftops_enquiries()
    {
        var mine = await CaptureAsync(Manager, await RooftopIdAsync("NAG-01"));
        var theirs = await CaptureAsync(Manager, await RooftopIdAsync("NAG-02"));

        var visible = await ListIdsAsync($"{Leads}?limit=200", Advisor);

        visible.Should().Contain(mine);
        visible.Should().NotContain(theirs,
            because: "the response must never carry another rooftop's enquiries");
    }

    [Fact]
    public async Task A_rooftop_scoped_user_is_refused_another_rooftops_lead_by_direct_id()
    {
        var leadId = await CaptureAsync(Manager, await RooftopIdAsync("NAG-02"));

        using var response = await SendAsync(HttpMethod.Get, $"{Leads}/{leadId}", Advisor);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_rooftop_scoped_user_is_refused_a_list_filtered_to_another_rooftop()
    {
        var sibling = await RooftopIdAsync("NAG-02");

        using var response = await SendAsync(HttpMethod.Get, $"{Leads}?rooftopId={sibling}", Advisor);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_advisor_can_read_enquiries_but_cannot_work_them()
    {
        var leadId = await CaptureAsync(Manager, await RooftopIdAsync("NAG-01"));

        using var read = await SendAsync(HttpMethod.Get, $"{Leads}/{leadId}", Advisor);
        read.StatusCode.Should().Be(HttpStatusCode.OK);

        using var move = await PostAsync($"{Leads}/{leadId}/status", Advisor, new { status = "Working" });

        move.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "Leads.Read and Leads.Manage are checked separately");
    }

    [Fact]
    public async Task A_user_with_no_assignment_cannot_reach_enquiries_at_all()
    {
        using var response = await SendAsync(HttpMethod.Get, Leads, Nobody);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "no assignment must mean no access, never unfiltered access");
    }

    // --- helpers -----------------------------------------------------------

    private static string Unique() => $"Test{Guid.NewGuid():N}"[..12];

    private async Task<string> AddCustomerAsync(string surname)
    {
        using var response = await PostAsync(Customers, Manager, new
        {
            kind = "Person",
            firstName = "Lead",
            lastName = surname,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        return detail.GetProperty("id").GetString()!;
    }

    private async Task<string> CaptureAsync(string email, string rooftopId)
    {
        var customerId = await AddCustomerAsync(Unique());

        using var response = await PostAsync(Leads, email, new
        {
            rooftopId,
            customerId,
            source = "Website",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        return detail.GetProperty("id").GetString()!;
    }

    private async Task<IReadOnlyList<string>> ListIdsAsync(string path, string email)
    {
        using var response = await SendAsync(HttpMethod.Get, path, email);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var results = await response.Content.ReadFromJsonAsync<JsonElement>();
        return results.EnumerateArray().Select(l => l.GetProperty("id").GetString()!).ToList();
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
