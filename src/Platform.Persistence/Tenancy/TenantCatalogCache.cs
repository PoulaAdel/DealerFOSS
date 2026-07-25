using System.Collections.Concurrent;
using OpenDealer360.Platform.Kernel;
using OpenDealer360.Platform.Tenancy;

namespace OpenDealer360.Platform.Persistence.Tenancy;

/// <summary>
/// In-process routing cache shared across requests (registered as a singleton).
/// A single-node deployment needs no external cache for this (ADR-005). Entries
/// expire so a re-provisioned or suspended tenant is picked up without a restart.
/// </summary>
public sealed class TenantCatalogCache(IClock clock)
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly IClock _clock = clock;

    public static TimeSpan Ttl { get; } = TimeSpan.FromMinutes(10);

    public bool TryGet(string key, out ResolvedTenant tenant)
    {
        if (_entries.TryGetValue(key, out var entry) && entry.ExpiresAt > _clock.UtcNow)
        {
            tenant = entry.Tenant;
            return true;
        }

        tenant = null!;
        return false;
    }

    public void Set(string key, ResolvedTenant tenant) =>
        _entries[key] = new Entry(tenant, _clock.UtcNow.Add(Ttl));

    public void Remove(string key) => _entries.TryRemove(key, out _);

    private readonly record struct Entry(ResolvedTenant Tenant, DateTimeOffset ExpiresAt);
}
