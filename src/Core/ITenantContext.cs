// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Which dealer organization the current request belongs to. A holder and
//   nothing more — it carries the answer, it does not work it out.
//
//   It is write-once per request and THROWS when read before resolution, rather
//   than returning null or a default. That is deliberate: code running outside
//   the middleware — a background job, a test harness, a startup path — is
//   exactly the code that must not quietly get "some tenant". A loud failure at
//   the first read is the cheapest possible way to find it.
//
// Usage:
//   Inject ITenantContext, read Current.
//
// Coding Instructions:
//   Resolution belongs to src/App/Tenancy — the middleware and the resolver.
//   Change those, not this.
//
//   Do not add a "try get" or a nullable accessor. Every caller that wanted one
//   so far turned out to be code that should have named its tenant explicitly,
//   which is what ITenantScopeFactory is for.
//
//   The interface is read-only. Set lives on the concrete TenantContext, which
//   only TenantMiddleware and TenantScopeFactory resolve — the same rule, and
//   the same reason, as ICurrentUser. Being able to ASK which dealership you are
//   in is ordinary; being able to DECIDE is not.

using DealerFOSS.Core;

namespace DealerFOSS.Core;

/// <summary>
/// The single dealer organization a request is bound to. Resolved once, in
/// middleware, before any endpoint runs (doc 04 §5). No code chooses a tenant
/// connection itself, so a request can never address a second tenant's database.
/// </summary>
public interface ITenantContext
{
    bool IsResolved { get; }

    ResolvedTenant Current { get; }
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
/// <remarks>
/// <see cref="Set"/> is deliberately not on <see cref="ITenantContext"/>. Only
/// <c>TenantMiddleware</c> and <c>TenantScopeFactory</c> resolve this concrete
/// type; everything else is injected the read-only interface and can ask which
/// dealership it is in without being able to choose one.
/// </remarks>
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
