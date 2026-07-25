using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using OpenDealer360.Host.Development;

namespace OpenDealer360.IntegrationTests;

/// <summary>
/// The tenant-isolation proof, running in CI. Two dealer organizations live in
/// separate databases; a request resolves exactly one of them; an unresolvable
/// tenant is refused at the edge (doc 04 §5, doc 06 §5).
/// </summary>
[Collection(nameof(HostCollection))]
public sealed class TenantIsolationTests(HostFixture fixture)
{
    private const string OrganizationEndpoint = "/api/v1/organization";

    private readonly HostFixture _fixture = fixture;

    [Fact]
    public async Task Multi_rooftop_organization_resolves_all_of_its_rooftops()
    {
        var organization = await GetOrganizationAsync("northgroup");

        organization.Name.Should().Be("North Auto Group");
        organization.RooftopCodes.Should().BeEquivalentTo(["NAG-01", "NAG-02"]);
    }

    [Fact]
    public async Task Single_rooftop_organization_resolves_only_its_own_rooftop()
    {
        var organization = await GetOrganizationAsync("citymotors");

        organization.Name.Should().Be("City Motors");
        organization.RooftopCodes.Should().BeEquivalentTo(["CM-01"]);
    }

    [Fact]
    public async Task Two_tenants_never_see_each_others_data()
    {
        var north = await GetOrganizationAsync("northgroup");
        var city = await GetOrganizationAsync("citymotors");

        // Separate databases: no identifier, name, or rooftop may appear in both.
        north.Id.Should().NotBe(city.Id);
        north.Name.Should().NotBe(city.Name);
        north.RooftopCodes.Should().NotIntersectWith(city.RooftopCodes);
    }

    [Fact]
    public async Task Request_without_a_tenant_header_is_refused()
    {
        using var client = _fixture.CreateClient();

        using var response = await client.GetAsync(new Uri(OrganizationEndpoint, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: "a tenant-scoped endpoint must never run without a resolved tenant");
    }

    [Fact]
    public async Task Request_for_an_unknown_tenant_is_refused()
    {
        using var response = await SendAsync("no-such-dealer");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<OrganizationSnapshot> GetOrganizationAsync(string tenantKey)
    {
        using var response = await SendAsync(tenantKey);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return OrganizationSnapshot.From(payload);
    }

    private async Task<HttpResponseMessage> SendAsync(string tenantKey)
    {
        using var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(OrganizationEndpoint, UriKind.Relative));
        request.Headers.Add("X-Tenant", tenantKey);
        // Organization-wide so these tests observe the tenant boundary rather
        // than a rooftop-scope filter; rooftop scope is covered separately by
        // RooftopAuthorizationTests.
        request.Headers.Add("X-User", DevelopmentSeeder.DevUsers.OrganizationWide.ToString());
        return await client.SendAsync(request);
    }

    /// <summary>Flattens the organization response to what these tests assert on.</summary>
    private sealed record OrganizationSnapshot(string Id, string Name, IReadOnlyList<string> RooftopCodes)
    {
        public static OrganizationSnapshot From(JsonElement root)
        {
            var codes = root.GetProperty("legalEntities")
                .EnumerateArray()
                .SelectMany(entity => entity.GetProperty("rooftops").EnumerateArray())
                .Select(rooftop => rooftop.GetProperty("code").GetString()!)
                .ToList();

            return new OrganizationSnapshot(
                root.GetProperty("id").ToString(),
                root.GetProperty("name").GetString()!,
                codes);
        }
    }
}
