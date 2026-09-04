// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   HostDbDesignTimeFactory — lets "dotnet ef" construct the host catalog context
//   outside the running application.
//
// Usage:
//   Tooling only; never referenced by application code.
//
// Coding Instructions:
//   Override the target with DEALERFOSS_HOST_CONNECTION. The connection is
//   used by "database update"; "migrations add" needs only the model. The
//   default is trusted LocalDB on purpose — a credential in source, even a
//   development one, is a habit worth not having.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DealerFOSS.Tenancy;

public sealed class HostDbDesignTimeFactory : IDesignTimeDbContextFactory<HostDb>
{
    public HostDb CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("DEALERFOSS_HOST_CONNECTION")
            ?? @"Server=(localdb)\MSSQLLocalDB;Database=DealerFOSS_Host;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

        var options = new DbContextOptionsBuilder<HostDb>()
            .UseSqlServer(connection)
            .Options;

        return new HostDb(options);
    }
}
