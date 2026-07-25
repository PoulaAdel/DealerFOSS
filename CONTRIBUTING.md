# Contributing to OpenDealer360

Thank you for helping build an open-source Dealer Management System. This file is
the short version; the authoritative rules live in the workbook under
[`docs/`](docs/00-Workbook.md).

## Before you start

1. Read [docs/01-Vision-and-Scope.md](docs/01-Vision-and-Scope.md) and
   [docs/02-Architecture-and-Decisions.md](docs/02-Architecture-and-Decisions.md).
2. For anything touching a boundary, read
   [docs/03-Project-Structure.md](docs/03-Project-Structure.md) — the module
   layout and the dependency rules are enforced by tests, not just convention.
3. Architectural changes are proposed as an ADR in
   [docs/adr/](docs/adr/README.md), never as a silent change in a pull request.

## Local setup

Read [`CLAUDE.md`](CLAUDE.md) first — it records the verified setup for this
project, including which SQL engine actually works on a given host.

```bash
# Prerequisites: .NET SDK (see global.json) · a reachable SQL Server
# Optional: Docker (dev services), Node.js (frontend, once it exists)

# 1. Start a SQL engine.
#    Windows/LocalDB (verified):
sqllocaldb start MSSQLLocalDB
#    Or containers, where the host supports them:
#    docker compose -f deploy/docker-compose.yml up -d

# 2. Build and test
dotnet tool restore
dotnet build OpenDealer360.slnx -c Release
dotnet test  OpenDealer360.slnx -c Release

# 3. Run the API host (http://localhost:5080)
dotnet run --project src/Host
#    GET /               → service banner
#    GET /health/live    → liveness
#    GET /health/ready   → readiness (dependencies)
```

Integration tests pick up `OPENDEALER360_TEST_SQL` when set and fall back to
LocalDB otherwise.

### Proving tenant isolation end to end

```bash
& .\deploy\verify-e2e.ps1 -HostConnection "Server=(localdb)\MSSQLLocalDB;Database=OpenDealer360_Host;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False"
```

This seeds one multi-rooftop and one single-rooftop dealer organization in
separate databases and asserts that each resolves only its own data. The same
assertions run in CI via `tests/Integration`. See
[`tests/Architecture/README.md`](tests/Architecture/README.md) for the
forbidden-reference rehearsal.

Copy `src/Host/appsettings.Development.json.example` to
`src/Host/appsettings.Development.json` and fill in local values. **Never commit
secrets** — that file is git-ignored.

## Definition of done (see docs 06–08)

- Domain code has no ASP.NET/EF/connector dependency; boundaries stay green in
  `tests/Architecture`.
- Expected business failures return a typed `Result`; unexpected failures throw.
- Every business write emits an audit event; retriable commands are idempotent.
- Money carries a currency; instants are UTC; user-visible strings are localizable.
- Tests accompany the change (unit, integration, authorization, migration as
  relevant). Coverage does not substitute for named critical-case tests.
- Public contract or database migration changes get explicit review.

## Provenance

The contributor sign-off policy (DCO or CLA) is finalized before external code is
accepted (docs/08 §2). Until then, keep contributions your own original work and
free of incompatible-license code.

## Commits & branches

Trunk-based: short-lived branches off `main`, one reviewed pull request each. The
PR states user/operational impact and flags any schema, public-contract, or
security effect.
