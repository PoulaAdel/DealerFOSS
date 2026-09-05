// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   TenantScope and UnattendedScope — how work that is not a request reaches
//   one dealer's database, and how much of the product it can see when it does.
//
// Usage:
//   await using var scope = await factory.OpenAsync(
//       JobContext.RequestedBy("northgroup", job.RequestedByUserId, "csv import"), ct);
//   var customers = scope.Services.GetRequiredService<ICustomers>();
//
//   await using var sweep = await factory.OpenUnattendedAsync(
//       UnattendedJob.For("northgroup", "capture expiry"), ct);
//   var db = sweep.Get<TenantDb>();     // ICustomers here would not compile
//
// Coding Instructions:
//   This is the only sanctioned way for a background job to touch tenant
//   data, and the reason it exists is that the alternative is worse. A
//   worker that captured a DbContext, or read the tenant from a field
//   somewhere, would eventually run one dealership's job against another's
//   database — and nothing in the type system would object.
//
//   Naming the tenant is mandatory and explicit. There is no "current" tenant
//   outside a request, and there must never be one.
//
//   NAMING THE REQUESTER IS MANDATORY TOO, and the two scope types are how.
//   TenantScope arrives with both holders already set, so there is no window in
//   which a scope has a tenant and no caller — the window the import worker's
//   claim used to be written in, and the reason a failed import was attributed
//   to the system or to a person depending on how far it got.
//
//   UNATTENDEDSCOPE DELIBERATELY HAS NO IServiceProvider. That absence is the
//   whole mechanism: Get<T>() is constrained to IUnattendedSafe, so a sweep
//   cannot compile a call to a capability that authorizes against a person.
//   Adding a Services property here, or widening the constraint, silently
//   removes the guarantee — an architecture test fails the build if either
//   happens.
//
//   Do not add an OpenAsync overload taking a bare tenant key, and do not make
//   one method serve both kinds of job. Either would be the shortest call, so
//   it would become the usual one, and the guarantee would be gone inside a
//   release.

using Microsoft.Extensions.DependencyInjection;
using DealerFOSS.Core;

namespace DealerFOSS.Tenancy;

/// <summary>Opens a service scope bound to one named dealer organization.</summary>
public interface ITenantScopeFactory
{
    /// <summary>
    /// Work somebody asked for. Null when the tenant is unknown or not active —
    /// the same answer a request would get, so a suspended dealership's queued
    /// work simply does not run.
    /// </summary>
    Task<TenantScope?> OpenAsync(JobContext job, CancellationToken cancellationToken);

    /// <summary>
    /// Work nobody asked for. Deliberately the longer name, and it returns a
    /// scope that can only reach <see cref="IUnattendedSafe"/> services.
    /// </summary>
    Task<UnattendedScope?> OpenUnattendedAsync(
        UnattendedJob job,
        CancellationToken cancellationToken);
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

    public ValueTask DisposeAsync() => Scopes.DisposeAsync(_scope);
}

/// <summary>
/// A service scope for work nobody asked for. It knows its dealership and has
/// no caller, so <see cref="ICurrentUser"/> reports unauthenticated and its
/// writes are attributed to the system.
/// </summary>
/// <remarks>
/// It exposes no <see cref="IServiceProvider"/>, on purpose. <see cref="Get{T}"/>
/// is the only way out and is constrained to <see cref="IUnattendedSafe"/>, so
/// asking for a permission-checked capability is a compile error rather than a
/// throw from somewhere inside it.
/// </remarks>
public sealed class UnattendedScope : IAsyncDisposable
{
    private readonly IServiceScope _scope;

    internal UnattendedScope(IServiceScope scope, ResolvedTenant tenant, UnattendedJob job)
    {
        _scope = scope;
        Tenant = tenant;
        Job = job;
    }

    public ResolvedTenant Tenant { get; }

    /// <summary>What this scope was opened to do, and why nobody is behind it.</summary>
    public UnattendedJob Job { get; }

    /// <summary>
    /// The one way to reach a service, and only one that has declared it does
    /// not authorize against a person.
    /// </summary>
    public T Get<T>()
        where T : class, IUnattendedSafe
        => _scope.ServiceProvider.GetRequiredService<T>();

    public ValueTask DisposeAsync() => Scopes.DisposeAsync(_scope);
}

internal static class Scopes
{
    internal static ValueTask DisposeAsync(IServiceScope scope) =>
        scope is IAsyncDisposable asyncScope
            ? asyncScope.DisposeAsync()
            : new ValueTask(Task.Run(scope.Dispose));
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
            var resolved = await ResolveAsync(scope, job.TenantKey, cancellationToken);
            if (resolved is null)
            {
                scope.Dispose();
                return null;
            }

            scope.ServiceProvider.GetRequiredService<CurrentUser>().Set(job.RequestedByUserId);
            return new TenantScope(scope, resolved, job);
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    public async Task<UnattendedScope?> OpenUnattendedAsync(
        UnattendedJob job,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        var scope = _scopeFactory.CreateScope();

        try
        {
            var resolved = await ResolveAsync(scope, job.TenantKey, cancellationToken);
            if (resolved is null)
            {
                scope.Dispose();
                return null;
            }

            // The caller is left unresolved on purpose. Reading it throws, which
            // is the correct outcome for a sweep that wanders into a permission
            // check — and its writes fall back to "system".
            return new UnattendedScope(scope, resolved, job);
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    private static async Task<ResolvedTenant?> ResolveAsync(
        IServiceScope scope,
        string tenantKey,
        CancellationToken cancellationToken)
    {
        var resolver = scope.ServiceProvider.GetRequiredService<ITenantResolver>();
        var resolved = await resolver.ResolveByKeyAsync(tenantKey, cancellationToken);

        if (resolved is not null)
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(resolved);
        }

        return resolved;
    }
}
