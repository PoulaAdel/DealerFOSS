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

- **SQL: both the container and LocalDB work.** Verified 2026-07-31 by connecting
  to each. An earlier note here claimed the container was unusable on this host;
  that was wrong. The failure was `deploy/docker-compose.yml` bind-mounting
  `/var/opt/mssql` to a Windows path, which the SQL Server Linux image cannot use —
  it dies inside SQLPAL with a message that reads like a host incompatibility. A
  named Docker volume fixes it, and the compose file now uses one.

  ```
  Server=(localdb)\MSSQLLocalDB;Database=DealerFOSS_Host;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False
  ```

  LocalDB starts with `sqllocaldb start MSSQLLocalDB` and is what the verification
  script defaults to. See `deploy/README.md` for the container path.

- **The maintainer runs anything outside this folder.** Containers, image pulls,
  installs, services — write the command into the handoff with an explanation of
  what it creates and how to undo it; do not run it. Reading host state
  (`docker ps`, `--version`) is fine.

- **Shell is Windows PowerShell 5.1.** `pwsh` (PowerShell 7) is **not**
  installed. Invoke scripts with `& .\deploy\verify-e2e.ps1`, and avoid `&&`,
  `??`, and ternaries, which 5.1 does not parse.

  **This applies to commands handed to the maintainer, not only to commands the
  agent runs.** A `git add -A && git commit -m "..."` block fails on their shell
  and they have to repair it by hand. Hand over one command per line instead, run
  from the repository root:

  ```
  git add -A
  git commit -m "..."
  ```

- **Node.js is not installed on the host, and does not need to be.** The `node`
  profile in `deploy/docker-compose.yml` provides Node 22 in a container with the
  repository mounted. Frontend work runs there. Do not report the host absence as
  a code defect, and do not ask for a host install.

  Starting or stopping that container is the maintainer's call. Running a command
  in one that is **already up** is not — the repository is mounted, so the work
  happens inside this folder:

  ```
  docker exec dealerfoss-node sh -c "cd /workspace/frontend && npm test"
  ```

  The container's own command is `sleep infinity`, so the Vite dev server is a
  process inside it and can be restarted without touching the container.

- **The agent's browser can reach this machine's localhost.** Verified
  2026-08-03: `preview_start` at `http://localhost:5173` loads the application,
  signs in, and drives every screen. Visual verification is therefore *not*
  blocked on the maintainer, and screens must be checked rather than assumed.
  It needs the `dealerfoss-node` container up and the backend on 5080. What still
  needs a person: whether a phone camera physically reads a QR code.

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

Tenant-isolation proof (the end-to-end check; expects `PASS`):

```bash
& .\deploy\verify-e2e.ps1 -HostConnection "Server=(localdb)\MSSQLLocalDB;Database=DealerFOSS_Host;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False"
```

Frontend, when the change touches `frontend/` (all four, and `npm audit` is the
counterpart of `NuGetAudit` — a high advisory fails CI):

```bash
docker exec dealerfoss-node sh -c "cd /workspace/frontend && npm audit --audit-level=high && npm run typecheck && npm test && npm run build"
```

Notes: the solution is `.slnx` (the .NET 10 format) — `DealerFOSS.sln` does
not exist. `dotnet-ef` is pinned in `dotnet-tools.json`; run `dotnet tool restore`
once per clone.

## Working rules

### Nothing leaves this machine

Absolute. Not covered by any standing approval, and a previous "yes" to one of
these does not carry to the next occasion.

- **Never `git push`, open a pull request, or otherwise touch the remote.** The
  maintainer pushes manually. The `origin` remote stays configured for their use;
  do not invoke it, and do not remove it.
- **Never publish a Claude artifact.** Artifacts are reachable from the
  maintainer's other devices. The visual progress view is device-only at
  `local/progress.html` (git-ignored); the shared one is
  [`docs/PROGRESS.md`](docs/PROGRESS.md).
- Anything else that changes state outside this repository needs an explicit ask
  first (doc 10 §4).

### Git: main only

This is a single-maintainer repository with one agent committing sequentially, and
pushes are manual — so local `main` is already a staging area, and nothing is
public until the maintainer pushes. Feature branches would add a second layer of
isolation on top of one that already exists.

- **Commit directly to `main`.** Do not create feature branches, and do not leave
  more than one milestone sitting on a branch other than `main`.
- **Verify before every commit**, never after: `dotnet build`, `dotnet test`, and
  `deploy/verify-e2e.ps1` must all pass. Local verification is the real gate; CI
  runs after a push and only confirms.
- One commit per milestone, with a message that says what now works that did not
  before. Never `--no-verify`. Never rewrite pushed history.
- **Branch only for a genuinely risky change** the maintainer wants to inspect
  before it lands — then say so explicitly and delete the branch once merged.
  Adopt branch-per-milestone properly if a second contributor appears, or once CI
  is reliably green and worth gating on.

### Everything else

- Warnings are errors. `NuGetAudit` fails the build on a vulnerable package —
  upgrade it rather than suppressing the check.
- **Three projects, and only three** (ADR-017): `src/Core`, `src/Identity`,
  `src/App`. A new capability is a flat folder inside `src/App`, not a project.
  Do not add `Domain/`, `Data/`, or `Contracts/` subfolders inside a capability.
- **Identity's internals are sealed.** Only `IAccessDirectory`, `IAuthenticator`,
  `ISecurityPolicy`, `IGlobalAdministration`, `IdentityRegistration`,
  `IdentitySeeder`, `ControlPlaneSeeder`, and `Permissions` (plus their result
  records) are public, and `BoundaryTests` asserts that list. Making another type
  public is a security decision, not a convenience — say so explicitly if you do
  it. Credential verification, TOTP, and session issuance live here and nowhere
  else, including for the control plane: a second copy in `src/App` would be
  reachable from every feature.
- EF migrations under any `Migrations/` folder are generated artifacts and are
  excluded from style analysis; do not hand-edit them. There are four, across
  three folders: `src/Identity/Migrations` holds two contexts — `IdentityDb`
  (per-tenant `identity` schema) and `ControlPlaneDb` (the `control` schema in
  the host catalog) — plus `src/App/Tenancy/Migrations` (host catalog routing)
  and `src/App/Data/Migrations` (tenant business data). A control-plane migration
  needs `--context ControlPlaneDb --output-dir Migrations`, and EF will warn that
  two contexts share one migrations namespace. Accept the warning: the flat
  folder is what the analyzer exclusion `[**/Migrations/*.cs]` matches, and
  `--namespace` makes `dotnet ef` write the model snapshot to a namespace-derived
  path outside it. The warning's failure mode is two migrations with the same
  class name, which is a compile error, not a silent one.
- SPDX is applied once at assembly level in `Directory.Build.props`. Do not add
  per-file licence headers.
- Never commit secrets. `appsettings.Development.json` is git-ignored; the
  committed example file is `appsettings.Development.json.example`.
