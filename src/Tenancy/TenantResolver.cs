// TenantResolver — reads the host catalog, checks the tenant is Active, and
// decrypts its connection reference. Results are cached.
//
// Use:  through ITenantResolver.
// Edit: a suspended or retired tenant must keep resolving to null. If you add a
//       status, decide explicitly whether it may serve traffic.

using Microsoft.EntityFrameworkCore;
using OpenDealer360.Core;
using OpenDealer360.Tenancy;

namespace OpenDealer360.Tenancy;

/// <summary>
/// Reads the host catalog to resolve a tenant, caching active results. Suspended,
/// retired, or unknown tenants resolve to <c>null</c> — the caller decides the
/// response (doc 04 §5).
/// </summary>
public sealed class TenantResolver(
    HostCatalogDbContext catalog,
    TenantCache cache,
    ISecretProtector secretProtector)
    : ITenantResolver
{
    private readonly HostCatalogDbContext _catalog = catalog;
    private readonly TenantCache _cache = cache;
    private readonly ISecretProtector _secretProtector = secretProtector;

    public async Task<ResolvedTenant?> ResolveByKeyAsync(string tenantKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantKey))
        {
            return null;
        }

        if (_cache.TryGet(tenantKey, out var cached))
        {
            return cached;
        }

        var record = await _catalog.Tenants
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Slug == tenantKey, cancellationToken);

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

        _cache.Set(tenantKey, resolved);
        return resolved;
    }

    public void Invalidate(string tenantKey) => _cache.Remove(tenantKey);
}
