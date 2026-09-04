// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   TrustedProxyTests — the credential rate limiter only believes a forwarded
//   address when the operator said which proxy may send one.
//
// Usage:
//   Dotnet test
//
// Coding Instructions:
//   The property worth guarding is the DEFAULT. With nothing configured
//   nothing must be trusted, because an unconditionally honoured
//   X-Forwarded-For lets an attacker write a fresh address on every request
//   and never share a rate-limit bucket with themselves — which is worse
//   than the shared-bucket problem it would be fixing.

using DealerFOSS.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DealerFOSS.UnitTests;

public sealed class TrustedProxyTests
{
    [Fact]
    public void With_nothing_configured_no_proxy_is_trusted()
    {
        var (configured, _) = Configure([]);

        // Empty means UseForwardedHeaders is never added to the pipeline at all,
        // so RemoteIpAddress stays the direct peer.
        Assert.Empty(configured);
    }

    [Fact]
    public void A_configured_address_is_trusted()
    {
        var (configured, options) = Configure(["10.0.0.5"]);

        Assert.Single(configured);
        Assert.Contains(options.KnownProxies, address => address.ToString() == "10.0.0.5");
    }

    [Fact]
    public void A_configured_range_is_trusted()
    {
        var (configured, options) = Configure(["10.1.0.0/16"]);

        Assert.Single(configured);
        Assert.Single(options.KnownIPNetworks);
        Assert.Equal(16, options.KnownIPNetworks[0].PrefixLength);
    }

    [Fact]
    public void The_loopback_default_is_cleared_rather_than_added_to()
    {
        // ASP.NET Core trusts loopback out of the box. On a container host
        // "loopback" covers rather more than it sounds like it does, so the
        // operator's list is the whole list and not an addition to it.
        var (_, options) = Configure(["10.0.0.5"]);

        Assert.Empty(options.KnownIPNetworks);
        Assert.Single(options.KnownProxies);
        Assert.Equal("10.0.0.5", options.KnownProxies[0].ToString());
    }

    [Fact]
    public void Both_the_caller_and_the_scheme_are_taken_from_the_proxy()
    {
        // The scheme matters as much as the address: behind a proxy that
        // terminates TLS the request arrives as plain HTTP, so Request.IsHttps
        // is false and the HSTS header would be silently omitted.
        var (_, options) = Configure(["10.0.0.5"]);

        Assert.True(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedFor));
        Assert.True(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedProto));
    }

    [Fact]
    public void One_hop_is_trusted_unless_more_are_asked_for()
    {
        var (_, single) = Configure(["10.0.0.5"]);
        Assert.Equal(1, single.ForwardLimit);

        var (_, chained) = Configure(["10.0.0.5"], forwardLimit: "2");
        Assert.Equal(2, chained.ForwardLimit);
    }

    [Fact]
    public void A_typo_stops_the_service_starting_rather_than_being_dropped()
    {
        // Silently ignoring an unparseable entry would leave the limiter
        // partitioning on the proxy's own address while the operator believed
        // they had fixed it — the failure would be invisible until an attack.
        var failure = Assert.Throws<InvalidOperationException>(
            () => Configure(["10.0.0.5", "not-an-address"]));

        Assert.Contains("not-an-address", failure.Message, StringComparison.Ordinal);
    }

    private static (IReadOnlyList<string> Configured, ForwardedHeadersOptions Options) Configure(
        string[] proxies,
        string? forwardLimit = null)
    {
        var settings = new Dictionary<string, string?>();
        for (var i = 0; i < proxies.Length; i++)
        {
            settings[$"Network:TrustedProxies:{i}"] = proxies[i];
        }

        if (forwardLimit is not null)
        {
            settings["Network:ForwardLimit"] = forwardLimit;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddOptions();
        var configured = services.AddTrustedProxies(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        return (configured, options);
    }
}
