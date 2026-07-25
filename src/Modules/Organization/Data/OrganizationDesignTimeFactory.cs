using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OpenDealer360.Platform.Kernel;

namespace OpenDealer360.Modules.Organization.Data;

/// <summary>
/// Design-time context builder for <c>dotnet ef</c>. The connection points at a
/// representative tenant database template; migrations describe the schema and
/// are applied per tenant at provisioning time.
/// </summary>
public sealed class OrganizationDesignTimeFactory : IDesignTimeDbContextFactory<OrganizationDbContext>
{
    public OrganizationDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("OPENDEALER360_TENANT_CONNECTION")
            ?? "Server=localhost,1433;Database=OpenDealer360_Tenant_design;User Id=sa;Password=OpenDealer360_dev!;TrustServerCertificate=True;Encrypt=True";

        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsAssembly(typeof(OrganizationDbContext).Assembly.FullName))
            .Options;

        return new OrganizationDbContext(options, new SystemClock());
    }
}
