// TrustedProxies — believing `X-Forwarded-For`, but only from a proxy the
// operator has named.
//
// Use:  configure `Network:TrustedProxies` with the addresses or CIDR ranges of
//       the reverse proxies in front of this installation:
//
//         "Network": { "TrustedProxies": [ "10.0.0.5", "10.1.0.0/16" ] }
//
// Edit: read why this is opt-in, because the obvious "just call
//       UseForwardedHeaders()" is worse than doing nothing.
//
//       THE PROBLEM. The credential rate limiter partitions callers by
//       `RemoteIpAddress`. Behind nginx or a hosted load balancer that address
//       is the PROXY's for every request, so the whole dealership shares one
//       bucket and twenty sign-in attempts from anywhere — including from one
//       attacker — locks everybody out. That is precisely the shared-bucket
//       failure the partitioning exists to prevent.
//
//       THE TRAP. The fix is to read the real client from `X-Forwarded-For`.
//       But that header is just a header: anyone can send one. Trusting it
//       unconditionally turns the limiter into a formality, because an attacker
//       writes a fresh address on every request and never shares a bucket with
//       themselves. A spoofable partition key is WORSE than a shared one — the
//       shared one at least still limits somebody.
//
//       SO: the header is honoured only when the request actually arrived from
//       an address the operator listed. With nothing configured, nothing is
//       trusted and the behaviour is exactly what it was before this file
//       existed. Silence means "no proxy", which is the safe reading.
//
//       The default KnownNetworks/KnownProxies (loopback) are CLEARED first.
//       Leaving them in would trust anything arriving over localhost, which on
//       a container host is a much larger set of things than it sounds.
//
//       `X-Forwarded-Proto` rides along deliberately. Behind a proxy that
//       terminates TLS the request reaches Kestrel as plain HTTP, so
//       `Request.IsHttps` is false and the HSTS header in
//       SecurityHeadersMiddleware would never be sent — the header would be
//       configured, believed to be working, and absent.

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Both namespaces define IPNetwork. `KnownIPNetworks` — the property that
// replaced the obsolete `KnownNetworks` — takes the System.Net one, so that is
// the one this file means everywhere.
using IPNetwork = System.Net.IPNetwork;

namespace DealerFOSS.Tenancy;

public static class TrustedProxies
{
    /// <summary>Where the operator lists their reverse proxies.</summary>
    public const string SectionName = "Network:TrustedProxies";

    /// <summary>
    /// Registers forwarded-header handling if — and only if — at least one
    /// trusted proxy is configured. Returns what was configured so startup can
    /// say so in the log rather than leaving an operator guessing.
    /// </summary>
    public static IReadOnlyList<string> AddTrustedProxies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var configured = configuration
            .GetSection(SectionName)
            .Get<string[]>() ?? [];

        var entries = configured
            .Select(entry => entry?.Trim() ?? string.Empty)
            .Where(entry => entry.Length > 0)
            .ToArray();

        if (entries.Length == 0)
        {
            return [];
        }

        // Parsed HERE rather than inside the options callback, which runs lazily
        // on first use. An operator who typo'd an address must find out when the
        // service refuses to start, not months later on discovering their rate
        // limiter has been partitioning on the proxy's own address all along.
        var networks = new List<IPNetwork>();
        var proxies = new List<IPAddress>();
        var rejected = new List<string>();

        foreach (var entry in entries)
        {
            if (entry.Contains('/', StringComparison.Ordinal))
            {
                // A CIDR range: "10.1.0.0/16".
                if (IPNetwork.TryParse(entry, out var network))
                {
                    networks.Add(network);
                }
                else
                {
                    rejected.Add(entry);
                }

                continue;
            }

            if (IPAddress.TryParse(entry, out var address))
            {
                proxies.Add(address);
            }
            else
            {
                rejected.Add(entry);
            }
        }

        if (rejected.Count > 0)
        {
            throw new InvalidOperationException(
                $"{SectionName} contains {rejected.Count} entry/entries that are neither an IP address "
                + $"nor a CIDR range: {string.Join(", ", rejected)}. Fix or remove them — a proxy list "
                + "that silently dropped what it could not parse would leave the credential rate limiter "
                + "partitioning on the proxy's own address without saying so.");
        }

        // One hop by default, which is the usual single reverse proxy. A chain
        // needs this raised deliberately: each extra hop is one more address the
        // operator is choosing to believe.
        var forwardLimit = configuration.GetValue("Network:ForwardLimit", 1);

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // Cleared, not appended to. See the note at the top.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();

            foreach (var network in networks)
            {
                options.KnownIPNetworks.Add(network);
            }

            foreach (var proxy in proxies)
            {
                options.KnownProxies.Add(proxy);
            }

            options.ForwardLimit = forwardLimit;
        });

        return entries;
    }
}

/// <summary>
/// Source-generated so the message is not formatted when logging is off. The
/// analyzer insists (CA1848) and it is right: this runs once at startup, but
/// the pattern is the one every other log line in the application should follow.
/// </summary>
internal static partial class TrustedProxyLog
{
    // The list is passed whole rather than pre-joined: `string.Join` at the call
    // site would run even with logging switched off, which is exactly what
    // CA1873 objects to. Serilog renders the collection itself, and does so only
    // if the message is actually going somewhere.
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Trusting forwarded headers from {Count} configured proxy source(s): {Proxies}. "
            + "The credential rate limiter will partition on the real client address.")]
    public static partial void Trusting(ILogger logger, int count, IReadOnlyList<string> proxies);
}
