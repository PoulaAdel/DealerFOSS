// CustomersModule — registers this slice with the application.
//
// Use:  services.AddCustomersModule() and app.MapCustomers() from Program.cs.
// Edit: the DbContext binds to the tenant resolved for the current request, so
//       it can only be constructed after tenant middleware has run.

using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenDealer360.Core;
using OpenDealer360.Customers.Contracts;
using OpenDealer360.Customers.Data;

namespace OpenDealer360.Customers;

public static class CustomersModule
{
    public static IServiceCollection AddCustomersModule(this IServiceCollection services)
    {
        services.AddDbContext<CustomersDbContext>((serviceProvider, options) =>
        {
            var tenant = serviceProvider.GetRequiredService<ITenantContext>();
            options.UseSqlServer(
                tenant.Current.ConnectionString,
                sql => sql.MigrationsAssembly(typeof(CustomersDbContext).Assembly.FullName));
        });

        services.AddScoped<ICustomerDirectory, CustomerService>();

        return services;
    }

    public static void MapCustomersModule(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapCustomers();
}
