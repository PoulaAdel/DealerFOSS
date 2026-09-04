# Contributing to DealerFOSS

Thank you for helping build an open-source Dealer Management System.

**Start at [docs/ONBOARDING.md](../docs/ONBOARDING.md).** It is the one door: it
reads the code before the documents, gets the application running in about ten
minutes, and lists the traps that have already caught somebody. This file assumes
you have been through it and covers what is different about *contributing* rather
than *understanding*.

This file used to open with its own reading list, which made it a third entry
point quietly disagreeing with the other two. There is now one.

## Looking for something to work on

**[docs/FIRST-TASKS.md](../docs/FIRST-TASKS.md)** is a curated list of real,
scoped work — each item says where it lives, why it is a reasonable first task,
and what "done" looks like. It is derived from named gaps in
[STATUS.md](../docs/implementation/STATUS.md), not invented to fill a page.

If you want the wider picture of what is deliberately *not* built and why, the
scope register in
[docs/11 §12](../docs/11-Franchise-and-External-Scope.md) labels every open item
by what actually blocks it — a manufacturer relationship, a commercial contract,
a market decision, or engineering time. Only the last kind is yours to take.

## The loop

1. Take a task. If it is not on the list, open an issue first and say what you
   intend — it is cheaper to disagree about scope in an issue than in a diff.
2. Read the **one** document that owns the topic
   ([00-Workbook](../docs/00-Workbook.md) says which).
3. **Write the failing test first.**
4. Implement until it passes.
5. **Break your test and watch it fail**, then restore it. See below.
6. Verify (all of the gates that apply — see the next section).
7. Update [STATUS.md](../docs/implementation/STATUS.md), pairing each claim with
   the command that proves it.

## The gates

Everything must pass locally before you push. CI runs afterwards and only
confirms; it is not where you find out.

```bash
dotnet build DealerFOSS.slnx -c Release
```

```bash
dotnet test DealerFOSS.slnx -c Release
```

Tenant isolation end to end — it prints `PASS` or you are not done:

```bash
& .\deploy\verify-e2e.ps1
```

**If your change touches `frontend/`, all four of these too.** `npm audit` is the
counterpart of `NuGetAudit`: a high advisory fails the build.

```bash
npm audit --audit-level=high && npm run typecheck && npm test && npm run build
```

Node does not have to be installed — `deploy/docker-compose.yml` has a `node`
profile with the repository mounted. [Local
Development](../docs/LOCAL-DEVELOPMENT.md) has the details, including both SQL
paths and the bind-mount trap that makes the container look unsupported when it
is not.

Copy `src/App/appsettings.Development.json.example` to
`src/App/appsettings.Development.json` and fill in local values. **Never commit
secrets** — that file is git-ignored.

## The habit that matters most

**Prove your test fails.** Break the thing it guards, watch it go red, restore
it. A test that has never failed is decoration rather than evidence, and this
project has caught its own vacuous tests this way more than once — including one
that passed against deliberately broken code because an index happened to supply
the ordering the test was checking.

The procedure for the architecture rules is written up in
[`tests/Architecture/README.md`](../tests/Architecture/README.md); apply the same
thinking to anything you add.

## Definition of done

- Every new file opens with the **four-part header** — Copyright /
  SPDX-License-Identifier / `Overview` / `Usage:` / `Coding Instructions:`. The
  template is in [docs/08 §5](../docs/08-Governance-and-Standards.md). Write the
  `Coding Instructions` for the person who will get it wrong.
- Domain code has no ASP.NET, EF or connector dependency; `tests/Architecture`
  stays green.
- Expected business failures return a typed `Result`; only bugs throw.
- Every business write emits an audit event; money carries a currency; instants
  are UTC.
- **Every user-visible string goes through the catalogue**, and English is the
  schema — a key added to `en.ts` and missing from the other five fails
  `npm run typecheck` (ADR-019).
- A screen renders loading, empty, denied, failed and retry — not just the happy
  path (ADR-020).
- Tests accompany the change. Coverage does not substitute for named
  critical-case tests.
- Public contract or database migration changes get explicit review.

## Architecture changes

Proposed as an ADR in [docs/adr/](../docs/adr/README.md), never as a silent
change in a pull request. ADRs are immutable: a material change writes a new one
that supersedes the old, and a stale *fact* inside an accepted ADR gets a dated
Correction section rather than a rewrite.

## Provenance

The contributor sign-off policy (DCO or CLA) is finalised before external code is
accepted ([docs/08 §2](../docs/08-Governance-and-Standards.md)). Until then, keep
contributions your own original work and free of incompatible-license code.

## Commits and branches

**Be aware that the repository does not currently run the way this section
describes.** Today it is a single maintainer committing directly to `main` with
pushes done by hand, and local verification as the only gate — recorded honestly
in [docs/08 §9](../docs/08-Governance-and-Standards.md).

The policy takes effect the moment a second person can review: short-lived
branches off `main`, one pull request each, stating user and operational impact
and flagging any schema, public-contract or security effect. If you are that
second person, say so in your first issue and it starts applying to both of us.
