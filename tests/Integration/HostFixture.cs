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

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using DealerFOSS.App;
using DealerFOSS.Identity;

namespace DealerFOSS.IntegrationTests;

/// <summary>
/// Boots the real Host against a real SQL database and seeds the two sample
/// dealer organizations, so tests exercise the same path as
/// <c>deploy/verify-e2e.ps1</c> — but in CI.
/// </summary>
/// <remarks>
/// The connection comes from <c>DEALERFOSS_TEST_SQL</c> when set (CI supplies
/// a SQL Server service container) and falls back to LocalDB for local runs. If
/// no engine is reachable the fixture fails loudly rather than skipping: a test
/// that never reaches the database is not evidence of isolation.
/// </remarks>
public sealed class HostFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string LocalDbFallback =
        @"Server=(localdb)\MSSQLLocalDB;Database=DealerFOSS_Host;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

    /// <summary>Databases created by a test run all start with this.</summary>
    private const string TestDatabasePrefix = "DealerFOSS_Test_";

    private static string EngineConnection =>
        Environment.GetEnvironmentVariable("DEALERFOSS_TEST_SQL") ?? LocalDbFallback;

    /// <summary>
    /// Unique to this run. Declared before the connection string below, because
    /// static field initializers run in the order they are written.
    /// </summary>
    private static readonly string RunId =
        $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}"[..24];

    /// <summary>
    /// The host catalog for this run — a database nothing else is using.
    ///
    /// The suite used to share one long-lived database, and assertions quietly
    /// became order-dependent as rows piled up: three separate tests failed over
    /// time because a freshly created row fell off the end of a capped, sorted
    /// page. A run that starts empty cannot develop that problem.
    /// </summary>
    public static string ConnectionString { get; } =
        new SqlConnectionStringBuilder(EngineConnection)
        {
            InitialCatalog = $"{TestDatabasePrefix}{RunId}_Host",
        }.ConnectionString;

    /// <summary>
    /// The connection for one dealer organization's database in this run. Tests
    /// that read tables directly must go through this rather than assuming a
    /// fixed name.
    /// </summary>
    public static string TenantConnectionString(string slug) =>
        new SqlConnectionStringBuilder(ConnectionString)
        {
            InitialCatalog = DevelopmentSeeder.TenantDatabaseName(ConnectionString, slug),
        }.ConnectionString;

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

        // The credential rate limiter is effectively off in here. This suite
        // makes hundreds of sign-ins in seconds down one in-process connection,
        // which no partitioning scheme can tell apart from an attack without
        // also failing to catch a real one. The limiter is proven instead by
        // verify-e2e.ps1, against a real host over a real socket — the more
        // honest test, since that is where a real caller lives.
        builder.UseSetting("RateLimiting:CredentialAttemptsPerMinute", "1000000");
    }

    // Implemented explicitly: xUnit's IAsyncLifetime returns Task, while
    // WebApplicationFactory.DisposeAsync returns ValueTask, so the two cannot
    // share a signature. Base disposal still runs via IDisposable.
    async Task IAsyncLifetime.InitializeAsync()
    {
        await EnsureSqlReachableAsync();

        // Databases left behind by a run that crashed before it could tidy up.
        // Swept on a delay so a run happening right now on the same engine is
        // not pulled out from under itself.
        await DropDatabasesAsync(
            "name LIKE @prefix + '%' AND create_date < DATEADD(hour, -6, GETUTCDATE())");

        // Force host construction (and therefore database creation, migration,
        // and seeding) once, before any test runs.
        using var client = CreateClient();
        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));
        response.EnsureSuccessStatusCode();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        // Everything this run created — the host catalog and both tenants.
        await DropDatabasesAsync("name LIKE @prefix + @run + '%'");
    }

    /// <summary>
    /// Drops every database matching a predicate, forcing other connections off
    /// first. Best effort: a failure to tidy up must not fail the test run, or a
    /// green suite would go red for a reason unrelated to the code.
    /// </summary>
    private static async Task DropDatabasesAsync(string predicate)
    {
        try
        {
            var master = new SqlConnectionStringBuilder(ConnectionString)
            {
                InitialCatalog = "master",
            }.ConnectionString;

            await using var connection = new SqlConnection(master);
            await connection.OpenAsync();

            var names = new List<string>();
            await using (var find = connection.CreateCommand())
            {
                find.CommandText = $"SELECT name FROM sys.databases WHERE {predicate}";
                find.Parameters.AddWithValue("@prefix", TestDatabasePrefix);
                find.Parameters.AddWithValue("@run", RunId);

                await using var reader = await find.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    names.Add(reader.GetString(0));
                }
            }

            foreach (var name in names)
            {
                await using var drop = connection.CreateCommand();

                // The name comes from sys.databases, not from input, and is
                // bracketed — but it still cannot be a parameter, because DROP
                // DATABASE does not take one.
                drop.CommandText =
                    $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; "
                    + $"DROP DATABASE [{name}];";

                await drop.ExecuteNonQueryAsync();
            }
        }
        catch (SqlException)
        {
            // Leaving a database behind costs disk, not correctness. The next
            // run sweeps it.
        }
    }

    /// <summary>
    /// Signs a development user in and returns their session token, caching it
    /// so a suite of tests does not re-authenticate on every call.
    /// </summary>
    public async Task<string> TokenForAsync(string email, string tenant) =>
        (await SignInAsync(email, tenant)).SessionToken;

    /// <summary>
    /// Both halves of a signed-in session: the cookie, and the token every write
    /// must present alongside it. Tests go through this rather than forging a
    /// request the application would never accept from a browser.
    /// </summary>
    public async Task<SignedInSession> SignInAsync(string email, string tenant)
    {
        var key = $"{tenant}|{email}";
        if (_sessions.TryGetValue(key, out var cached))
        {
            return cached;
        }

        using var client = CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri("/api/v1/auth/login", UriKind.Relative))
        {
            Content = JsonContent.Create(new { email, password = DevelopmentSeeder.DevUsers.Password }),
        };
        request.Headers.Add("X-Tenant", tenant);

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var session = new SignedInSession(
            CookieFrom(response, "dfoss_session"),
            CookieFrom(response, "dfoss_csrf"));

        _sessions[key] = session;
        return session;
    }

    private static string CookieFrom(HttpResponseMessage response, string name)
    {
        var header = response.Headers.GetValues("Set-Cookie")
            .Single(v => v.StartsWith($"{name}=", StringComparison.Ordinal));

        return Uri.UnescapeDataString(header.Split(';')[0][(name.Length + 1)..]);
    }

    private readonly Dictionary<string, SignedInSession> _sessions = [];

    /// <summary>
    /// Signs the development administrator in and enrols the second factor the
    /// control plane insists on, once for the whole run.
    /// </summary>
    /// <remarks>
    /// Enrolment happens through the API rather than by writing the row, so the
    /// helper exercises the same path an operator would — including the fact that
    /// the restricted session becomes unrestricted without signing in again.
    /// </remarks>
    public async Task<SignedInAdministrator> AdministratorAsync()
    {
        if (_administrator is not null)
        {
            return _administrator;
        }

        using var client = CreateClient();

        using var login = await client.PostAsJsonAsync(
            new Uri("/api/v1/admin/login", UriKind.Relative),
            new
            {
                email = DevelopmentSeeder.DevAdministrator.Email,
                password = DevelopmentSeeder.DevUsers.Password,
            });

        login.EnsureSuccessStatusCode();

        var session = CookieFrom(login, "dfoss_admin");
        var antiForgery = CookieFrom(login, "dfoss_admin_csrf");

        using var enrol = new HttpRequestMessage(
            HttpMethod.Post, new Uri("/api/v1/admin/mfa/enrol", UriKind.Relative));
        enrol.Headers.Add("Cookie", $"dfoss_admin={session}");
        enrol.Headers.Add("X-Admin-CSRF-Token", antiForgery);

        using var enrolled = await client.SendAsync(enrol);
        enrolled.EnsureSuccessStatusCode();

        var secret = (await enrolled.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("secret").GetString()!;

        using var confirm = new HttpRequestMessage(
            HttpMethod.Post, new Uri("/api/v1/admin/mfa/confirm", UriKind.Relative))
        {
            Content = JsonContent.Create(new { code = Totp.Generate(secret, DateTimeOffset.UtcNow) }),
        };
        confirm.Headers.Add("Cookie", $"dfoss_admin={session}");
        confirm.Headers.Add("X-Admin-CSRF-Token", antiForgery);

        using var confirmed = await client.SendAsync(confirm);
        confirmed.EnsureSuccessStatusCode();

        _administrator = new SignedInAdministrator(session, antiForgery, secret);
        return _administrator;
    }

    private SignedInAdministrator? _administrator;

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
                + "'sqllocaldb start MSSQLLocalDB', or set DEALERFOSS_TEST_SQL to another "
                + "instance. See docs/LOCAL-DEVELOPMENT.md. Attempted: "
                + MaskCredentials(probe),
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

/// <summary>
/// What a browser holds after signing in: the session cookie it cannot read, and
/// the anti-forgery token it must echo on every write.
/// </summary>
public sealed record SignedInSession(string SessionToken, string AntiForgeryToken);

/// <summary>
/// A signed-in administrator: cookies that mean nothing to a tenant endpoint, and
/// the shared secret so a test can sign in again with a fresh code.
/// </summary>
public sealed record SignedInAdministrator(
    string SessionToken,
    string AntiForgeryToken,
    string TotpSecret);

[CollectionDefinition(nameof(HostCollection))]
public sealed class HostCollection : ICollectionFixture<HostFixture>;
