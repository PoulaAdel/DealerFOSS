// HostFixture — boots the real application against a real database and seeds the
// sample dealer organizations, once, for the whole integration suite.
//
// Use:  take it as a constructor parameter on a class marked
//       [Collection(nameof(HostCollection))].
// Edit: configuration must arrive as environment variables. Program.cs reads
//       configuration while its top-level statements run, before
//       WebApplicationFactory can add an in-memory source. If SQL is
//       unreachable this fails loudly on purpose — a skipped isolation test is
//       not evidence of isolation.

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;

namespace OpenDealer360.IntegrationTests;

/// <summary>
/// Boots the real Host against a real SQL database and seeds the two sample
/// dealer organizations, so tests exercise the same path as
/// <c>deploy/verify-e2e.ps1</c> — but in CI.
/// </summary>
/// <remarks>
/// The connection comes from <c>OPENDEALER360_TEST_SQL</c> when set (CI supplies
/// a SQL Server service container) and falls back to LocalDB for local runs. If
/// no engine is reachable the fixture fails loudly rather than skipping: a test
/// that never reaches the database is not evidence of isolation.
/// </remarks>
public sealed class HostFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string LocalDbFallback =
        @"Server=(localdb)\MSSQLLocalDB;Database=OpenDealer360_Host;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("OPENDEALER360_TEST_SQL") ?? LocalDbFallback;

    static HostFixture()
    {
        // Program.cs reads configuration while its top-level statements run —
        // before WebApplicationFactory can add an in-memory source — so these
        // must arrive as environment variables, which the default builder reads
        // at construction. Setting them in a static constructor guarantees they
        // exist before any host is created.
        Environment.SetEnvironmentVariable("ConnectionStrings__HostCatalog", ConnectionString);
        Environment.SetEnvironmentVariable("Seed__Enabled", "true");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development is required for the seeder to run; it is idempotent.
        builder.UseEnvironment(Environments.Development);
    }

    // Implemented explicitly: xUnit's IAsyncLifetime returns Task, while
    // WebApplicationFactory.DisposeAsync returns ValueTask, so the two cannot
    // share a signature. Base disposal still runs via IDisposable.
    async Task IAsyncLifetime.InitializeAsync()
    {
        await EnsureSqlReachableAsync();

        // Force host construction (and therefore migration + seeding) once,
        // before any test runs.
        using var client = CreateClient();
        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));
        response.EnsureSuccessStatusCode();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    private static async Task EnsureSqlReachableAsync()
    {
        var probe = new SqlConnectionStringBuilder(ConnectionString)
        {
            InitialCatalog = "master",
            ConnectTimeout = 30,
        }.ConnectionString;

        try
        {
            await using var connection = new SqlConnection(probe);
            await connection.OpenAsync();
        }
        catch (SqlException ex)
        {
            throw new InvalidOperationException(
                "Integration tests need a reachable SQL Server. Start LocalDB with "
                + "'sqllocaldb start MSSQLLocalDB', or set OPENDEALER360_TEST_SQL to another "
                + $"instance. See CLAUDE.md. Attempted: {MaskCredentials(probe)}",
                ex);
        }
    }

    private static string MaskCredentials(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!string.IsNullOrEmpty(builder.Password))
        {
            builder.Password = "***";
        }

        return builder.ConnectionString;
    }
}

[CollectionDefinition(nameof(HostCollection))]
public sealed class HostCollection : ICollectionFixture<HostFixture>;
