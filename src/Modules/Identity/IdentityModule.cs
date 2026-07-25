using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenDealer360.Modules.Identity.Contracts;
using OpenDealer360.Modules.Identity.Data;
using OpenDealer360.Core;

namespace OpenDealer360.Modules.Identity;

/// <summary>Self-registration for the Identity slice (doc 03 §3).</summary>
public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddDbContext<IdentityDbContext>((serviceProvider, options) =>
        {
            var tenant = serviceProvider.GetRequiredService<ITenantContext>();
            options.UseSqlServer(
                tenant.Current.ConnectionString,
                sql => sql.MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName));
        });

        services.AddScoped<IAuditSink, SqlAuditSink>();
        services.AddScoped<IAccessDirectory, AccessService>();

        return services;
    }
}
