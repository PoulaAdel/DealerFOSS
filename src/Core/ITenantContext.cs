// ITenantContext — which dealer organization the current request belongs to.
//
// Use:  inject it and read Current. It is write-once per request and throws if
//       read before resolution, which catches code running outside middleware.
// Edit: this is only the holder. Resolution lives in Host/Tenancy/TenantMiddleware
//       and src/Tenancy; change those instead.

using OpenDealer360.Core;

namespace OpenDealer360.Core;

/// <summary>
/// The single dealer organization a request is bound to. Resolved once, in
/// middleware, before any endpoint runs (doc 04 §5). No code chooses a tenant
/// connection itself, so a request can never address a second tenant's database.
/// </summary>
public interface ITenantContext
{
    bool IsResolved { get; }

    ResolvedTenant Current { get; }

    void Set(ResolvedTenant tenant);
}

/// <summary>A resolved tenant: its identity, routing key, and connection.</summary>
public sealed record ResolvedTenant(
    DealerOrganizationId Id,
    string Key,
    string ConnectionString,
    int DatabaseVersion);

/// <summary>
/// Scoped, write-once holder of the resolved tenant for the current request.
/// Registered per-scope; a background job builds its own instead of reusing
/// request state (doc 04 §5).
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private ResolvedTenant? _current;

    public bool IsResolved => _current is not null;

    public ResolvedTenant Current => _current
        ?? throw new InvalidOperationException(
            "No tenant is resolved for this request. A tenant-scoped operation ran outside tenant middleware.");

    public void Set(ResolvedTenant tenant)
    {
        if (_current is not null)
        {
            throw new InvalidOperationException("The tenant for this request is already resolved.");
        }

        _current = tenant;
    }
}
