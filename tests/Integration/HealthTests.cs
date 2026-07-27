// HealthTests — liveness and readiness are separate signals and must stay so
// (doc 07 §6).
//
// Use:  runs with the normal test suite.
// Edit: health must never require a tenant or a user; load balancers and probes
//       call it without either. Readiness gains a check whenever a new
//       must-be-available dependency is introduced.

using System.Net;
using FluentAssertions;

namespace OpenDealer360.IntegrationTests;

/// <summary>
/// Liveness and readiness are separate signals (doc 07 §6): liveness answers
/// "is the process up", readiness answers "are its dependencies usable".
/// </summary>
[Collection(nameof(HostCollection))]
public sealed class HealthTests(HostFixture fixture)
{
    private readonly HostFixture _fixture = fixture;

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_endpoints_report_healthy(string path)
    {
        using var client = _fixture.CreateClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task Health_endpoints_do_not_require_a_tenant()
    {
        // Health must stay reachable for load balancers and probes, which never
        // carry a tenant header.
        using var client = _fixture.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
