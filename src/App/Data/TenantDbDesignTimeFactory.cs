// TenantDbDesignTimeFactory — lets "dotnet ef" build the tenant context outside
// the running application.
//
// Use:  tooling only; never referenced by application code. Without it, "dotnet
//       ef" would start the web host, which cannot resolve a tenant and would
//       therefore fail.
// Edit: override the target with OPENDEALER360_TENANT_CONNECTION. Migrations
//       describe the schema; they are applied per tenant at provisioning time.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OpenDealer360.Core;

namespace OpenDealer360.Data;

public sealed class TenantDbDesignTimeFactory : IDesignTimeDbContextFactory<TenantDb>
{
    public TenantDb CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("OPENDEALER360_TENANT_CONNECTION")
            ?? @"Server=(localdb)\MSSQLLocalDB;Database=OpenDealer360_Tenant_design;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

        var options = new DbContextOptionsBuilder<TenantDb>()
            .UseSqlServer(connection)
            .Options;

        return new TenantDb(options, new SystemClock());
    }
}
