using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OpenDealer360.Core;

namespace OpenDealer360.Modules.Identity.Data;

/// <summary>Design-time context builder for <c>dotnet ef</c>.</summary>
public sealed class IdentityDesignTimeFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("OPENDEALER360_TENANT_CONNECTION")
            ?? @"Server=(localdb)\MSSQLLocalDB;Database=OpenDealer360_Tenant_design;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName))
            .Options;

        return new IdentityDbContext(options, new SystemClock());
    }
}
