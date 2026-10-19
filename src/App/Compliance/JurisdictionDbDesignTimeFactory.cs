// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   JurisdictionDbDesignTimeFactory — lets "dotnet ef" construct the
//   jurisdiction context outside the running application.
//
// Usage:
//   Tooling only; never referenced by application code.
//
//   dotnet ef migrations add <Name> --project src/App --context JurisdictionDb
//
// Coding Instructions:
//   Same shape and same reasoning as HostDbDesignTimeFactory, including the
//   default: trusted LocalDB, because a credential in source is a habit worth
//   not having even when it is only a development one. This context points at
//   the HOST catalog, not a tenant database — the override is therefore the same
//   DEALERFOSS_HOST_CONNECTION the host factory reads, so the two cannot be
//   pointed at different databases by accident.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DealerFOSS.Compliance;

internal sealed class JurisdictionDbDesignTimeFactory : IDesignTimeDbContextFactory<JurisdictionDb>
{
    public JurisdictionDb CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("DEALERFOSS_HOST_CONNECTION")
            ?? @"Server=(localdb)\MSSQLLocalDB;Database=DealerFOSS_Host;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

        var options = new DbContextOptionsBuilder<JurisdictionDb>()
            .UseSqlServer(connection)
            .Options;

        return new JurisdictionDb(options);
    }
}
