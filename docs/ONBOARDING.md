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
| 1 | [`src/Host/Program.cs`](../src/Host/Program.cs) | how the application is composed |
| 2 | [`src/Host/Tenancy/TenantMiddleware.cs`](../src/Host/Tenancy/TenantMiddleware.cs) | how a request finds its dealer organization's database |
| 3 | [`src/Host/Tenancy/CurrentUserMiddleware.cs`](../src/Host/Tenancy/CurrentUserMiddleware.cs) | how the caller is identified (provisional today — see §4) |
| 4 | [`src/Modules/Organization/OrganizationEndpoints.cs`](../src/Modules/Organization/OrganizationEndpoints.cs) | how thin the HTTP layer is, and how errors map to Problem Details |
| 5 | [`src/Modules/Organization/OrganizationService.cs`](../src/Modules/Organization/OrganizationService.cs) | where business decisions and authorization actually live |
| 6 | [`src/Modules/Identity/Contracts/IAccessDirectory.cs`](../src/Modules/Identity/Contracts/IAccessDirectory.cs) | how one module asks another a question without touching its data |
| 7 | [`src/Modules/Identity/AccessService.cs`](../src/Modules/Identity/AccessService.cs) | how "deny by default" is implemented, and where denials are audited |
| 8 | [`src/Modules/Organization/Data/OrganizationDbContext.cs`](../src/Modules/Organization/Data/OrganizationDbContext.cs) | schema ownership, audit stamping, optimistic concurrency |
| 9 | [`src/Core/Result.cs`](../src/Core/Result.cs) | the return type you will use in nearly everything |

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
dotnet run --project src/Host
```

### Feel the tenancy model in one minute

Development seeds two dealer organizations — `northgroup` (two rooftops) and
`citymotors` (one) — and three users in each:

| User id | Scope | Sees |
|---|---|---|
| `11111111-1111-1111-1111-111111111111` | organization-wide | every rooftop |
| `22222222-2222-2222-2222-222222222222` | one rooftop | `NAG-01` only |
| `33333333-3333-3333-3333-333333333333` | no assignment | nothing — `403` |

```powershell
Invoke-RestMethod http://localhost:5080/api/v1/organization -Headers @{ "X-Tenant"="northgroup"; "X-User"="22222222-2222-2222-2222-222222222222" }
```

Change the user id and watch the response change. Change `X-Tenant` and watch the
data change entirely — that is a different database. This demonstrates the model
faster than any diagram.

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
6. Commit to a `feature/*` branch. See [CONTRIBUTING](../.github/CONTRIBUTING.md).

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

---

## 4. Know what is still soft

Calibrate your confidence — these are current, honest limitations:

- **Only two modules exist** (Organization, Identity) out of roughly thirteen
  planned. The module template is not battle-tested; expect it to bend when the
  first large capability lands. Do not treat Organization as canonical.
- **There are no unit tests yet** — only architecture and integration. Domain
  rules are reached incidentally rather than asserted directly.
- **CI has never executed** (no remote configured). The workflow is a claim.
- **Authentication is provisional.** The `X-User` header is refused outside
  Development. Authorization is real; identity is not yet.
- **Backup and restore have never been rehearsed.**

`STATUS.md` is the live version of this list. If it disagrees with this section,
`STATUS.md` is right and this section is stale.

---

## If you remember three things

1. **Read the request trace, not the documents.** Nine files teach you the system.
2. **Evidence over claims.** If you say it works, name the command.
3. **Break your test before you trust it.**
