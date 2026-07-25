using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenDealer360.Platform.Persistence.HostCatalog;
using OpenDealer360.Platform.Persistence.Security;
using OpenDealer360.Platform.Persistence.Tenancy;
using OpenDealer360.Platform.Security;
using OpenDealer360.Platform.Tenancy;

namespace OpenDealer360.Platform.Persistence;

/// <summary>Registers the control-plane catalog and tenant resolution services.</summary>
public static class PlatformPersistenceRegistration
{
    public static IServiceCollection AddHostCatalog(this IServiceCollection services, string hostConnectionString)
    {
        services.AddDbContext<HostCatalogDbContext>(options =>
            options.UseSqlServer(hostConnectionString, sql =>
                sql.MigrationsAssembly(typeof(HostCatalogDbContext).Assembly.FullName)));

        services.AddSingleton<TenantCatalogCache>();
        services.AddScoped<ITenantConnectionResolver, TenantConnectionResolver>();
        services.AddScoped<ITenantContext, TenantContext>();

        // Default dev protector; the Host replaces this outside Development.
        services.AddSingleton<ISecretProtector, PassThroughSecretProtector>();

        return services;
    }
}
