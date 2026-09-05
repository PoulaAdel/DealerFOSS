// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   TenantScope — how work that is not a request reaches one dealer's database.
//
// Usage:
//   await using var scope = await factory.OpenAsync(
//       JobContext.RequestedBy("northgroup", job.RequestedByUserId, "csv import"), ct);
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
//
//   NAMING THE REQUESTER IS MANDATORY TOO, and that is what JobContext is for.
//   The factory establishes BOTH holders before it hands the scope back, so
//   there is no window in which a scope exists with a tenant and no caller —
//   which is the window the import worker's claim used to be written in, and
//   the reason a failed import could be attributed to the system or to a person
//   depending on how far it got. A scope arrives fully identified or not at all.
//
//   Do not add an overload taking a bare tenant key. It would be the shortest
//   call, so it would become the usual one, and the guarantee would be gone
//   inside a release.

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
    /// <param name="job">
    /// Which dealership, and on whose behalf. There is no overload without it.
    /// </param>
    Task<TenantScope?> OpenAsync(JobContext job, CancellationToken cancellationToken);
}

/// <summary>
/// A service scope whose <see cref="ITenantContext"/> and <see cref="ICurrentUser"/>
/// are already set, so anything resolved from it — <c>TenantDb</c> included —
/// addresses that dealership and is attributed to that caller.
/// </summary>
public sealed class TenantScope : IAsyncDisposable
{
    private readonly IServiceScope _scope;

    internal TenantScope(IServiceScope scope, ResolvedTenant tenant, JobContext job)
    {
        _scope = scope;
        Tenant = tenant;
        Job = job;
    }

    public IServiceProvider Services => _scope.ServiceProvider;

    public ResolvedTenant Tenant { get; }

    /// <summary>What this scope was opened to do, and for whom.</summary>
    public JobContext Job { get; }

    public ValueTask DisposeAsync() =>
        _scope is IAsyncDisposable asyncScope
            ? asyncScope.DisposeAsync()
            : new ValueTask(Task.Run(_scope.Dispose));
}

internal sealed class TenantScopeFactory(IServiceScopeFactory scopeFactory) : ITenantScopeFactory
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    public async Task<TenantScope?> OpenAsync(JobContext job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        var scope = _scopeFactory.CreateScope();

        try
        {
            var resolver = scope.ServiceProvider.GetRequiredService<ITenantResolver>();
            var resolved = await resolver.ResolveByKeyAsync(job.TenantKey, cancellationToken);

            if (resolved is null)
            {
                scope.Dispose();
                return null;
            }

            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(resolved);

            // Unattended work leaves the caller unresolved on purpose. Reading it
            // then throws, which is the correct outcome for a sweep that wanders
            // into a permission check — and its writes fall back to "system".
            if (job.RequestedByUserId is Guid requester)
            {
                scope.ServiceProvider.GetRequiredService<CurrentUser>().Set(requester);
            }

            return new TenantScope(scope, resolved, job);
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }
}
