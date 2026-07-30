// ServiceRegistration — wires the host catalog and tenant resolution into DI.
//
// Use:  services.AddHostCatalog(connectionString) from Program.cs.
// Edit: this is where a production ISecretProtector replaces the development
//       one. Registering it after AddHostCatalog overrides the default.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenDealer360.Tenancy;
using OpenDealer360.Core;

namespace OpenDealer360.Tenancy;

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
        services.AddScoped<ITenantContext, TenantContext>();

        // Default dev protector; the Host replaces this outside Development.
        services.AddSingleton<ISecretProtector, DevSecretProtector>();

        return services;
    }
}
