// VehiclesModule — registers this slice with the application.
//
// Use:  services.AddVehiclesModule() and app.MapVehiclesModule() from Program.cs.
// Edit: the DbContext binds to the tenant resolved for the current request, so
//       it can only be constructed after tenant middleware has run.

using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenDealer360.Core;
using OpenDealer360.Vehicles.Contracts;
using OpenDealer360.Vehicles.Data;

namespace OpenDealer360.Vehicles;

public static class VehiclesModule
{
    public static IServiceCollection AddVehiclesModule(this IServiceCollection services)
    {
        services.AddDbContext<VehiclesDbContext>((serviceProvider, options) =>
        {
            var tenant = serviceProvider.GetRequiredService<ITenantContext>();
            options.UseSqlServer(
                tenant.Current.ConnectionString,
                sql => sql.MigrationsAssembly(typeof(VehiclesDbContext).Assembly.FullName));
        });

        services.AddScoped<IVehicleDirectory, VehicleService>();
        services.AddScoped<IInventoryDirectory, InventoryService>();

        return services;
    }

    public static void MapVehiclesModule(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapVehicles();
}
