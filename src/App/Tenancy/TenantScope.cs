// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   TenantScope — how work that is not a request reaches one dealer's database.
//
// Usage:
//   await using var scope = await factory.OpenAsync("northgroup", ct);
//   var db = scope.Services.GetRequiredService<TenantDb>();
//
// Coding Instructions:
//   This is the only sanctioned way for a background job to touch tenant
//   data, and the reason it exists is that the alternative is worse. A
//   worker that captured a DbContext, or read the tenant from a field
//   somewhere, would eventually run one dealership's job against another's
//   database — and nothing in the type system would object.
//
//   Naming the tenant is therefore mandatory and explicit. There is no
//   "current" tenant outside a request, and there must never be one.

using Microsoft.Extensions.DependencyInjection;
using DealerFOSS.Core;

namespace DealerFOSS.Tenancy;

/// <summary>Opens a service scope bound to one named dealer organization.</summary>
public interface ITenantScopeFactory
{
    /// <summary>
    /// Null when the tenant is unknown or not active — the same answer a request
    /// would get, so a suspended dealership's queued work simply does not run.
    /// </summary>
    Task<TenantScope?> OpenAsync(string tenantKey, CancellationToken cancellationToken);
}

/// <summary>
/// A service scope whose <see cref="ITenantContext"/> is already set, so anything
/// resolved from it — <c>TenantDb</c> included — addresses that dealership.
/// </summary>
public sealed class TenantScope : IAsyncDisposable
{
    private readonly IServiceScope _scope;

    internal TenantScope(IServiceScope scope, ResolvedTenant tenant)
    {
        _scope = scope;
        Tenant = tenant;
    }

    public IServiceProvider Services => _scope.ServiceProvider;

    public ResolvedTenant Tenant { get; }

    public ValueTask DisposeAsync() =>
        _scope is IAsyncDisposable asyncScope
            ? asyncScope.DisposeAsync()
            : new ValueTask(Task.Run(_scope.Dispose));
}

internal sealed class TenantScopeFactory(IServiceScopeFactory scopeFactory) : ITenantScopeFactory
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    public async Task<TenantScope?> OpenAsync(string tenantKey, CancellationToken cancellationToken)
    {
        var scope = _scopeFactory.CreateScope();

        try
        {
            var resolver = scope.ServiceProvider.GetRequiredService<ITenantResolver>();
            var resolved = await resolver.ResolveByKeyAsync(tenantKey, cancellationToken);

            if (resolved is null)
            {
                scope.Dispose();
                return null;
            }

            scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(resolved);
            return new TenantScope(scope, resolved);
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }
}
