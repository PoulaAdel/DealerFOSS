# Security testing — what was attacked, and what happened

**Run on 2026-09-24 against `main`.** This is the record of an adversarial pass
over the application: a list of attacks that were tried, with the outcome of
each. It is deliberately not a list of controls that exist. A control list is
written by reading the code, and reading the code is how the one defect below
survived nine days of review by the people who wrote it.

> **This is not the independent penetration test.** [`docs/06`](../06-Security-and-API.md)
> §production-readiness requires one, conducted by somebody outside this
> project, and that requirement is untouched by this document. What follows is
> the work that should be done *before* paying for that test, so the findings
> that come back are the ones nobody here could have found.

Everything below is now a test in the suite, so each attack is re-run on every
`dotnet test` rather than having been tried once. The three new files are
[`RecordAuthorizationTests`](../../tests/Integration/RecordAuthorizationTests.cs),
[`CredentialSurfaceTests`](../../tests/Integration/CredentialSurfaceTests.cs) and
[`OutputEncodingTests`](../../tests/Integration/OutputEncodingTests.cs).

---

## The one finding

**A bill could be confirmed to exist at a lot the caller cannot see.** Found by
rehearsal, not by reading. Fixed in the same change.

`GET /api/v1/receivables/for/{source}/{reference}` looks a receivable up by what
it is for rather than by its own id. It answered:

| The reference names | Answer |
|---|---|
| nothing this installation has billed | `204 No Content` |
| a bill at a lot the caller may see | `200 OK` with the bill |
| a bill at a lot the caller may **not** see | `403 Forbidden` |

The third row is the defect. The difference between `204` and `403` is itself
the disclosure: any signed-in caller — including one holding no assignment at
all, because the permission was checked against the *found row's* rooftop rather
than up front — could hand this route a deal or repair-order id and learn from
the status code alone whether it had been invoiced anywhere in the group. That
is the fact rooftop scope exists to withhold.

**Severity: low, and it is worth saying why rather than leaving it to look
worse or better than it is.** The reference is a Guid, so the route cannot be
walked — an attacker must already hold an id they are not entitled to read. That
is not hypothetical (an advisor moved between lots keeps old bookmarks,
printouts and exported files), but it is a confirmation oracle rather than a way
in, and it discloses one bit: *this was billed*.

**Fixed** in `ReceivableService.FindAsync`: an out-of-scope hit now answers
exactly as a reference that does not exist. The reach is still recorded —
`IsAuthorizedAsync` writes a `Denied` row to the dealership's own audit trail
before the answer is composed — so only the *answer* is made identical, and the
dealership can still see that somebody tried.

**Why it was there when nothing else was.** The rule is held everywhere else,
and is written down in the code where it is held: `OrganizationService`,
`AppointmentService` and `AccountingService` each carry a comment saying unknown
and unauthorized must answer alike. `FindAsync` is the only read in the system
that takes a *reference* rather than a record id, so it was the only one where
"not found" was already a legitimate business answer — a deal still being worked
has no receivable yet. Having a real `null` to return is what made a second,
different refusal look reasonable.

---

## What was attacked, and what held

### Authorization at the record level

The existing `RooftopAuthorizationTests` proves the rule in the Organization
capability and says in its own header: *"Cover both routes when you extend
them."* Nothing had extended them. Every other by-id route was held only by the
code being written correctly, which is not evidence.

The attacker is the seeded advisor at NAG-01 — a real user, a real session, and
every read permission their role carries. The only thing they lack is the
rooftop. Records were built at NAG-02 through the ordinary API.

| Attack | Result |
|---|---|
| Read a car in stock at another lot by id | **Refused** |
| Read an enquiry, a deal, a workshop job, a booking, a bill, a ledger entry at another lot by id | **Refused** |
| Read the other lot itself by id | **Refused** |
| Print the vehicle order for another lot's deal | **Refused**, and the refusal does not print the customer |
| Print the service invoice for another lot's job | **Refused**, same |
| Widen a list by naming another lot in `?rooftopId=` — on stock, enquiries, deals, jobs, bills, bookings and the journal | **Refused** on all seven; a filter is not a grant |
| Do all of the above holding no assignment at all | **Refused**; no scope is never read as no filter |

Nothing was found. The delegation is the part worth naming: the printed
documents hold because `DocumentService` reads its record *through* the
capability contract rather than from the database, so the scope check it
inherits cannot be forgotten separately.

### Enumeration — telling "not yours" apart from "not there"

Each by-id route above was asked twice: once with the id of a record really at
the other lot, and once with a Guid nobody has ever issued. **The status and the
error code must match.** Comparing only the status would miss a route that
answered `403` to both while naming a different reason.

This is what found the receivables defect. Every other route matched on both.

### Mass assignment — naming another lot in a body

A rooftop is not in the path of a write; it is a value in the body, so it must
be checked at the value. A salesperson holding `Inventory.Manage` **at NAG-01**
posted a new stock unit naming NAG-02.

**Refused.** Checked in `InventoryService` against the rooftop in the body, not
against the route.

### The records-package endpoints

`POST /api/v1/migration/packages/{rooftopId}` landed 2026-09-21, writes across
five capabilities in bulk, and was nine days younger than any review of it.
Export permission was already covered; the import half was not.

| Attack | Result |
|---|---|
| Apply a package as a one-lot advisor, into another lot | **Refused** |
| Apply a package as a one-lot advisor, into their **own** lot | **Refused** — group-wide or nothing |
| Apply a package containing ids that already exist | Already covered: answers `AlreadyPresent`, does not overwrite |

The own-lot refusal is the one worth keeping. Customers and vehicles in a
package are not scoped to a lot at all, so a one-lot manager who could apply a
package could rewrite the group's customer list.

Not found, but noted: an importer holding the group-wide right can learn that a
given record id already exists somewhere, because an id that is present answers
`AlreadyPresent`. That caller may already write anywhere in the group, so it
discloses nothing they could not obtain directly.

### Rate limiting beyond the password

The limiter's *behaviour* is proven by `verify-e2e.ps1` against a real host over
a real socket — the in-process suite makes hundreds of sign-ins in seconds down
one connection, so `HostFixture` raises the allowance to keep it out of the way.
This run: **40 wrong passwords, 29 refused as too many.**

That leaves a gap behaviour tests cannot close: a *new* credential route added
next year with no `.RequireRateLimiting` on it would break nothing and be
noticed by nobody. So the route table is now read out of the running host and
asserted route by route — twelve credential routes carry the policy, the policy
name is one the limiter was actually configured with, and the limiter has not
crept onto the business API, where the volume that matters is a busy dealership
working rather than somebody guessing.

The sweep that catches what nobody thought of is the second half: **every** route
under `/api/v1/auth` and `/api/v1/admin` is treated as a credential route until
it is exempted in writing. It immediately named five that were outside the
check — the operator console's tenant and support-access routes, and forgetting
a passkey. All five are correctly unlimited (they need an administrator session
that has already cleared a limited password and a limited second factor, or a
session holding the credential being removed), and each now carries its reason
beside it rather than being silently absent.

### Injection

**Measured, not assumed: there is no raw SQL anywhere in `src`.** No
`FromSql`, no `ExecuteSql`, no `SqlQuery`, no `CommandText` — every query goes
through EF Core, which parameterises. The only hand-written SQL in the
repository is inside tests, reading audit rows back.

### Stored markup in the printed documents

The one surface that builds HTML out of dealership data. Everything else this
application returns is JSON. A customer was created whose surname was
`<script>alert('x')</script> & "Sons"`, and a repair order whose complaint —
free text typed at a service counter, the likeliest field in the system to
contain whatever somebody pasted — carried the same.

**Both printed as text.** `DocumentHtml.Text()` encodes every value, and the
discipline is held at every interpolation site. The test asserts the name
*survives encoded* rather than merely that no tag appears: a document that
silently dropped the customer's name would pass the weaker check and be a
different bug. The page is also served under `script-src 'self'`, which is the
second line if a future field is ever interpolated without the encoder.

### Cookies, headers and what a browser is told

Re-checked rather than re-tested, because `AuthenticationTests` and
`SecurityHardeningTests` already cover them: session cookie `HttpOnly`,
`SameSite=Strict`, `Secure` outside Development, and the anti-forgery cookie
identical except for `HttpOnly`, built by one shared function so the pair cannot
drift. Headers are set by middleware registered first in the pipeline, so a
refused response carries them too — asserted on a `401`, which is the half that
breaks when the middleware is registered too late.

### Forwarded headers

`X-Forwarded-For` is opt-in, and the defaults are cleared rather than extended:
`KnownIPNetworks` and `KnownProxies` are emptied, only the operator's listed
proxies are added, and `ForwardLimit` is 1. An unconditionally trusted
`X-Forwarded-For` would let one caller step around the sign-in limiter at will,
which is worse than a limiter that is merely shared. Nothing found.

---

## What was not tested, and why

Saying this plainly is the point of the document. Each of these is a real gap in
the evidence, not a gap somebody decided did not matter.

- **Timing as a side channel.** The sign-in path answers an unknown email and a
  wrong password identically by *content*, which is asserted. Whether they take
  the same *time* is not, and cannot honestly be measured in an in-process test
  host sharing a connection with hundreds of other tests. This needs a real host,
  a real socket and a statistical method, and belongs with the independent test.

- **TLS itself.** Both shipped packages serve plain HTTP and expect a reverse
  proxy to terminate TLS, which `deploy/README.md` states as a requirement
  rather than a suggestion. So there is no cipher suite, certificate chain or
  protocol version here to test — that posture belongs to the operator's proxy.
  The application's part is tested: the cookie is `Secure` outside Development
  and HSTS is sent only over a connection that is already secure. **The failure
  mode of deploying without a proxy is a visible one** — a `Secure` cookie is
  never sent back over HTTP, so sign-in stops working rather than quietly
  travelling in the clear.

- **Denial of service.** The CSV import caps at 20,000 rows, but the records
  package has no equivalent cap and no request-size limit beyond Kestrel's
  default 30 MB. Nothing was measured here. It is a real question for the
  performance milestone rather than this one, and it is recorded so that
  milestone inherits it rather than rediscovering it.

- **The frontend.** No `frontend/` file changed in this pass and no browser
  attack was attempted. ADR-025's rule — the browser is told things to decide
  what to draw, never to decide what is allowed — is a server-side property, and
  it is the server side that was tested.

- **OIDC federation.** Blocked on having an identity provider to test against,
  and left alone deliberately.

- **Everything an outsider would bring.** Dependency-chain analysis beyond
  `NuGetAudit` and `npm audit`, fuzzing, and the judgement of somebody who has
  not spent months forming opinions about this codebase.

---

## What changed in the code

Two lines of behaviour and a great deal of evidence.

- `ReceivableService.FindAsync` — an out-of-scope hit answers as not-found. The
  audit trail is unchanged.
- `IReceivables.FindAsync` — the contract now says what `null` means, because a
  caller reading only the interface would otherwise reasonably assume it meant
  "there is none".

**No type was made public.** Identity's sealed surface is untouched: every
attack above was mounted from outside, through the HTTP API, as a real caller
would. That is a stronger result than it would have been with a widened
boundary — nothing here needed one.
