# Running DealerFOSS locally

From a clean machine to a running application. Nothing here installs a database
or a JavaScript runtime onto your computer — those live in containers, so the
machine stays clean and every contributor gets the same versions.

**You need:** the .NET 10 SDK, and Docker Desktop. That is the whole list.

> **Running an installation, rather than developing on one, is a different
> document.** [`docs/OPERATING.md`](../docs/OPERATING.md) covers that. This page
> is for contributors.

## What is in this folder

| File | For |
|---|---|
| `docker-compose.yml` | **development** dependencies — SQL, the Node toolchain, optional Redis and a telemetry collector |
| `docker-compose.app.yml` | **running the product** — the application and its database, built from source |
| `Dockerfile` | the application image, frontend and all, built from source |
| `Dockerfile.prebuilt` | the same image from an already-published folder — a third of the download, for when bandwidth is the constraint |
| `.env.example` | copy to `.env` for `docker-compose.app.yml`; it has no defaults on purpose |
| `publish.ps1` | builds the frontend and publishes the application into one folder |
| `install-service.ps1` | registers that folder as a Windows service, and removes it again |
| `new-key.ps1` | generates a secret-protection key |
| `backup.ps1`, `restore.ps1` | the backup drill |
| `verify-e2e.ps1` | the end-to-end proof |

---

## The short version

```bash
docker compose -f deploy/docker-compose.yml up -d sql
```

```bash
dotnet tool restore && dotnet build DealerFOSS.slnx -c Release
```

```bash
dotnet run --project src/App
```

The application comes up on `http://localhost:5080`. Sign in with
`gm@dev.local` / `Dev@Pass1!` and the header `X-Tenant: northgroup`.

---

## What each command actually does

### `docker compose ... up -d sql`

Starts **one container**, `dealerfoss-sql`, running SQL Server 2022, and publishes it on
`localhost:1433`.

The data lives in a **named Docker volume** called
`dealerfoss-dev_dealerfoss-sql-data`, managed by Docker — not in a folder on your
drive. That matters for two reasons: your working folder stays clean, and SQL
Server refuses to run properly on a Windows bind mount (see the note in
`docker-compose.yml` — it fails inside SQLPAL with a message that looks like a
host incompatibility and is not one).

The first run pulls roughly 1.5 GB and takes a few minutes. After that it starts
in about thirty seconds. Watch it become healthy:

```bash
docker compose -f deploy/docker-compose.yml ps
```

**To stop it, keeping the data:**

```bash
docker compose -f deploy/docker-compose.yml stop
```

**To remove the containers, keeping the data:**

```bash
docker compose -f deploy/docker-compose.yml down
```

**To throw the databases away and start clean** — this deletes every dealer
database, which is exactly what you want before rehearsing a restore:

```bash
docker compose -f deploy/docker-compose.yml down -v
```

### Connecting the application to it

The application reads its connection string from configuration. Copy the example
file once:

```bash
cp src/App/appsettings.Development.json.example src/App/appsettings.Development.json
```

and set the host catalog connection to the container:

```text
Server=localhost,1433;Database=DealerFOSS_Host;User Id=sa;Password=DealerFOSS_dev!;TrustServerCertificate=True;Encrypt=False
```

`appsettings.Development.json` is git-ignored. The password above is a
development-only value that exists in the compose file in plain sight; it must
never be reused anywhere shared.

On first run with `Seed:Enabled` set, the application creates the host catalog and
two sample dealer organizations, applies every migration, and seeds sample data.
Nothing else needs doing.

The host catalog holds two schemas: `dbo` for tenant routing, and `control` for
the people who operate the deployment — administrators, their sessions, the
support grants they have been given, and the installation's own append-only log.
Development seeds two administrator accounts there. **Neither belongs in a real
installation**: `Seed:Enabled` is the switch, and outside Development an
administrator has to be created deliberately by whoever owns the deployment.
There is no endpoint for that yet — see `docs/implementation/STATUS.md`.

### `dotnet run --project src/App`

Runs on the host, not in a container. That is deliberate: the .NET SDK is already
a normal developer install, and running the application on the host keeps the
edit-run-debug loop fast and the debugger straightforward. Containers earn their
place for *dependencies* — things you would otherwise install and later have to
uninstall.

---

## Backup and restore

A backup nobody has restored from is not a backup. So the deliverable here is the
**drill**, not the script — and the drill ends by running the application against
the restored copy, because that is the only thing that proves the backup was any
good.

```bash
& .\deploy\backup.ps1
```

```bash
& .\deploy\restore.ps1 -From .\local\backups\<timestamp> -Verify
```

The restore lands **alongside** the original under a `Restored_` prefix, so the
drill can be run on a machine that is already serving the real thing. That is
also the only way to prove anything: a restore that overwrote the original would
tell you nothing about whether the backup worked.

### The step that is easy to miss

Each tenant's connection string lives in the host catalog **encrypted**. Restore
the catalog under a new name and every row still points at the *original*
databases — so a "restored" installation would quietly read and write the live
ones. That is worse than a restore that plainly failed.

Only something holding the deployment's keys can rewrite those rows, so
`restore.ps1` calls the application rather than being handed the keys:

```bash
dotnet run --project src/App -- --repoint-tenants --prefix Restored_ --dry-run
```

Add `--server <name>` when restoring onto a different machine. It is idempotent —
running it twice does not produce `Restored_Restored_…`.

### What the drill actually checks

1. **Every file against its checksum, before anything is restored.** A backup
   truncated in transit looks like data until the day you need it. Damaging a
   manifest checksum was rehearsed: the restore refuses and touches nothing.
2. **That the restored databases contain data.** The end-to-end check seeds what
   it does not find, so without this guard an empty restore would be seeded from
   scratch and pass — proving the application works and the backup does not.
   Counting rows is not sufficient proof, but it is necessary.
3. **That the application runs on it.** `verify-e2e.ps1` against the restored
   catalog, on port 5099 so it cannot collide with a development host.

### What this deliberately does not do

Copy anything off this host, encrypt the files, schedule itself, or expire old
backups. **A `.bak` holds every customer record in plain form**, so where these
files end up is a decision somebody has to make deliberately — doc 08 owns it.

Tenant databases are found by the naming convention
(`<catalog>_Tenant_<slug>`), not by decrypting the catalog. A tenant whose stored
connection points somewhere off-convention would be missed. Nothing creates such
a tenant today; if something ever does, this is the script that has to change.

---

## LocalDB instead of the container

SQL Server LocalDB also works and needs no Docker at all:

```bash
sqllocaldb start MSSQLLocalDB
```

```text
Server=(localdb)\MSSQLLocalDB;Database=DealerFOSS_Host;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False
```

It is Windows-only and it lives on your machine rather than in a container, so it
is the less clean option — but it is faster to start and perfectly adequate. Both
paths are supported and both are verified.

---

## The frontend toolchain

Node is **not** installed on the host either. Start the workspace container:

```bash
docker compose -f deploy/docker-compose.yml --profile node up -d
```

Then work inside it:

```bash
docker exec -it dealerfoss-node sh
```

You are now in `/workspace`, which is this repository. Run `npm`, `pnpm`, `vite`
— anything — and it happens inside the container using Node 22. The files it
creates land in your working folder because the repository is mounted; the
`node_modules` folder does not, because it holds platform-specific binaries that
would be both slow and wrong to share with Windows.

The Vite dev server port, 5173, is published, so a frontend started inside the
container is reachable at `http://localhost:5173` in your normal browser.

**To remove it entirely, including the installed packages:**

```bash
docker compose -f deploy/docker-compose.yml --profile node down -v
```

---

## Optional extras

Neither is needed for a single-node development loop (ADR-005).

```bash
docker compose -f deploy/docker-compose.yml --profile redis up -d
```

```bash
docker compose -f deploy/docker-compose.yml --profile otel up -d
```

Redis is for scale-out work. The OpenTelemetry collector receives traces and
metrics when `OpenTelemetry:OtlpEndpoint` is configured; without it the
application runs perfectly well and simply exports nothing.

---

## Secret protection — required outside Development

Tenant connection strings are encrypted at rest. Without a key the application
uses a pass-through and **refuses to start** in any environment but Development —
storing them in plaintext is not a degraded mode, it is a breach waiting to be
found.

Generate a key:

```powershell
& .\deploy\new-key.ps1
```

It prints the key id and the key, and the warning that matters next to them.
`-Format env` gives the two environment variables ready to paste; `-Format json`
gives an `appsettings` fragment.

> This used to be a three-line snippet here using
> `[RandomNumberGenerator]::Fill($b)`. That method takes a `Span<byte>` and exists
> only on .NET Core 2.1 and later — **it is not present in Windows PowerShell
> 5.1**, which runs on .NET Framework. The documented first step of an
> installation therefore failed on the most likely shell, with an error about a
> missing method rather than anything to do with keys. The script uses
> `RandomNumberGenerator.Create()` and `GetBytes()`, which work on both.

Then supply it. Configuration keys map to environment variables by replacing `:`
with `__`, which is how you avoid putting a key in a file on the two targets where
that would be awkward.

**Windows service**

Set machine-level environment variables on the service account, or use a
`appsettings.Production.json` with locked-down NTFS permissions:

```
setx /M Secrets__CurrentKeyId "2026-07"
setx /M Secrets__Keys__2026-07 "<the base64 key>"
```

**Linux container**

Pass it as an environment variable, ideally from a mounted secret rather than the
compose file:

```
-e Secrets__CurrentKeyId=2026-07
-e Secrets__Keys__2026-07=<the base64 key>
```

**Hosted**

Use the platform's secret store — Key Vault, Secrets Manager, whatever it
provides — surfaced as the same two environment variables. The application does
not care where they came from.

### Rotating a key

Add the new key **alongside** the old one and point `CurrentKeyId` at it:

```json
"Secrets": {
  "CurrentKeyId": "2026-10",
  "Keys": {
    "2026-07": "<old key>",
    "2026-10": "<new key>"
  }
}
```

New values are written with the new key; existing ones still decrypt with the old.
**Do not remove the old key** until everything written under it has been
re-encrypted — and note that no tool does that re-encryption yet, so for now
rotation means "add a key and keep the old one".

## Behind a reverse proxy — required, or the sign-in limiter misfires

**Skip this if the application is reached directly.** It matters only when
something sits in front of it: nginx, Apache, IIS ARR, Caddy, a cloud load
balancer, or a hosting platform's ingress.

Sign-in attempts are rate limited **per caller**, and the caller is identified by
their network address. Behind a proxy, every request arrives from the *proxy's*
address — so all of them land in one bucket, and twenty failed sign-ins from
anybody, including one attacker, locks out the whole dealership.

The real client is in the `X-Forwarded-For` header, but that header is only worth
reading if it came from a proxy you actually run. Anyone can send one, and an
attacker who can write their own address gets a fresh bucket on every request —
a limiter that is worse than useless because it looks like it is working. So the
application refuses to read it until you say which proxies may set it:

```json
{
  "Network": {
    "TrustedProxies": [ "10.0.0.5", "10.1.0.0/16" ]
  }
}
```

Or as environment variables, which is how the container and a hosted target
usually take it:

```bash
Network__TrustedProxies__0=10.0.0.5
Network__TrustedProxies__1=10.1.0.0/16
```

Single addresses and CIDR ranges are both accepted. **List the address the proxy
connects *from*, which is not always the address it listens on** — on Docker it
is the container's address on the shared network, not the published port's host.
`docker inspect` or one line in the access log will tell you.

Two things worth knowing:

- **A typo stops the service starting.** An entry that is neither an address nor
  a range throws at startup and names itself. Dropping it silently would leave
  the limiter partitioning on the proxy's address while you believed it fixed —
  invisible until the day it mattered.
- **It also fixes HSTS.** A proxy that terminates TLS forwards plain HTTP, so the
  application sees a non-secure request and omits `Strict-Transport-Security`.
  `X-Forwarded-Proto` is honoured alongside the address, so configuring this is
  what makes that header actually appear.

Only one hop is trusted. A chain of two proxies needs `Network:ForwardLimit`
raised deliberately — each extra hop is one more machine you are choosing to
believe.

Confirm it took: the log line at startup names what it trusted.

```
Trusting forwarded headers from 2 configured proxy source(s): …
```

## Proving it works end to end

```bash
& .\deploy\verify-e2e.ps1
```

Starts the application, signs in as four different users, and asserts that two
dealer organizations cannot see each other, that a location cannot reach another
location's stock, enquiries, or deals, that a salesperson cannot approve their own
deal, and that the ledger balances. It prints `PASS` or explains what failed.

Pass `-HostConnection` to point it at the container instead of LocalDB:

```bash
& .\deploy\verify-e2e.ps1 -HostConnection "Server=localhost,1433;Database=DealerFOSS_Host;User Id=sa;Password=DealerFOSS_dev!;TrustServerCertificate=True;Encrypt=False"
```

---

## What to do when something is wrong

| Symptom | Cause |
|---|---|
| SQL container exits, log mentions `sqlpal.dll` or `Failed to load LSA` | The data volume is a bind mount to a Windows path. It must be a named volume. |
| `dotnet ef` cannot reach a database | It uses its own design-time connection. Override with `DEALERFOSS_TENANT_CONNECTION` or `DEALERFOSS_HOST_CONNECTION`. |
| A stray `DealerFOSS_Test_*` database | A test run crashed before tidying up. The next run sweeps anything over six hours old; dropping it by hand is safe. |
| Port 1433 already in use | A local SQL Server instance is running. Stop it, or change the published port in the compose file. |
