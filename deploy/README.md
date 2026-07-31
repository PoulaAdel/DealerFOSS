# Running OpenDealer360 locally

From a clean machine to a running application. Nothing here installs a database
or a JavaScript runtime onto your computer — those live in containers, so the
machine stays clean and every contributor gets the same versions.

**You need:** the .NET 10 SDK, and Docker Desktop. That is the whole list.

---

## The short version

```bash
docker compose -f deploy/docker-compose.yml up -d sql
```

```bash
dotnet tool restore && dotnet build OpenDealer360.slnx -c Release
```

```bash
dotnet run --project src/App
```

The application comes up on `http://localhost:5080`. Sign in with
`gm@dev.local` / `Dev@Pass1!` and the header `X-Tenant: northgroup`.

---

## What each command actually does

### `docker compose ... up -d sql`

Starts **one container**, `odms-sql`, running SQL Server 2022, and publishes it on
`localhost:1433`.

The data lives in a **named Docker volume** called
`opendealer360-dev_odms-sql-data`, managed by Docker — not in a folder on your
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
Server=localhost,1433;Database=OpenDealer360_Host;User Id=sa;Password=OpenDealer360_dev!;TrustServerCertificate=True;Encrypt=False
```

`appsettings.Development.json` is git-ignored. The password above is a
development-only value that exists in the compose file in plain sight; it must
never be reused anywhere shared.

On first run with `Seed:Enabled` set, the application creates the host catalog and
two sample dealer organizations, applies every migration, and seeds sample data.
Nothing else needs doing.

### `dotnet run --project src/App`

Runs on the host, not in a container. That is deliberate: the .NET SDK is already
a normal developer install, and running the application on the host keeps the
edit-run-debug loop fast and the debugger straightforward. Containers earn their
place for *dependencies* — things you would otherwise install and later have to
uninstall.

---

## LocalDB instead of the container

SQL Server LocalDB also works and needs no Docker at all:

```bash
sqllocaldb start MSSQLLocalDB
```

```text
Server=(localdb)\MSSQLLocalDB;Database=OpenDealer360_Host;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False
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
docker exec -it odms-node sh
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
& .\deploy\verify-e2e.ps1 -HostConnection "Server=localhost,1433;Database=OpenDealer360_Host;User Id=sa;Password=OpenDealer360_dev!;TrustServerCertificate=True;Encrypt=False"
```

---

## What to do when something is wrong

| Symptom | Cause |
|---|---|
| SQL container exits, log mentions `sqlpal.dll` or `Failed to load LSA` | The data volume is a bind mount to a Windows path. It must be a named volume. |
| `dotnet ef` cannot reach a database | It uses its own design-time connection. Override with `OPENDEALER360_TENANT_CONNECTION` or `OPENDEALER360_HOST_CONNECTION`. |
| Tests fail after a lot of runs | Development data accumulates. `down -v` and let the seeder rebuild. |
| Port 1433 already in use | A local SQL Server instance is running. Stop it, or change the published port in the compose file. |
