// ControlPlaneSeeder — creates the development administrator. Development only.
//
// Use:  called by the application's DevelopmentSeeder, for the same reason
//       IdentitySeeder is: administrator records are internal to this project.
// Edit: it is idempotent, and it never overwrites an existing password or an
//       existing second factor. Never seed a real administrator here, and never
//       seed one outside Development — an account that operates the whole
//       installation must be created deliberately by whoever owns it.

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenDealer360.Core;

namespace OpenDealer360.Identity;

/// <summary>
/// DEVELOPMENT ONLY. Provisions the control-plane schema in the host catalog and
/// one well-known administrator account, so the separation between operating the
/// deployment and reading a dealership's records can be exercised end to end.
/// </summary>
public static class ControlPlaneSeeder
{
    public static async Task SeedDevelopmentAsync(
        string hostConnection,
        IClock clock,
        Guid administratorId,
        string email,
        string displayName,
        string password)
    {
        ArgumentNullException.ThrowIfNull(clock);

        var options = new DbContextOptionsBuilder<ControlPlaneDb>()
            .UseSqlServer(hostConnection)
            .Options;

        await using var db = new ControlPlaneDb(options);
        await db.Database.MigrateAsync();

        var existing = await db.Administrators.SingleOrDefaultAsync(a => a.Id == administratorId);
        if (existing is not null)
        {
            return;
        }

        var administrator = new Administrator(administratorId, email, displayName, clock.UtcNow);
        administrator.SetPasswordHash(new PasswordHasher<Administrator>().HashPassword(administrator, password));

        db.Administrators.Add(administrator);
        await db.SaveChangesAsync();
    }
}
