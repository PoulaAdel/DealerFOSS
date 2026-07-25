# CLAUDE.md — operating instructions for coding agents

This file holds **machine and workflow facts only**. Product and architecture
decisions live in `docs/` and are never duplicated here.

## Start here

| Question | File |
|---|---|
| What is the current state? | [`docs/implementation/STATUS.md`](docs/implementation/STATUS.md) |
| How should I work? | [`docs/10-Claude-Code-Execution-Prompt.md`](docs/10-Claude-Code-Execution-Prompt.md) |
| What phase am I in, and what are its exit criteria? | [`docs/09-Codex-Execution-Prompt.md`](docs/09-Codex-Execution-Prompt.md) |
| Why is it built this way? | [`docs/02-Architecture-and-Decisions.md`](docs/02-Architecture-and-Decisions.md) |
| Everything else | [`docs/00-Workbook.md`](docs/00-Workbook.md) |

Verify claims against the repository. A status checkbox is not evidence; a
passing command is.

## Environment on this machine

These are verified facts, not assumptions. They differ from what the execution
prompts assume by default.

- **SQL: use LocalDB, not Docker.** The SQL Server 2022 Linux container crashes
  on this host (`LSA initialization failed 0xc000004b` — a SQLPAL/WSL2
  incompatibility, not a memory or configuration problem). `deploy/docker-compose.yml`
  remains the committed target per ADR-015; only this host cannot run it.

  ```
  Server=(localdb)\MSSQLLocalDB;Database=OpenDealer360_Host;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False
  ```

  Start it with `sqllocaldb start MSSQLLocalDB`.

- **Shell is Windows PowerShell 5.1.** `pwsh` (PowerShell 7) is **not**
  installed. Invoke scripts with `& .\deploy\verify-e2e.ps1`, and avoid `&&`,
  `??`, and ternaries, which 5.1 does not parse.

- **Node.js is not installed.** Any frontend work requires installing it first;
  do not report the absence as a code defect.

## Canonical commands

```bash
dotnet build OpenDealer360.slnx -c Release
```

```bash
dotnet test OpenDealer360.slnx -c Release
```

```bash
dotnet run --project src/Host
```

Tenant-isolation proof (the end-to-end check; expects `PASS`):

```bash
& .\deploy\verify-e2e.ps1 -HostConnection "Server=(localdb)\MSSQLLocalDB;Database=OpenDealer360_Host;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False"
```

Notes: the solution is `.slnx` (the .NET 10 format) — `OpenDealer360.sln` does
not exist. `dotnet-ef` is pinned in `dotnet-tools.json`; run `dotnet tool restore`
once per clone.

## Working rules

- **Do not commit, push, create a pull request, or change any external state
  unless explicitly asked** (doc 10 §4). Work is left in the working tree for
  review. When a commit is requested: short-lived `feature/*` branch, never a
  direct merge to `main`, never `--no-verify`.
- Warnings are errors. `NuGetAudit` fails the build on a vulnerable package —
  upgrade it rather than suppressing the check.
- EF migrations under any `Migrations/` folder are generated artifacts and are
  excluded from style analysis; do not hand-edit them.
- SPDX is applied once at assembly level in `Directory.Build.props`. Do not add
  per-file licence headers.
- Never commit secrets. `appsettings.Development.json` is git-ignored; the
  committed example file is `appsettings.Development.json.example`.
