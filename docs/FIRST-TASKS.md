# First tasks

Real, scoped work with nothing blocking it but engineering time. Every item here
was **checked against the code on 2026-08-15** rather than copied from a wish
list — if one of them turns out to be already built, that is a bug in this file
and worth reporting on its own.

Read [ONBOARDING](ONBOARDING.md) first. Then
[CONTRIBUTING](../.github/CONTRIBUTING.md) for the gates your change has to pass.

**What is deliberately not here.** Anything blocked on a manufacturer
relationship, a commercial contract, or the market decision nobody has taken yet
— roughly two thirds of the backlog. The scope register in
[doc 11 §12](11-Franchise-and-External-Scope.md) labels each open item by what
actually blocks it; only rows marked `Build` are available to anyone.

---

## Start here — an afternoon each

### 1. Decode a VIN

**Where:** `src/App/Vehicles/`, alongside `ISafetyRecalls`.

The same road-safety regulator that answers our recall lookup also decodes a VIN
into year, make, model, body style, engine and plant — free, unauthenticated, and
verified live on 2026-08-14:

```
https://vpic.nhtsa.dot.gov/api/vehicles/DecodeVinValues/{vin}?format=json
```

**Why it is a good first task.** The whole pattern already exists next door and
you copy it: a narrow interface, a typed `HttpClient` adapter, a `Result` with a
distinct `Unavailable` error, and a screen band that runs on request rather than
on open. You will touch a capability, an endpoint, a contract, a test and a
screen — a complete tour of how this codebase is put together — without designing
anything new.

**Done looks like:** an endpoint that turns a VIN into a decode, refusing
gracefully when the service is unreachable, distinguishable from "this VIN
decoded to nothing". A test proving the two are not conflated. Ideally the
vehicle-entry screen offers to fill the fields in and a person can still overrule
it — the regulator is occasionally wrong about trim.

**Watch out for:** the VIN exceptions this product deliberately allows
(`Vin.cs`). A trailer with a frame-plate number must not become an error.

---

### 2. Let somebody see and end their own sessions

**Where:** `src/Identity/` (the rows already exist), `src/App/AuthEndpoints.cs`,
and a band on the security screen.

Every session row already carries a device summary, when it was issued, when it
was last seen, and how it was authenticated. **Nothing reads them back.** Doc 06
§2 says a user can view and revoke their sessions; that sentence is marked
*specified, not built* because of this gap.

**Why it is a good first task.** No new storage and no new concepts — the data is
sitting there. It teaches you Identity's sealed boundary, because you will have
to decide what the contract exposes and justify it: `BoundaryTests` asserts the
exported type list, and widening it is a security decision that has to be argued
in a comment.

**Done looks like:** a person sees their own sessions and only their own, can end
one, and the ended session stops working on its *next* request rather than
whenever a token would have expired. A test proving somebody cannot end somebody
else's.

**Watch out for:** the current session should be marked as such. An interface
that lets you sign yourself out without realising it is a small cruelty.

---

### 3. Cancel a running import

**Where:** `src/App/DataMigration/`.

An import is the one piece of background work in the product. It reports progress
and errors, and there is no way to stop one — named as absent in doc 06 §6 and in
that folder's own README.

**Why it is a good first task.** It is a small state-machine change on a worker
that already claims its jobs with a conditional update, so the concurrency
thinking is done and visible for you to follow.

**Done looks like:** a requested cancellation is honoured between rows rather
than mid-row, the job ends in a state that says it was cancelled rather than
failed, and everything already imported stays imported. A test proving a
cancelled run leaves the database consistent.

**Watch out for:** two application instances share one database. Cancellation is
a flag the worker reads, not a method somebody calls on an object.

---

## A bit more to it — a few days

### 4. Prune expired passkey challenges

**Where:** `src/Identity/PasskeyDirectory.cs` and its table.

A challenge is single-use and short-lived. Spent and expired ones are never
deleted, so the table grows forever. Named as not built when passkeys landed.

**Why it is interesting.** There is no scheduler (doc 02 §4 lists Quartz as
*selected, not built*), so the honest first version prunes opportunistically —
when a new challenge is issued, say — and that is a legitimate answer rather than
a workaround. Doing it well means thinking about what happens when two instances
prune at once.

**Done looks like:** the table stops growing without bound, and a still-valid
challenge is never removed. A test that would fail if the window were wrong.

---

### 5. Surface the concurrency token as an ETag

**Where:** `src/App/ProblemResults.cs`, the endpoints, `AuditableEntity`.

Every business row already carries an optimistic concurrency stamp and
`SaveChanges` enforces it — so a conflict is **detected**, but only after the
caller has already sent their change. Doc 06 §6 specifies `ETag` and `409`;
neither exists on the HTTP surface.

**Why it is interesting.** It is the difference between "your change was
rejected" and "somebody else edited this while you were typing", which is the
same fact told two very different ways. It touches the whole API surface, so it
is a good way to learn where the seams are.

**Done looks like:** a mutable resource returns an `ETag`, a write with a stale
one gets `409` with a body that says what to do, and a write with no `ETag` at
all behaves exactly as it does today. Backwards compatibility is the constraint.

---

### 6. Create a second administrator

**Where:** `src/Identity/GlobalAdministrationService.cs`,
`src/App/Administration/AdminEndpoints.cs`.

There is exactly one way to get an administrator: seed one. There is no endpoint
that creates a second, and no recovery if the only one loses their phone —
clearing a row in the database is the current answer.

**Why it is interesting.** This is the most privileged account in the
installation, so every decision is a security decision: who may create one, what
proof is required, whether the new account starts with a second factor already
mandatory (it must), and what the dealership's audit trail should say about it —
which is nothing, because this is not their business.

**Done looks like:** an existing administrator can create another, the new one
can do nothing until it has enrolled a second factor, the act is in the control
plane's own append-only log, and no tenant database learns about it.

**Watch out for:** `FeatureBoundaryTests` fails the build if anything in the
control plane so much as references `ICurrentUser`. That is the wall keeping
whoever runs the servers out of the dealerships' records, and it is not
negotiable.

---

## Not code

### 7. Read a language you actually speak

**Where:** `frontend/src/shared/i18n/locales/`.

Six catalogues — English, Spanish, French, German, Russian, Arabic — written by
one person and one machine. They are structurally correct: a missing key fails
the type check, and the plural categories are real ones chosen by
`Intl.PluralRules`. Whether they sound like a **dealership** is a different
question, and it needs somebody who has worked in one.

The file headers say what to watch: French uses *demande* and *vente*, not
*piste* and *affaire*; Arabic needs Arabic punctuation, and a Latin comma is the
usual tell of an untranslated string.

**Done looks like:** corrections with a sentence saying why, in a pull request or
an issue. "This is what a French service advisor would actually say" is worth
more here than any amount of code review.

### 8. Audit a screen against WCAG 2.2 AA

**Where:** any screen, and [ADR-020](adr/0020-screen-shape-and-interface-standards.md).

That ADR is unusually honest about which of its standards are met, which are
partly met, and which are not. Pick a screen, work through it with a keyboard and
a screen reader, and report what you find against what the ADR claims.

**Done looks like:** either a correction to a screen, or a correction to the ADR.
Both are useful; the second may be more so, because a standards document that
overstates itself is worse than one that admits a gap.

---

## Before you pick something bigger

Two items are **claimed** and better left alone unless you talk to the maintainer
first: the tenant-aware background job context (the last unmet exit criterion not
blocked on an identity provider), and workflow triggers on data the product
already holds, which needs the first one plus a scheduler underneath it.

Three more are named in STATUS as deliberately unbuilt, for reasons worth reading
before you volunteer: **attestation verification** for passkeys, a **policy that
lets a passkey replace a password**, and **`[rpt]` analytical projections** —
which are premature, because no query is yet slow enough to need one.
