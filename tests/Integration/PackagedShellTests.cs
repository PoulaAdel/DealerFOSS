// PackagedShellTests (integration) — the application serving its own frontend,
// which is what makes an installation one thing to install rather than two.
//
// Use:  runs with the normal test suite; needs a reachable SQL engine.
// Edit: this boots its OWN host with a web root, because the shared fixture has
//       none — a checkout has no built frontend and must still start. The files
//       here are a stand-in for a Vite build: an index.html and one fingerprinted
//       asset, which is all the behaviour under test depends on.
//
//       The valuable test is "a mistyped API route is not the shell". Rehearsed
//       2026-08-07 against the real published package: with the /api fallback
//       removed, a signed-in caller asking for /api/v1/organisation gets the
//       application shell and HTTP 200 — because the shell fallback matches any
//       path without a dot in it, and every mistyped API route is one. A client
//       then parses HTML looking for JSON, and nothing reports an error.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class PackagedShellTests : IDisposable
{
    private const string Tenant = "northgroup";
    private const string Manager = DevelopmentSeeder.DevUsers.OrganizationWideEmail;

    private const string ShellMarker = "<div id=\"root\"></div>";

    private readonly string _webRoot;
    private readonly WebApplicationFactory<Program> _factory;

    public PackagedShellTests()
    {
        // A published wwwroot, in miniature. Deleted with the fixture.
        _webRoot = Path.Combine(Path.GetTempPath(), $"dealerfoss-shell-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));

        File.WriteAllText(
            Path.Combine(_webRoot, "index.html"),
            $"<!doctype html><html><head><title>DealerFOSS</title></head><body>{ShellMarker}</body></html>");

        File.WriteAllText(Path.Combine(_webRoot, "assets", "index-abc123.js"), "export const built = true;");

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(Environments.Development);
                builder.UseSetting("RateLimiting:CredentialAttemptsPerMinute", "1000000");
                builder.UseWebRoot(_webRoot);
            });
    }

    public void Dispose()
    {
        _factory.Dispose();

        try
        {
            Directory.Delete(_webRoot, recursive: true);
        }
        catch (IOException)
        {
            // A temp folder left behind costs disk, not correctness.
        }
    }

    [Fact]
    public async Task The_root_serves_the_application_and_not_a_description_of_it()
    {
        // An operator opening http://their-server:8080 for the first time must see
        // the product. The JSON identity document that lives at "/" when no
        // frontend is published would read as a broken install.
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain(ShellMarker);
    }

    [Theory]
    [InlineData("/dashboard")]
    [InlineData("/accounting/periods")]
    public async Task A_frontend_route_reloaded_in_the_browser_is_handed_the_shell(string path)
    {
        // These are not server routes. The browser asks for them on a refresh and
        // on a pasted link, and a 404 there is the classic single-page-app
        // packaging failure.
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain(ShellMarker);
    }

    [Fact]
    public async Task A_mistyped_api_route_is_a_404_and_never_the_shell()
    {
        // The one that costs an afternoon: a 200 carrying HTML, to a caller
        // expecting JSON, with nothing anywhere reporting an error.
        var session = await _fixtureSessionAsync();

        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri("/api/v1/organisation", UriKind.Relative));
        request.Headers.Add("X-Tenant", Tenant);
        request.Headers.Add("Cookie", $"dfoss_session={session}");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(ShellMarker);
    }

    [Fact]
    public async Task A_missing_asset_is_a_404_and_never_the_shell()
    {
        // A stale index.html asking for an asset that no longer exists must fail
        // as a missing script, not as a page that returns HTML where JavaScript
        // was expected.
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/assets/gone.js", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_shell_is_never_cached_and_fingerprinted_assets_are_cached_forever()
    {
        // index.html is the file that names the current fingerprints. A cached
        // copy points a browser at assets that no longer exist, which presents as
        // a white page after an upgrade and is fixed by a hard refresh nobody
        // knows to perform.
        using var client = _factory.CreateClient();

        using var shell = await client.GetAsync(new Uri("/", UriKind.Relative));
        shell.Headers.CacheControl!.NoStore.Should().BeTrue();

        using var asset = await client.GetAsync(new Uri("/assets/index-abc123.js", UriKind.Relative));
        asset.Headers.CacheControl!.MaxAge.Should().Be(TimeSpan.FromDays(365));
    }

    [Fact]
    public async Task A_static_file_carries_the_same_security_headers_as_everything_else()
    {
        // The shell is the page every screen runs inside. Serving it without the
        // framing and sniffing protections would exempt the one response that
        // matters most.
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/", UriKind.Relative));

        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        response.Headers.GetValues("Content-Security-Policy").Single()
            .Should().Contain("frame-ancestors 'none'");
    }

    /// <summary>
    /// A session against this host. Signs in directly rather than through
    /// HostFixture, whose sessions belong to a different host instance.
    /// </summary>
    private async Task<string> _fixtureSessionAsync()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri("/api/v1/auth/login", UriKind.Relative))
        {
            Content = JsonContent.Create(
                new { email = Manager, password = DevelopmentSeeder.DevUsers.Password }),
        };
        request.Headers.Add("X-Tenant", Tenant);

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var cookie = response.Headers.GetValues("Set-Cookie")
            .Single(v => v.StartsWith("dfoss_session=", StringComparison.Ordinal));

        return Uri.UnescapeDataString(cookie.Split(';')[0]["dfoss_session=".Length..]);
    }
}
