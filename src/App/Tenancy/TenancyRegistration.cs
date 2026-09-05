// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ServiceRegistration — wires the host catalog and tenant resolution into DI.
//
// Usage:
//   Services.AddHostCatalog(connectionString) from Program.cs.
//
// Coding Instructions:
//   This is where a production ISecretProtector replaces the development
//   one. Registering it after AddHostCatalog overrides the default.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DealerFOSS.Tenancy;
using DealerFOSS.Core;

namespace DealerFOSS.Tenancy;

/// <summary>Registers the control-plane catalog and tenant resolution services.</summary>
public static class TenancyRegistration
{
    public static IServiceCollection AddHostCatalog(this IServiceCollection services, string hostConnectionString)
    {
        services.AddDbContext<HostDb>(options =>
            options.UseSqlServer(hostConnectionString, sql =>
                sql.MigrationsAssembly(typeof(HostDb).Assembly.FullName)));

        services.AddSingleton<TenantCache>();
        services.AddScoped<ITenantResolver, TenantResolver>();
        // Concrete-first for the same reason as CurrentUser: Set is not on the
        // interface, so only the middleware and TenantScopeFactory can choose a tenant.
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

        // Default dev protector; the Host replaces this outside Development.
        services.AddSingleton<ISecretProtector, DevSecretProtector>();

        return services;
    }
}
