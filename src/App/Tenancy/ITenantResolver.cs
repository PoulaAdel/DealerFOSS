// ITenantResolver — turns a tenant key into a resolved connection.
//
// Use:  called by Host/Tenancy/TenantMiddleware once per request. A null result
//       means unknown or not active; the caller decides the response.
// Edit: keep the "null means refuse" contract. Returning a default tenant here
//       would silently route a request into the wrong dealer's data.

using DealerFOSS.Core;

namespace DealerFOSS.Tenancy;

/// <summary>
/// Resolves a routing key (tenant slug) to a <see cref="ResolvedTenant"/> by
/// reading the host catalog, verifying the tenant is active, and unprotecting
/// its connection reference. Results are cached in process (doc 04 §2, ADR-005).
/// </summary>
public interface ITenantResolver
{
    Task<ResolvedTenant?> ResolveByKeyAsync(string tenantKey, CancellationToken cancellationToken);

    /// <summary>Drops any cached routing for a tenant (e.g. after re-provisioning).</summary>
    void Invalidate(string tenantKey);
}
