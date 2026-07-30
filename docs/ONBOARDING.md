# Onboarding — how to read, run, and change this project

This is a **path through** the material, not more specification. Every rule it
mentions is owned by another document; this one only tells you what to look at
first and in what order. Where it disagrees with an owning document, the owning
document wins.

Budget about 30 minutes for the reading, 10 for getting it running.

---

## 1. Read (30 minutes)

**Do not start with the documents.** There are 45 of them and they are written as
a reference. Reading them cold is the slowest route in.

### Step 1 — the shape (5 min)

- [`README.md`](../README.md) — the repository map: one line per directory.
- [`implementation/STATUS.md`](implementation/STATUS.md) — what is actually built,
  what is proven, and what is next. Every completed item names the command that
  proves it, so it can be trusted rather than assumed.

### Step 2 — trace one request through the code (20 min)

This is the step that teaches the architecture. Follow a single request —
`GET /api/v1/organization` — through nine small files, in order. About 800 lines
total.

| # | File | What it teaches |
|---|---|---|
| 1 | [`src/App/Program.cs`](../src/App/Program.cs) | how the application is composed |
| 2 | [`src/App/Tenancy/TenantMiddleware.cs`](../src/App/Tenancy/TenantMiddleware.cs) | how a request finds its dealer organization's database |
| 3 | [`src/App/Tenancy/CurrentUserMiddleware.cs`](../src/App/Tenancy/CurrentUserMiddleware.cs) | how the caller is identified from their session, on every request |
| 4 | [`src/App/Organization/OrganizationEndpoints.cs`](../src/App/Organization/OrganizationEndpoints.cs) | how thin the HTTP layer is, and how errors map to Problem Details |
| 5 | [`src/App/Organization/OrganizationService.cs`](../src/App/Organization/OrganizationService.cs) | where business decisions and authorization actually live |
| 6 | [`src/Identity/IAccessDirectory.cs`](../src/Identity/IAccessDirectory.cs) | how one capability asks another a question without touching its data |
| 7 | [`src/Identity/AccessService.cs`](../src/Identity/AccessService.cs) | how "deny by default" is implemented, and where denials are audited |
| 8 | [`src/App/Data/TenantDb.cs`](../src/App/Data/TenantDb.cs) | audit stamping, optimistic concurrency, and the append-only rule |
| 9 | [`src/Core/Result.cs`](../src/Core/Result.cs) | the return type you will use in nearly everything |

Then open [`src/App/Customers/`](../src/App/Customers/) and read the folder as a
whole — six files, one capability. That is the shape every new capability takes.

### Step 3 — the why (5 min, then on demand)

Now read [02 — Architecture & Decisions](02-Architecture-and-Decisions.md) and
[03 — Project Structure](03-Project-Structure.md). Having seen the code, they
answer questions you have already formed.

Read everything else **when you are about to touch it**.
[00 — Workbook](00-Workbook.md) tells you which document owns which subject.

---

## 2. Run it (10 minutes)

Prerequisites and the verified local setup live in [`CLAUDE.md`](../CLAUDE.md) —
read it before fighting your environment, because the obvious choice is not
always the working one on a given host.

```bash
sqllocaldb start MSSQLLocalDB
```

```bash
dotnet tool restore && dotnet build OpenDealer360.slnx -c Release
```

```bash
dotnet test OpenDealer360.slnx -c Release
```

```bash
dotnet run --project src/App
```

### Feel the tenancy model in one minute

Development seeds two dealer organizations — `northgroup` (two rooftops) and
`citymotors` (one) — and three accounts in each, all sharing the password
`Dev@Pass1!`:

| Sign in as | Scope | Sees |
|---|---|---|
| `gm@dev.local` | organization-wide, every permission | every rooftop and every lot |
| `advisor@dev.local` | one rooftop, read-only | `NAG-01` only; cannot move stock |
| `nobody@dev.local` | no assignment | nothing — `403` |

Sign in, keep the session cookie, then call an endpoint:

```powershell
$s = $null; Invoke-RestMethod http://localhost:5080/api/v1/auth/login -Method Post -Body '{"email":"advisor@dev.local","password":"Dev@Pass1!"}' -ContentType application/json -Headers @{ "X-Tenant"="northgroup" } -SessionVariable s; Invoke-RestMethod http://localhost:5080/api/v1/inventory -Headers @{ "X-Tenant"="northgroup" } -WebSession $s
```

Sign in as `gm@dev.local` instead and the same call returns both lots. Change
`X-Tenant` to `citymotors` and the data changes entirely — that is a different
database. This demonstrates the model faster than any diagram.

The whole thing, asserted end to end:

```bash
& .\deploy\verify-e2e.ps1
```

---

## 3. Change something

### The loop

1. Take the **Next milestone** from [`implementation/STATUS.md`](implementation/STATUS.md).
   It states what is included *and what is explicitly excluded* — respect both.
2. Read the one document that owns that topic.
3. **Write the failing test first**, then implement until it passes.
4. Verify: build → test → `verify-e2e.ps1`.
5. Update `STATUS.md`, pairing each claim with the command that proves it.
6. Commit — see [CONTRIBUTING](../.github/CONTRIBUTING.md) and, for the branching
   model this repository actually uses, [`CLAUDE.md`](../CLAUDE.md).

### The habit that matters most

**Prove your test fails.** Break the thing it guards, watch it go red, restore it.
A test that has never failed is decoration, not evidence. The procedure for the
architecture rules is written up in
[`tests/Architecture/README.md`](../tests/Architecture/README.md); apply the same
thinking to any test you add.

### The repository will argue with you

Warnings are errors. Vulnerable packages fail the build. Architecture tests fail
on a forbidden reference. Analyzers object to naming.

**Fix the cause, not the rule.** Every suppression in `.editorconfig` carries a
comment explaining why it is legitimate — that is the standard for adding another.

### Traps that have already caught someone

| Trap | What happens |
|---|---|
| `dotnet test --no-build` after a **failed** build | Runs stale binaries and can report a false pass. Always confirm the build succeeded first. |
| Reaching for the SQL Server container | It does not run on every host. `CLAUDE.md` records the working option. |
| `identity` in hand-written SQL | Reserved T-SQL keyword — bracket it as `[identity]`. EF quotes it automatically. |
| Config in `WebApplicationFactory` | `Program.cs` reads configuration before in-memory sources are applied; use environment variables. |
| Adding a strongly-typed id | Needs a JSON converter, or it serializes as `{"value":"…"}` and breaks route binding. |
| Editing anything under `Migrations/` | Generated code, excluded from analysis. Create a new migration instead. |
| Adding a `using` for another capability | Compiles fine, then fails `FeatureBoundaryTests`. Go through that capability's `I<Feature>` interface instead. |
| Making a type in `src/Identity/` public | Fails `BoundaryTests`, which asserts Identity's exported type list. Widening it is a security decision. |

---

## 4. Know what is still soft

Calibrate your confidence — these are current, honest limitations:

- **Four capabilities exist** (Organization, Identity, Customers, Vehicles +
  Inventory) out of roughly thirteen planned. The shape in
  [ADR-017](adr/0017-three-projects-flat-features.md) is one week old; expect it
  to bend when the first large capability lands.
- **The features inside `App` are held apart by tests, not by the compiler.** That
  is deliberate, and it means a cross-feature `using` compiles and fails later.
- **CI has never executed** (no remote configured). The workflow is a claim.
- **Second-factor authentication and federation do not exist.** Passwords and
  sessions are real; MFA and OIDC are the next milestone.
- **No explicit anti-forgery token on writes.** The session cookie is
  `SameSite=Strict`, which is the current defence.
- **Backup and restore have never been rehearsed.**

`STATUS.md` is the live version of this list. If it disagrees with this section,
`STATUS.md` is right and this section is stale.

---

## If you remember three things

1. **Read the request trace, not the documents.** Nine files teach you the system.
2. **Evidence over claims.** If you say it works, name the command.
3. **Break your test before you trust it.**
