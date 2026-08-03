// ControlPlaneDesignTimeFactory — lets "dotnet ef" build the control-plane
// context outside the running application.
//
// Use:  tooling only; never referenced by application code.
// Edit: override the target with OPENDEALER360_HOST_CONNECTION — the control
//       plane lives in the host catalog, so it is the same connection HostDb
//       uses, not a tenant's.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OpenDealer360.Identity;

/// <summary>Design-time context builder for <c>dotnet ef</c>.</summary>
internal sealed class ControlPlaneDesignTimeFactory : IDesignTimeDbContextFactory<ControlPlaneDb>
{
    public ControlPlaneDb CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("OPENDEALER360_HOST_CONNECTION")
            ?? @"Server=(localdb)\MSSQLLocalDB;Database=OpenDealer360_Host;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

        var options = new DbContextOptionsBuilder<ControlPlaneDb>()
            .UseSqlServer(connection, sql => sql.MigrationsAssembly(typeof(ControlPlaneDb).Assembly.FullName))
            .Options;

        return new ControlPlaneDb(options);
    }
}
