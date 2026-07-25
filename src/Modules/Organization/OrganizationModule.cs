using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Routing;
using OpenDealer360.Organization.Contracts;
using OpenDealer360.Organization.Data;
using OpenDealer360.Core;

namespace OpenDealer360.Organization;

/// <summary>
/// Self-registration for the Organization slice (doc 03 §3). The DbContext binds
/// to the tenant database resolved for the current request; it is created only
/// after tenant middleware has run.
/// </summary>
public static class OrganizationModule
{
    public static IServiceCollection AddOrganizationModule(this IServiceCollection services)
    {
        services.AddDbContext<OrganizationDbContext>((serviceProvider, options) =>
        {
            var tenant = serviceProvider.GetRequiredService<ITenantContext>();
            options.UseSqlServer(
                tenant.Current.ConnectionString,
                sql =>
                {
                    sql.MigrationsAssembly(typeof(OrganizationDbContext).Assembly.FullName);
                    // Multi-collection reads (org → entities → rooftops → departments)
                    // are split into one query per collection to avoid a cartesian join.
                    sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                });
        });

        services.AddScoped<IOrganizationDirectory, OrganizationService>();

        return services;
    }

    public static void MapOrganizationModule(this IEndpointRouteBuilder endpoints) =>
        OrganizationEndpoints.Map(endpoints);
}
