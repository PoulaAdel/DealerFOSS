// TenantResolver — reads the host catalog, checks the tenant is Active, and
// decrypts its connection reference. Results are cached.
//
// Use:  through ITenantResolver.
// Edit: a suspended or retired tenant must keep resolving to null. If you add a
//       status, decide explicitly whether it may serve traffic.
//
//       The key is normalized ONCE, here, and the normalized form is used for
//       both the cache and the query. It used to be passed through raw, and the
//       two layers agreed only by coincidence: TenantCache compares
//       case-insensitively, while `t.Slug == tenantKey` inherits whatever
//       collation the SQL Server was installed with. On a case-sensitive
//       collation "NORTHGROUP" would miss in the database but HIT a warm cache —
//       so the same request would succeed or 404 depending on cache state, which
//       is the worst kind of bug to be handed. Slugs are stored lowercase
//       (TenantProvisioning refuses anything else), so lowercasing the key makes
//       both layers agree on purpose rather than by luck.

using Microsoft.EntityFrameworkCore;
using DealerFOSS.Core;
using DealerFOSS.Tenancy;

namespace DealerFOSS.Tenancy;

/// <summary>
/// Reads the host catalog to resolve a tenant, caching active results. Suspended,
/// retired, or unknown tenants resolve to <c>null</c> — the caller decides the
/// response (doc 04 §5).
/// </summary>
public sealed class TenantResolver(
    HostDb catalog,
    TenantCache cache,
    ISecretProtector secretProtector)
    : ITenantResolver
{
    private readonly HostDb _catalog = catalog;
    private readonly TenantCache _cache = cache;
    private readonly ISecretProtector _secretProtector = secretProtector;

    public async Task<ResolvedTenant?> ResolveByKeyAsync(string tenantKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantKey))
        {
            return null;
        }

        var key = Normalize(tenantKey);

        if (_cache.TryGet(key, out var cached))
        {
            return cached;
        }

        var record = await _catalog.Tenants
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Slug == key, cancellationToken);

        if (record is null || record.Status != TenantStatus.Active)
        {
            return null;
        }

        var connectionString = _secretProtector.Unprotect(record.ProtectedConnectionString);
        var resolved = new ResolvedTenant(
            new DealerOrganizationId(record.Id),
            record.Slug,
            connectionString,
            record.DatabaseVersion);

        _cache.Set(key, resolved);
        return resolved;
    }

    /// <summary>
    /// Normalized the same way as the lookup. Invalidating with a raw key while
    /// the entry was cached under a normalized one would leave a suspended
    /// dealership serving traffic for the rest of the cache's lifetime.
    /// </summary>
    public void Invalidate(string tenantKey) =>
        _cache.Remove(string.IsNullOrWhiteSpace(tenantKey) ? string.Empty : Normalize(tenantKey));

    private static string Normalize(string tenantKey) => tenantKey.Trim().ToLowerInvariant();
}
