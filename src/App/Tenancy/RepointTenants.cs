// RepointTenants — after a restore, make the host catalog name the databases
// that were actually restored.
//
// Use:  dotnet run --project src/App -- --repoint-tenants --prefix Restored_
// Edit: this exists because of what a restore drill exposed. Each tenant's
//       connection string lives in the host catalog **encrypted**, so restoring
//       the catalog under a new name leaves every row still naming the original
//       databases. A "restored" installation would then quietly read and write
//       the live ones — which is worse than a restore that plainly failed.
//
//       Re-pointing therefore has to decrypt and re-encrypt, and only something
//       holding the deployment's keys can do that. A PowerShell script cannot,
//       and should not be given the keys to try. So it lives here, in the
//       application, and the restore script calls it.
//
//       It is deliberately not an endpoint. This runs against a catalog that is
//       not serving anybody yet, from a console, by whoever is performing the
//       restore.

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using DealerFOSS.Core;

namespace DealerFOSS.Tenancy;

internal static class RepointTenants
{
    public const string Verb = "--repoint-tenants";

    /// <summary>
    /// Rewrites every tenant's stored connection so it names the restored copy.
    /// Returns the process exit code: 0 when every row was re-pointed.
    /// </summary>
    /// <remarks>
    /// <paramref name="prefix"/> is prepended to each database name, and
    /// <paramref name="server"/> replaces the server when supplied — a restore
    /// onto a different machine needs both. Nothing else in the connection is
    /// touched, so credentials and options survive.
    /// </remarks>
    public static async Task<int> RunAsync(
        HostDb catalog,
        ISecretProtector protector,
        IClock clock,
        string prefix,
        string? server,
        bool dryRun,
        TextWriter output)
    {
        var tenants = await catalog.Tenants.OrderBy(t => t.Slug).ToListAsync();
        if (tenants.Count == 0)
        {
            await output.WriteLineAsync(
                "No tenants in this catalog. Either it is empty or you are pointed at the wrong one.");
            return 1;
        }

        foreach (var tenant in tenants)
        {
            string current;
            try
            {
                current = protector.Unprotect(tenant.ProtectedConnectionString);
            }
            catch (InvalidOperationException ex)
            {
                // Almost always the wrong key ring: the catalog was restored to a
                // machine that does not hold the keys it was encrypted with.
                await output.WriteLineAsync(
                    $"{tenant.Slug}: could not read the stored connection — {ex.Message}");
                return 1;
            }

            var builder = new SqlConnectionStringBuilder(current);
            var before = builder.InitialCatalog;

            // Idempotent: running the script twice must not produce
            // Restored_Restored_DealerFOSS_Tenant_northgroup.
            if (!before.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                builder.InitialCatalog = prefix + before;
            }

            if (!string.IsNullOrWhiteSpace(server))
            {
                builder.DataSource = server;
            }

            await output.WriteLineAsync(
                $"{tenant.Slug}: {before} -> {builder.InitialCatalog}"
                + (string.IsNullOrWhiteSpace(server) ? string.Empty : $" on {server}"));

            if (dryRun)
            {
                continue;
            }

            tenant.ProtectedConnectionString = protector.Protect(builder.ConnectionString);
            tenant.ModifiedAt = clock.UtcNow;
        }

        if (dryRun)
        {
            await output.WriteLineAsync(
                $"Dry run: {tenants.Count} tenant(s) would be re-pointed. Nothing was written.");
            return 0;
        }

        await catalog.SaveChangesAsync();
        await output.WriteLineAsync($"Re-pointed {tenants.Count} tenant(s).");
        return 0;
    }

    /// <summary>Reads the verb's options off the command line.</summary>
    public static (string Prefix, string? Server, bool DryRun) Parse(string[] args)
    {
        var prefix = Value(args, "--prefix") ?? "Restored_";
        var server = Value(args, "--server");
        var dryRun = args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);

        return (prefix, server, dryRun);
    }

    private static string? Value(string[] args, string name)
    {
        var index = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
