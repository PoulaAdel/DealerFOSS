// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   IdentityDesignTimeFactory — lets "dotnet ef" build this context outside the
//   running application.
//
// Usage:
//   Tooling only; never referenced by application code.
//
// Coding Instructions:
//   Override the target with DEALERFOSS_TENANT_CONNECTION. Migrations
//   describe the schema; they are applied per tenant at provisioning time.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using DealerFOSS.Core;

namespace DealerFOSS.Identity;

/// <summary>Design-time context builder for <c>dotnet ef</c>.</summary>
internal sealed class IdentityDesignTimeFactory : IDesignTimeDbContextFactory<IdentityDb>
{
    public IdentityDb CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("DEALERFOSS_TENANT_CONNECTION")
            ?? @"Server=(localdb)\MSSQLLocalDB;Database=DealerFOSS_Tenant_design;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

        var options = new DbContextOptionsBuilder<IdentityDb>()
            .UseSqlServer(connection, sql => sql.MigrationsAssembly(typeof(IdentityDb).Assembly.FullName))
            .Options;

        return new IdentityDb(options, new SystemClock());
    }
}
