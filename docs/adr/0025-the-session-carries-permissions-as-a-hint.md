# ADR-0025 — The session carries the caller's permissions, and they are a hint

**Status:** Accepted · **Date:** 2026-09-18

## Context

Until now `/auth/me` answered `{ userId, mustEnrolSecondFactor }`. The browser
therefore had no idea what the signed-in person was allowed to do, and the
navigation showed every module to everybody. A technician saw **Books** and
**People** in the bar, clicked, and was told off by the server.

That was not an oversight. It follows directly from a rule this project holds
hard: **there is one copy of an authorization rule and it lives on the server.**
Every service asks `IAccessDirectory`; nothing else decides. Shipping a
permission table to the browser looks, at first glance, exactly like starting a
second copy — which is how the deal desk's transition table and the workshop's
status rules were nearly duplicated twice before, and why both now come down
from the server as `availableMoves`.

Per *record*, that pattern already solves the problem properly. There was no
equivalent for a *module*: nothing tells the browser whether to draw a link at
all. So the product had 33 permissions, sixteen destinations, and a navigation
bar that assumed everyone was a general manager.

The UI/UX audit of 2026-09-17 raised this as H5 and deliberately did **not**
fix it, because it touches authorization and that is not a decision to take
while tidying CSS.

## Decision

`/auth/me` also returns `permissions`: every permission the caller holds
**somewhere** — organization-wide, or on at least one rooftop — sorted, and
empty for anybody who still owes a second factor.

**It is a hint for drawing a screen. It is never a decision.**

Four things make that more than a promise in a comment:

1. **It is deliberately lossy.** The list carries no scope. Somebody who may
   read accounting at one rooftop out of four appears identically to somebody
   who may read it everywhere, because the only question it answers is "draw
   the link at all?". Anything needing to know *where* must call
   `GetAuthorizedScopeAsync`, which is the only thing that knows.
2. **Nothing on the server may branch on it.** It is computed for the response
   and thrown away. No service reads it.
3. **Every endpoint enforces exactly as before.** Nothing was removed, relaxed,
   or made conditional on the caller's list.
4. **A test forbids the obvious future mistake.**
   `SessionPermissionsTests.Permissions_are_a_hint_not_a_control` signs in as a
   technician, asserts their list does not contain `Accounting.Read`, and then
   calls the accounting endpoint and requires a **403**. The day somebody
   decides the browser already filters this and stops checking, that test goes
   red.

On the browser side the list is reached only through `holds(permission)` on the
session context, whose own documentation says the same thing, and the strings
live in one partial mirror (`shared/permissions.ts`) rather than being typed at
sixteen call sites.

### What is never hidden

- **The dashboard.** It is the landing screen, and it already withholds
  individual figures a reader may not see rather than refusing wholesale.
  Hiding it would leave somebody signed in with nowhere to be.
- **Two-step sign-in and passkeys.** A person's own credentials are not the
  dealership's business and need no permission.

A group whose every child is hidden hides itself, because "Accounting ▾"
opening onto an empty menu reads as broken rather than as absent.

## Alternatives rejected

**Leave it as it was.** Defensible, and it cost nothing to maintain: being
refused is an honest answer. Rejected because it is an honest answer to a
question the product should not have made the person ask. A technician learning
the shape of their own job by collecting refusals is a poor first week.

**Ask the server per destination.** Sixteen requests on every page load, or one
batch endpoint that is this one with extra ceremony.

**Compute it from roles in the browser.** This is the actual second copy, and
the reason the decision needed writing down: the browser would need the
role→permission mapping, and that mapping would then exist twice.

**Send the scope as well** (`{ permission → rooftops }`). More faithful, and
more tempting to misuse: a payload that says *where* invites a screen to filter
a list by it, and now the browser is deciding what data a person sees. The lossy
version cannot be misused that way because it does not carry the information.

## Consequences

**Good.** The navigation reflects the job. A technician sees four destinations
instead of six, and the two they could never use are simply absent. The
mechanism generalises: any screen-level action can ask `holds` instead of
guessing.

**Cost.** One method on `IAccessDirectory` — a surface change to the sealed
Identity contract, which CLAUDE.md requires be stated out loud rather than done
quietly. It is a method on an already-public interface, not a newly public
type, and it decides nothing; but it is still the first thing on that interface
that hands out information instead of a verdict, and it carries the longest
remarks on the interface for that reason.

**Cost.** A permission renamed on the server must be renamed in
`shared/permissions.ts` in the same commit, exactly like `contracts.ts`. A
string that stops matching fails safe — the link is not drawn — which is a
missing link rather than an open door, and a missing link is the failure
somebody notices.

**The standing risk.** Somebody, one day, will reason that the browser already
filters this. The test named above is the guard, and it is named so that it
cannot be deleted without reading what it is for.

## Validation / review trigger

Revisit if a screen ever genuinely needs to know *where* a permission is held.
The answer is not to enrich this payload — it is for that screen to ask the
server, the way record-level actions already do.
