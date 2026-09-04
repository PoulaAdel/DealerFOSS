// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ControlPlaneDesignTimeFactory — lets "dotnet ef" build the control-plane
//   context outside the running application.
//
// Usage:
//   Tooling only; never referenced by application code.
//
// Coding Instructions:
//   Override the target with DEALERFOSS_HOST_CONNECTION — the control
//   plane lives in the host catalog, so it is the same connection HostDb
//   uses, not a tenant's.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DealerFOSS.Identity;

/// <summary>Design-time context builder for <c>dotnet ef</c>.</summary>
internal sealed class ControlPlaneDesignTimeFactory : IDesignTimeDbContextFactory<ControlPlaneDb>
{
    public ControlPlaneDb CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("DEALERFOSS_HOST_CONNECTION")
            ?? @"Server=(localdb)\MSSQLLocalDB;Database=DealerFOSS_Host;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

        var options = new DbContextOptionsBuilder<ControlPlaneDb>()
            .UseSqlServer(connection, sql => sql.MigrationsAssembly(typeof(ControlPlaneDb).Assembly.FullName))
            .Options;

        return new ControlPlaneDb(options);
    }
}
