# Local Development

← [Onboarding](ONBOARDING.md) · [Workbook](00-Workbook.md)

Prerequisites, the canonical commands, and the environment facts that are easy to
get wrong. Read this before fighting your setup — on several of these the obvious
choice is not the working one.

## Prerequisites

| Need | Version | Note |
|---|---|---|
| .NET SDK | 10.0 | the solution is `DealerFOSS.slnx`, the .NET 10 format — there is no `.sln` |
| SQL Server | 2022 | container or LocalDB; see below |
| Node.js | 22 | only for `frontend/`, and a container can supply it |
| Docker | any recent | optional — LocalDB covers the backend without it |

Run once per clone, because `dotnet-ef` is pinned in `dotnet-tools.json`:

```bash
dotnet tool restore
```

## Canonical commands

```bash
dotnet build DealerFOSS.slnx -c Release
```

```bash
dotnet test DealerFOSS.slnx -c Release
```

```bash
dotnet run --project src/App
```

The tenant-isolation proof — the end-to-end check, which expects `PASS`:

```bash
& .\deploy\verify-e2e.ps1 -HostConnection "Server=(localdb)\MSSQLLocalDB;Database=DealerFOSS_Host;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False"
```

When a change touches `frontend/`, all four. `npm audit` is the counterpart of
`NuGetAudit`: a high advisory fails CI.

```bash
npm audit --audit-level=high && npm run typecheck && npm test && npm run build
```

## The database

Both paths work. Pick either.

**LocalDB** is the lightest, and is what the verification script defaults to:

```bash
sqllocaldb start MSSQLLocalDB
```

```text
Server=(localdb)\MSSQLLocalDB;Database=DealerFOSS_Host;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False
```

**The container** is in `deploy/docker-compose.yml`; `deploy/README.md` covers it.
One trap is worth stating outright, because it cost a day and reads like a host
incompatibility rather than a configuration mistake: **do not bind-mount
`/var/opt/mssql` to a Windows path.** The SQL Server Linux image cannot use one,
and it dies inside SQLPAL with a message that looks like your machine is
unsupported. A named Docker volume fixes it, and the compose file uses one.

## Node without installing Node

The `node` profile in `deploy/docker-compose.yml` provides Node 22 with the
repository mounted, so the host needs nothing installed. The container's own
command is `sleep infinity`, which means the Vite dev server is a process inside
it and can be restarted without touching the container:

```bash
docker exec dealerfoss-node sh -c "cd /workspace/frontend && npm test"
```

Vite's file watcher does not see edits across a Windows bind mount, so polling is
enabled in `vite.config.ts`. Without it, hot reload is silently dead and a hard
refresh still serves the previous bundle — which reads as "my change did nothing".

## Windows PowerShell 5.1

The scripts here are written for it, since it is what ships with Windows and
PowerShell 7 is not always present. It does not parse `&&`, `??`, or ternaries, so
chain commands by putting one per line. Invoke scripts with the call operator:

```bash
& .\deploy\verify-e2e.ps1
```

## Migrations

There are four migration sets across three folders, and they are generated
artifacts — excluded from style analysis, and never hand-edited.

| Folder | Context | Holds |
|---|---|---|
| `src/Identity/Migrations` | `IdentityDb` | the per-tenant `identity` schema |
| `src/Identity/Migrations` | `ControlPlaneDb` | the `control` schema in the host catalog |
| `src/App/Tenancy/Migrations` | `HostDb` | host catalog routing |
| `src/App/Data/Migrations` | `TenantDb` | tenant business data |

A control-plane migration needs `--context ControlPlaneDb --output-dir Migrations`,
and EF will warn that two contexts share one migrations namespace. **Accept the
warning.** The flat folder is what the analyzer exclusion `[**/Migrations/*.cs]`
matches, and `--namespace` makes `dotnet ef` write the model snapshot to a
namespace-derived path outside it. The warning's failure mode is two migrations
with the same class name — a compile error, not a silent one.

## House rules that will fail your build

- **Warnings are errors.** `NuGetAudit` fails the build on a vulnerable package.
  Upgrade it rather than suppressing the check.
- **Three projects, and only three** ([ADR-017](adr/0017-three-projects-flat-features.md)):
  `src/Core`, `src/Identity`, `src/App`. A new capability is a flat folder inside
  `src/App` — not a project, and not a `Domain/`, `Data/`, or `Contracts/`
  subfolder inside the capability.
- **Identity's internals are sealed.** A short list of contracts is public and
  everything else is `internal`. **The list lives in one place —
  `tests/Architecture/BoundaryTests.cs` — and this file deliberately does not
  copy it**, because the copy that used to be here went stale three times and
  was three names short by the time anyone noticed. Read the test: every entry
  carries a comment saying why exporting it was a security decision. Making
  another type public fails that test, which is the point.
- **SPDX is applied once** at assembly level in `Directory.Build.props`. Do not add
  per-file licence headers.
- **Never commit secrets.** `appsettings.Development.json` is git-ignored; the
  committed example is `appsettings.Development.json.example`.
