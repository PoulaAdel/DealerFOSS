using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OpenDealer360.Tenancy;

/// <summary>
/// Lets <c>dotnet ef</c> build the host catalog context at design time.
/// The connection is only used by <c>database update</c>; <c>migrations add</c>
/// needs the model alone. Override with OPENDEALER360_HOST_CONNECTION.
/// </summary>
public sealed class HostCatalogDesignTimeFactory : IDesignTimeDbContextFactory<HostCatalogDbContext>
{
    public HostCatalogDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("OPENDEALER360_HOST_CONNECTION")
            ?? "Server=localhost,1433;Database=OpenDealer360_Host;User Id=sa;Password=OpenDealer360_dev!;TrustServerCertificate=True;Encrypt=True";

        var options = new DbContextOptionsBuilder<HostCatalogDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsAssembly(typeof(HostCatalogDesignTimeFactory).Assembly.FullName))
            .Options;

        return new HostCatalogDbContext(options);
    }
}
