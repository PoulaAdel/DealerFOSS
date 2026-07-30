// CustomerTests (integration) — proves adding and finding a customer works
// against a real database, and that permissions are enforced per action.
//
// Use:  runs with the normal test suite; needs a reachable SQL engine.
// Edit: the advisor account holds Customers.Read but not Customers.Create, which
//       is what makes "can look up, cannot add" a real assertion rather than a
//       theory. Keep that asymmetry in the seeder.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using OpenDealer360.App;

namespace OpenDealer360.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class CustomerTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";
    private const string Endpoint = "/api/v1/customers";

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task A_manager_can_add_a_person_and_read_them_back()
    {
        var surname = UniqueSurname();

        using var created = await AddAsync(DevelopmentSeeder.DevUsers.OrganizationWideEmail, new
        {
            kind = "Person",
            firstName = "Marisol",
            lastName = surname,
            email = "Marisol.Alvarez@Example.TEST",
            phone = "(555) 010-2030",
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var detail = await created.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("displayName").GetString().Should().Be($"Marisol {surname}");

        // Stored normalized, so it can be found however it was typed.
        var contacts = detail.GetProperty("contactPoints").EnumerateArray()
            .Select(c => c.GetProperty("value").GetString())
            .ToList();
        contacts.Should().Contain("marisol.alvarez@example.test");
        contacts.Should().Contain("5550102030");
    }

    [Fact]
    public async Task A_created_customer_can_be_fetched_by_id()
    {
        var surname = UniqueSurname();
        var id = await AddPersonAsync(surname);

        using var response = await SendAsync(
            HttpMethod.Get, $"{Endpoint}/{id}", DevelopmentSeeder.DevUsers.OrganizationWideEmail);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("lastName").GetString().Should().Be(surname);
    }

    [Fact]
    public async Task An_unknown_customer_id_is_not_found()
    {
        using var response = await SendAsync(
            HttpMethod.Get, $"{Endpoint}/{Guid.NewGuid()}", DevelopmentSeeder.DevUsers.OrganizationWideEmail);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_customer_can_be_found_by_surname()
    {
        var surname = UniqueSurname();
        await AddPersonAsync(surname);

        var names = await SearchAsync(surname, DevelopmentSeeder.DevUsers.OrganizationWideEmail);

        names.Should().ContainSingle().Which.Should().Contain(surname);
    }

    [Fact]
    public async Task A_customer_can_be_found_by_phone_however_it_is_typed()
    {
        var surname = UniqueSurname();
        await AddPersonAsync(surname, phone: "(555) 987-6543");

        // Typed with punctuation, stored as digits — the search must bridge that.
        var names = await SearchAsync("555-987-6543", DevelopmentSeeder.DevUsers.OrganizationWideEmail);

        names.Should().Contain(n => n.Contains(surname, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_customer_can_be_found_by_email_in_any_case()
    {
        var surname = UniqueSurname();
        var email = $"{surname.ToLowerInvariant()}@example.test";
        await AddPersonAsync(surname, email: email);

        var names = await SearchAsync(email.ToUpperInvariant(), DevelopmentSeeder.DevUsers.OrganizationWideEmail);

        names.Should().Contain(n => n.Contains(surname, StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_advisor_can_look_customers_up_but_cannot_add_one()
    {
        // Read is granted, create is not — the two permissions are checked
        // separately, not lumped into one "customers" right.
        using var search = await SendAsync(
            HttpMethod.Get, Endpoint, DevelopmentSeeder.DevUsers.FirstRooftopOnlyEmail);
        search.StatusCode.Should().Be(HttpStatusCode.OK);

        using var add = await AddAsync(DevelopmentSeeder.DevUsers.FirstRooftopOnlyEmail, new
        {
            kind = "Person",
            firstName = "Should",
            lastName = "NotBeCreated",
        });

        add.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_user_with_no_assignment_cannot_reach_customers_at_all()
    {
        using var response = await SendAsync(
            HttpMethod.Get, Endpoint, DevelopmentSeeder.DevUsers.UnassignedEmail);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Customers_are_shared_across_rooftops_not_hidden_by_scope()
    {
        // A rooftop-scoped advisor still sees organization customers: the same
        // person buys at one location and services at another (doc 04 §1).
        var surname = UniqueSurname();
        await AddPersonAsync(surname);

        var names = await SearchAsync(surname, DevelopmentSeeder.DevUsers.FirstRooftopOnlyEmail);

        names.Should().Contain(n => n.Contains(surname, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Customers_do_not_leak_between_dealer_organizations()
    {
        var surname = UniqueSurname();
        await AddPersonAsync(surname);

        // Same search, other organization, other database.
        using var client = _fixture.CreateClient();
        var token = await _fixture.TokenForAsync(DevelopmentSeeder.DevUsers.OrganizationWideEmail, "citymotors");
        using var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri($"{Endpoint}?search={surname}", UriKind.Relative));
        request.Headers.Add("X-Tenant", "citymotors");
        request.Headers.Add("Cookie", $"odms_session={token}");

        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var found = await response.Content.ReadFromJsonAsync<JsonElement>();
        found.EnumerateArray().Should().BeEmpty(
            because: "a customer belongs to one dealer organization's database");
    }

    [Fact]
    public async Task A_customer_without_a_name_is_rejected_with_a_readable_reason()
    {
        using var response = await AddAsync(DevelopmentSeeder.DevUsers.OrganizationWideEmail, new
        {
            kind = "Person",
            firstName = "Nameless",
            lastName = "  ",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("last name", because: "the message should say what to fix");
    }

    [Fact]
    public async Task An_unrecognised_customer_kind_is_rejected()
    {
        using var response = await AddAsync(DevelopmentSeeder.DevUsers.OrganizationWideEmail, new
        {
            kind = "Robot",
            lastName = "Unit",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- helpers -----------------------------------------------------------

    /// <summary>Surnames unique per run, so tests do not collide on shared data.</summary>
    private static string UniqueSurname() => $"Test{Guid.NewGuid():N}"[..12];

    private async Task<string> AddPersonAsync(
        string surname, string? email = null, string? phone = null)
    {
        using var response = await AddAsync(DevelopmentSeeder.DevUsers.OrganizationWideEmail, new
        {
            kind = "Person",
            firstName = "Test",
            lastName = surname,
            email,
            phone,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        return detail.GetProperty("id").GetString()!;
    }

    private async Task<IReadOnlyList<string>> SearchAsync(string term, string email)
    {
        using var response = await SendAsync(
            HttpMethod.Get, $"{Endpoint}?search={Uri.EscapeDataString(term)}", email);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var results = await response.Content.ReadFromJsonAsync<JsonElement>();
        return results.EnumerateArray()
            .Select(c => c.GetProperty("displayName").GetString()!)
            .ToList();
    }

    private async Task<HttpResponseMessage> AddAsync(string email, object body)
    {
        using var client = _fixture.CreateClient();
        var token = await _fixture.TokenForAsync(email, Tenant);

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Endpoint, UriKind.Relative))
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("X-Tenant", Tenant);
        request.Headers.Add("Cookie", $"odms_session={token}");

        return await client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string email)
    {
        using var client = _fixture.CreateClient();
        var token = await _fixture.TokenForAsync(email, Tenant);

        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Headers.Add("X-Tenant", Tenant);
        request.Headers.Add("Cookie", $"odms_session={token}");

        return await client.SendAsync(request);
    }
}
