// VehiclesDesignTimeFactory — lets "dotnet ef" build this context outside the
// running application.
//
// Use:  tooling only; never referenced by application code.
// Edit: override the target with OPENDEALER360_TENANT_CONNECTION. Migrations
//       describe the schema; they are applied per tenant at provisioning time.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OpenDealer360.Core;

namespace OpenDealer360.Vehicles.Data;

public sealed class VehiclesDesignTimeFactory : IDesignTimeDbContextFactory<VehiclesDbContext>
{
    public VehiclesDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("OPENDEALER360_TENANT_CONNECTION")
            ?? @"Server=(localdb)\MSSQLLocalDB;Database=OpenDealer360_Tenant_design;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

        var options = new DbContextOptionsBuilder<VehiclesDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsAssembly(typeof(VehiclesDbContext).Assembly.FullName))
            .Options;

        return new VehiclesDbContext(options, new SystemClock());
    }
}
