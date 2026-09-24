// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   CredentialSurfaceTests — every route where somebody can guess a secret, and
//   whether the limiter is actually on it.
//
//   The limiter itself is proven by verify-e2e.ps1, against a real host over a
//   real socket, because this suite makes hundreds of sign-ins in seconds down
//   one connection and HostFixture raises the allowance to keep it out of the
//   way. That leaves a gap the limiter's own test cannot close: a NEW credential
//   route added next year, with no .RequireRateLimiting on it, would break
//   nothing and be noticed by nobody. Proving one route is limited is not
//   proving the surface is.
//
//   So this reads the application's own route table and asserts the policy is
//   attached, route by route. It is a spelling test rather than a behaviour
//   test, and that is deliberate: the behaviour is already proven elsewhere and
//   what goes wrong in practice is somebody forgetting a line.
//
// Usage:
//   Runs with the normal test suite; needs a reachable SQL engine, because the
//   routes below are only mapped when tenancy is configured.
//
// Coding Instructions:
//   THE LIST IS THE ASSERTION. Adding a route to Credentials below without
//   adding the attribute makes the test fail, which is the point; removing a
//   route from the list to make a failure go away is the one edit that defeats
//   it. If a credential route genuinely should not be limited, say why in a
//   comment beside it rather than deleting the row.
//
//   The second test is the other half and matters more: it walks EVERY mapped
//   route and fails on one that takes a secret by the shape of its path but is
//   missing from the list above. Without it, a new /auth/… route would simply
//   never be considered.

using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using DealerFOSS.App;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class CredentialSurfaceTests(HostFixture fixture)
{
    /// <summary>
    /// Every route where a wrong answer can simply be tried again — a password,
    /// a six-digit code, a one-time enrolment or recovery code, or a passkey
    /// ceremony. Written out rather than derived, so that adding one is a
    /// deliberate act and the reader can see the whole surface at once.
    /// </summary>
    private static readonly string[] CredentialRoutes =
    [
        "/api/v1/auth/login",
        "/api/v1/auth/login/second-factor",
        // Binding an authenticator, and taking one off. Both are persistence
        // rather than access, which is the step worth making expensive.
        "/api/v1/auth/mfa/confirm",
        "/api/v1/auth/mfa/disable",
        "/api/v1/auth/passkeys/sign-in/begin",
        "/api/v1/auth/passkeys/sign-in/finish",
        "/api/v1/auth/enrol",
        "/api/v1/auth/recover",
        "/api/v1/auth/recover/authenticator",
        "/api/v1/auth/recover/code",
        // The control plane's own sign-in and enrolment. Same reasoning, and a
        // separate store — an administrator's password is guessable exactly like
        // anybody else's.
        "/api/v1/admin/login",
        "/api/v1/admin/mfa/confirm",
    ];

    public static TheoryData<string> Credentials() => [.. CredentialRoutes];

    private readonly HostFixture _fixture = fixture;

    [Theory]
    [MemberData(nameof(Credentials))]
    public void A_route_that_takes_a_secret_is_rate_limited(string route)
    {
        Policy(route).Should().Be(RateLimits.Credentials,
            because: $"{route} is a place where a wrong answer can be tried again, so volume has to cost");
    }

    /// <summary>
    /// The half that catches what nobody thought of. Any route under the two
    /// authentication prefixes is a credential route by default; if it really is
    /// not one, it belongs in the exemption list below with its reason, not
    /// silently outside the check.
    /// </summary>
    [Fact]
    public void No_authentication_route_escapes_the_list_by_being_new()
    {
        // Named exemptions, each for a reason that is about the route rather
        // than about convenience.
        string[] notCredentials =
        [
            // Ending a session. There is no secret to guess — the caller either
            // holds the cookie or does not, and limiting it would mean a person
            // who cannot sign out.
            "/api/v1/auth/logout",
            "/api/v1/admin/logout",
            // Reading who you already are, and what the enrolment screen needs
            // to draw a QR code. Both require a session that already exists.
            "/api/v1/auth/me",
            "/api/v1/admin/me",
            "/api/v1/auth/mfa/enrol",
            "/api/v1/admin/mfa/enrol",
            // Passkey REGISTRATION, as opposed to sign-in. The caller is already
            // signed in, so there is nothing here to guess your way into.
            "/api/v1/auth/passkeys/register/begin",
            "/api/v1/auth/passkeys/register/finish",
            "/api/v1/auth/passkeys",
            // Forgetting a passkey you already own. It names the credential by
            // id and needs the session that holds it, so there is nothing to
            // guess — and a person locked out of removing a lost key is worse.
            "/api/v1/auth/passkeys/{passkeyId:guid}",
            // The operator console. Every one of these is a consequential act —
            // creating a dealership, suspending one, opening support access into
            // somebody's data — and none of them is a SECRET. They are reached
            // only with an administrator session that has already cleared a
            // password and a second factor, both of which are limited above, so
            // the guessing has already been paid for by the time a caller is
            // here. What protects these instead is that they are deliberate,
            // clamped and written into the dealership's own audit trail.
            "/api/v1/admin/tenants",
            "/api/v1/admin/tenants/{slug}/status",
            "/api/v1/admin/support-access",
            "/api/v1/admin/support-access/{id:guid}/end",
        ];

        var unconsidered = AuthenticationRoutes()
            .Where(route => !CredentialRoutes.Contains(route, StringComparer.Ordinal))
            .Where(route => !notCredentials.Contains(route, StringComparer.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

        unconsidered.Should().BeEmpty(
            because: "a new route under /auth or /admin is a credential route until somebody says "
                + "otherwise in writing — add it to CredentialRoutes, or to the exemptions with its "
                + "reason. Unconsidered: " + string.Join(", ", unconsidered));
    }

    /// <summary>
    /// And the counterpart: the limiter must NOT have crept onto the business
    /// API, where the volume that matters is a busy dealership working rather
    /// than somebody guessing.
    /// </summary>
    [Fact]
    public void The_limiter_is_not_on_the_ordinary_business_api()
    {
        var limited = MappedRoutes()
            .Where(route => route.Policy == RateLimits.Credentials)
            .Select(route => route.Path)
            .Where(path => !path.StartsWith("/api/v1/auth", StringComparison.Ordinal)
                && !path.StartsWith("/api/v1/admin", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

        limited.Should().BeEmpty(
            because: "throttling stock lists or job sheets punishes a dealership for being busy");
    }

    /// <summary>
    /// A last check that the policy name in the metadata is the one actually
    /// registered. A typo would attach a policy that does not exist, and the two
    /// tests above would still pass while nothing was limited at all.
    /// </summary>
    [Fact]
    public async Task The_policy_the_routes_name_is_a_policy_that_exists()
    {
        // An unregistered policy name makes ASP.NET throw when the endpoint is
        // reached, so a route that answers at all has a real policy behind it.
        // Sent with no body on purpose: the answer only has to not be a 500.
        using var client = _fixture.CreateClient();
        using var response = await client.PostAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative), content: null);

        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
            because: $"'{RateLimits.Credentials}' must be a policy the limiter was configured with");
    }

    // --- reading the application's own route table --------------------------

    private IEnumerable<string> AuthenticationRoutes() =>
        MappedRoutes()
            .Select(route => route.Path)
            .Where(path => path.StartsWith("/api/v1/auth", StringComparison.Ordinal)
                || path.StartsWith("/api/v1/admin", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal);

    private string? Policy(string path) =>
        MappedRoutes()
            .Where(route => string.Equals(route.Path, path, StringComparison.Ordinal))
            .Select(route => route.Policy)
            .FirstOrDefault();

    /// <summary>
    /// Every route the application mapped, with the rate-limiting policy
    /// attached to it, straight out of the running host rather than out of a
    /// list a test keeps its own copy of.
    /// </summary>
    private IEnumerable<(string Path, string? Policy)> MappedRoutes() =>
        _fixture.Services.GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => (
                Path: "/" + endpoint.RoutePattern.RawText?.TrimStart('/'),
                Policy: endpoint.Metadata
                    .GetMetadata<EnableRateLimitingAttribute>()?.PolicyName));
}
