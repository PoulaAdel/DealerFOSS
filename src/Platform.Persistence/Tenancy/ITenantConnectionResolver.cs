using OpenDealer360.Platform.Tenancy;

namespace OpenDealer360.Platform.Persistence.Tenancy;

/// <summary>
/// Resolves a routing key (tenant slug) to a <see cref="ResolvedTenant"/> by
/// reading the host catalog, verifying the tenant is active, and unprotecting
/// its connection reference. Results are cached in process (doc 04 §2, ADR-005).
/// </summary>
public interface ITenantConnectionResolver
{
    Task<ResolvedTenant?> ResolveByKeyAsync(string tenantKey, CancellationToken cancellationToken);

    /// <summary>Drops any cached routing for a tenant (e.g. after re-provisioning).</summary>
    void Invalidate(string tenantKey);
}
