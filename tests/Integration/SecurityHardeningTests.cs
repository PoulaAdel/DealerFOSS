// SecurityHardeningTests — the headers every response carries, and the limit on
// guessing a secret.
//
// Use:  runs with the normal test suite.
// Edit: the headers are asserted on a REFUSED response as well as a successful
//       one. A security header set only on the happy path is not a control, and
//       that is exactly the mistake that is easy to make by registering the
//       middleware too late in the pipeline.
//
//       The credential RATE LIMITER is deliberately not tested here. This suite
//       runs in-process and makes hundreds of sign-ins in seconds down one
//       connection, so HostFixture raises the limit to keep it out of the way.
//       verify-e2e.ps1 proves the limiter instead, against a real host over a
//       real socket — where a real caller actually lives.

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class SecurityHardeningTests(HostFixture fixture)
{
    private readonly HostFixture _fixture = fixture;

    [Theory]
    [InlineData("X-Content-Type-Options", "nosniff")]
    [InlineData("X-Frame-Options", "DENY")]
    [InlineData("Referrer-Policy", "no-referrer")]
    public async Task Every_response_carries_the_headers_a_browser_needs(string header, string expected)
    {
        using var client = _fixture.CreateClient();
        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));

        response.Headers.GetValues(header).Should().ContainSingle().Which.Should().Be(expected);
    }

    [Fact]
    public async Task A_refused_response_carries_them_too()
    {
        // The half that is easy to get wrong: register the middleware after the
        // thing that refuses, and the headers appear only when nothing went
        // wrong.
        using var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri("/api/v1/organization", UriKind.Relative));
        request.Headers.Add("X-Tenant", "northgroup");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
    }

    [Fact]
    public async Task The_page_cannot_be_framed_by_another_site()
    {
        // Clickjacking matters here specifically: the operator console has a
        // Suspend button, and suspending is a real outage.
        using var client = _fixture.CreateClient();
        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));

        var policy = response.Headers.GetValues("Content-Security-Policy").Single();
        policy.Should().Contain("frame-ancestors 'none'");
    }

    [Fact]
    public async Task Inline_script_is_forbidden_but_inline_style_is_not()
    {
        // Printed documents inline their whole stylesheet, because a document has
        // to survive being saved and opened next year. Inline script stays
        // forbidden — that is the half that turns a value into an execution.
        using var client = _fixture.CreateClient();
        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));

        var policy = response.Headers.GetValues("Content-Security-Policy").Single();

        policy.Should().Contain("style-src 'self' 'unsafe-inline'");
        policy.Should().Contain("script-src 'self'");
        policy.Should().NotContain("script-src 'self' 'unsafe-inline'");
    }
}
