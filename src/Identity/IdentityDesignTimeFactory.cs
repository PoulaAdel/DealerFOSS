// IdentityDesignTimeFactory — lets "dotnet ef" build this context outside the
// running application.
//
// Use:  tooling only; never referenced by application code.
// Edit: override the target with OPENDEALER360_TENANT_CONNECTION. Migrations
//       describe the schema; they are applied per tenant at provisioning time.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OpenDealer360.Core;

namespace OpenDealer360.Identity;

/// <summary>Design-time context builder for <c>dotnet ef</c>.</summary>
internal sealed class IdentityDesignTimeFactory : IDesignTimeDbContextFactory<IdentityDb>
{
    public IdentityDb CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("OPENDEALER360_TENANT_CONNECTION")
            ?? @"Server=(localdb)\MSSQLLocalDB;Database=OpenDealer360_Tenant_design;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

        var options = new DbContextOptionsBuilder<IdentityDb>()
            .UseSqlServer(connection, sql => sql.MigrationsAssembly(typeof(IdentityDb).Assembly.FullName))
            .Options;

        return new IdentityDb(options, new SystemClock());
    }
}
