# Operating DealerFOSS

← [Local Development](LOCAL-DEVELOPMENT.md) · [Workbook](00-Workbook.md)

For whoever runs the installation. Not for dealership staff, and not for people
changing the code — this is the runbook for standing the system up, setting a
dealership up on it, and the handful of things that go wrong.

> **You are not a dealership.** Everything here happens at the *control plane*: a
> separate sign-in, at `/admin`, with its own account. There is deliberately no
> path from an operator account into a dealership's records — see
> [Support access](#5-helping-a-dealership) for the one supervised exception.

---

## 1. Before the first start

Three things must exist. The application refuses to start without the first two,
with a message naming what to set.

| | What | Why |
|---|---|---|
| **Host catalog connection** | `ConnectionStrings__HostCatalog` | The small database that knows which dealerships exist and where each one's data lives |
| **A secret key** | `Secrets__CurrentKeyId` and `Secrets__Keys__<id>` | Every dealership's database password is encrypted with it. Lose it and no dealership can be reached |
| **A first operator** | Seeded on first run | The only account that can reach `/admin` |

### The secret key

Generate it with the script, which prints the key and the warning together:

```powershell
& .\deploy\new-key.ps1
```

`-Format env` gives the two variables ready to paste; `-Format json` gives an
`appsettings` fragment. The key id is any short string — dating it makes rotation
obvious later.

```bash
Secrets__CurrentKeyId=2026-08
Secrets__Keys__2026-08=<base64 of 32 random bytes>
```

> The variable is `CurrentKeyId`. This page said `ActiveKeyId` until 2026-08-07,
> which is not what the application reads — following it exactly produced a
> refusal to start, with a message about plaintext connection strings and no
> obvious connection to the typo.

> **Back this up somewhere other than the server.** It is not recoverable, and
> without it the host catalog's connection strings cannot be decrypted — which
> means every dealership on the installation becomes unreachable. A database
> backup without the key is not a backup.

Old keys stay listed after a rotation: values encrypted under them carry the key
id, so they keep working. Nothing re-encrypts existing values yet, so rotation
today means *adding* a key, not replacing one.

---

## 2. Installing it

Two packages, one product. Pick whichever suits the machine. **The application
serves its own web interface** — there is nothing else to install and no web
server to configure in front of it.

### A Linux container

Everything is built from source in the image, including the web interface, so
there is no separate build step to remember.

```bash
cp deploy/.env.example deploy/.env
```

Fill in `deploy/.env` — a SQL password, and the key from `new-key.ps1 -Format env`.
Compose fails loudly if any of them is missing, which is deliberate: an
installation started without a key is one whose backups cannot be restored.

```bash
docker compose -f deploy/docker-compose.app.yml up -d --build
```

It comes up on `http://localhost:8080`. The database is on the compose network and
is **not** published to the host — attach a tool by adding a `ports:` mapping
temporarily, and take it away again.

```bash
docker compose -f deploy/docker-compose.app.yml logs -f app
```

To stop it, `down`. To stop it *and erase the database*, `down -v` — that volume
is the dealerships' data.

### A Windows service

Build the package on any machine with the .NET SDK:

```powershell
& .\deploy\publish.ps1 -Runtime win-x64 -Output C:\DealerFOSS\app
```

The script builds the web interface first and **refuses to continue if it
produced nothing**. That check exists because the failure it prevents is silent:
the service starts, answers health, serves the API, and shows a blank page.

Copy the folder to the target machine, then, from an elevated PowerShell:

```powershell
& .\deploy\install-service.ps1 -Path C:\DealerFOSS\app -Connection "Server=.;Database=DealerFOSS_Host;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False" -KeyId 2026-08 -Key "<the base64 key>"
```

It registers the service to start automatically, sets it to restart on failure,
and writes the connection string and key into **the service's own registry entry**
rather than as machine-wide environment variables — so the key that decrypts every
dealership's connection string is not readable by every process on the box.

To remove it:

```powershell
& .\deploy\install-service.ps1 -Uninstall
```

That leaves the databases and the published folder alone. Delete them yourself if
you meant to.

### Either way

```powershell
Invoke-RestMethod http://localhost:8080/health/ready
```

`/health/live` says the process is up. `/health/ready` says it can reach what it
needs. An orchestrator should watch the first and a load balancer the second.

> **Do not point a Production installation at a database that was set up in
> Development.** Development stores connection strings unencrypted, and the real
> protector correctly refuses to read them — every request answers 500 with *"This
> value was not written by this protector"* in the log. There is no upgrade path;
> set the dealership up again.

---

## 3. Setting up a dealership

Sign in at `/admin`, then **Dealerships → Set up a dealership**.

You are asked for six things:

| Field | Notes |
|---|---|
| Dealership name | What appears on their paperwork |
| Short name | Lowercase letters, digits, hyphens. **Their staff type this to sign in, and it cannot be changed afterwards** |
| First location | They add any others themselves |
| Location code | Short, e.g. `MAIN` — appears on stock numbers and job sheets |
| Manager's name and email | The one person who exists on day one |

Pressing **Set it up** does all of this in one go:

1. creates and migrates their database,
2. writes the dealership, its legal entity, and its first location,
3. seeds the chart of accounts a sale and a service invoice need,
4. **opens the books for the current month**,
5. creates one manager who holds everything, with no password.

### The code you are given

You get a one-time enrolment code, shown once. **Read it out to the manager.**
They go to the sign-in page, choose *set your password*, and use it. Nobody at
your end ever knows or chooses a dealership password.

If it goes astray before they use it, the manager can be issued a new one from
that dealership's own **People** screen — which needs somebody who can already
sign in there, so in practice: do not lose it before they use it. If everybody at
the dealership is locked out, that is a support-access job (§4).

### What the manager does next

They add their own staff from **People**, each with their own enrolment code, and
open any further locations. You are not involved.

---

## 4. Taking a dealership out of service

**Dealerships → Suspend.** Everyone there is signed out immediately and cannot
work until you resume it. The console asks first, because it is a real outage for
real people.

Suspension is reversible and destroys nothing. There is deliberately no way to
delete a dealership from the console.

---

## 5. Helping a dealership

You cannot read a dealership's records by signing in as an operator — there is no
such path, and that is structural rather than a permission you happen not to
hold.

When they ask for help, open a **support visit**: a written reason, at most an
hour, read-only. It appears in **the dealership's own audit trail**, naming you
and your reason, because visibility that exists only in the vendor's console is
not visibility. Ending the visit stops the session on the very next request.

---

## 6. Backups

`deploy/backup.ps1` writes one `.bak` per database — the host catalog and every
dealership — plus a manifest with a SHA-256 of each.

```bash
& .\deploy\backup.ps1 -HostConnection "<host catalog connection>" -Destination "<folder>"
```

Restoring is `deploy/restore.ps1 -Verify`, which puts everything back **alongside**
the originals under a `Restored_` prefix and then runs the full end-to-end check
against the restored copy. A restore that overwrote the original would prove
nothing.

> **A `.bak` holds every customer record in plain form.** Where these files go and
> whether they are encrypted is a decision somebody has to make on purpose;
> nothing here makes it for you. See [Governance](08-Governance-and-Standards.md).

**A restored catalog still names the original databases**, because each
dealership's connection string is encrypted inside it. `restore.ps1` calls the
application's `--repoint-tenants` verb to fix that. If you ever restore by hand,
you must do the same, or the "restored" installation will quietly read and write
the live data — worse than a restore that plainly failed.

---

## 7. When something is wrong

| Symptom | Almost certainly |
|---|---|
| **A dealership's first sale is refused** and mentions the books | Their books were never opened. Provisioning does this; a hand-created tenant will not have |
| **Everything returns 400** with "provide the X-Tenant header" | Something is calling the API without naming a dealership. Documents opened as a plain link do this — they must be fetched |
| **A dealership returns 404** | Unknown short name, or the dealership is suspended. The two look identical on purpose |
| **Sign-in works, then everything is 403** | A role that demands a second factor, and the person has not set one up. They can reach enrolment and nothing else |
| **The application will not start**, naming a secret | `Secrets__CurrentKeyId` or the matching key is missing. It refuses rather than falling back to no encryption |
| **Every request answers 500** after moving an installation to Production | The catalog was written in Development, where connection strings are stored unencrypted. The log says *"This value was not written by this protector"*. There is no upgrade path: set the dealership up again on a Production installation |
| **The site is a blank page**, but `/health/live` answers | The package was built without the frontend. `deploy/publish.ps1` refuses to produce one, so this means a hand-rolled publish. Rebuild with the script |
| **A white page after an upgrade** | A cached `index.html` pointing at assets that no longer exist. It is served `no-store`, so this means a proxy or CDN in front is overriding that |
| **A dealership is unreachable after a restore** | `--repoint-tenants` was not run — see §5 |

### Checking the whole installation

```bash
& .\deploy\verify-e2e.ps1 -HostConnection "<host catalog connection>"
```

Expects `PASS`. It drives a real host end to end: tenant isolation, sign-in,
permissions, a deal, a service invoice, parts at cost, closing a month, import
and export. It is the fastest honest answer to "is this installation healthy".

---

## 8. What this system will not do for you

Named so you plan around them rather than discovering them:

- **Nothing is emailed.** Enrolment codes are read out, not sent.
- **There is no password reset.** A new starter sets their own with a code; there
  is no flow yet for somebody who forgets theirs later.
- **A second operator cannot be created from the console** — only the seeded one
  exists, and there are no recovery codes for it.
- **Backups are not scheduled, copied off-host, or encrypted** by anything here.
- **There is no live sync with another DMS.** Records import and export as files.
