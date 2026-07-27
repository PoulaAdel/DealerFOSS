// IdentityModule — registers this slice with the application.
//
// Use:  services.AddIdentityModule() from Program.cs.
// Edit: add a registration here when you add a service to the module. The
//       DbContext binds to the tenant resolved for the current request, so it
//       can only be constructed after tenant middleware has run.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenDealer360.Identity.Contracts;
using OpenDealer360.Identity.Data;
using OpenDealer360.Core;

namespace OpenDealer360.Identity;

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
