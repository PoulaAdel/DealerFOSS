# ADR-018 — Account recovery is a set of pluggable proofs, not one flow

Date: 2026-08-07
Status: Accepted — **built 2026-08-09** for the two methods that need nothing
external (authenticator app, manager-issued code). Email and text message are
declared by the contract and report `false`, exactly as "a method that is not
configured is never offered" requires. Passkey/WebAuthn is deferred: it needs a
dependency with a licence review and an advisory history, which is a decision of
the same kind this document already made twice.

One thing the build settled that this document left open. **Recovery is a single
call** — email, proof and the new password together — rather than prove-then-
redeem. The intermediate ticket a two-step flow needs would be a second
credential that has to be stored, expired, transported and invalidated, and is
worth stealing. Nothing needs to survive between the two requests, so nothing
does.
Supersedes: —

## Context

A new starter sets their own password with a one-time enrolment code. Somebody who
forgets theirs afterwards has nowhere to go. That is the gap most likely to be hit
in a pilot's second week, and closing it means answering a question with no single
right answer: **how does somebody prove an account is theirs when they cannot sign
in to it?**

Every familiar answer routes through an outbound channel, and this system
deliberately has none. Nothing is emailed, no SMS is sent, and there is no provider
account anywhere in the deployment. That was a real decision — fewer moving parts,
nothing to leak, and an installation that works on a dealership's own server with
no internet connection.

Installations also differ in a way that matters here. A rural independent has no
mail server and never will. A group already on Microsoft 365 has one and expects
to use it. A workshop where the staff share two laptops is not the same problem as
one where everybody has a phone. Picking one method centrally means being wrong for
most of them.

## Decision

Recovery is **one contract with several methods**. A dealership enables the ones it
can support, and **a method that is not configured is never offered** — it does not
appear and then fail.

| Method | External dependency | Notes |
|---|---|---|
| Authenticator app | none | TOTP is already built and enrolled. A valid code, plus the email address. |
| Passkey / biometric | none | WebAuthn. The device performs the check and never transmits it; the server stores only a public key. Device-bound. |
| Email verification | an SMTP host or provider | Reverses the "this system sends nothing" default for this one purpose. Off unless configured. |
| WhatsApp or SMS | a paid gateway | Also needs a **verified** phone number on the staff record, a field that does not exist yet. Off unless configured. |
| Manager-issued code | none | The existing enrolment-code path. **The backstop**, and privileged. |

**The manager-issued code stays as the last resort**, and carries its own
permission rather than riding on `Staff.Manage`. Somebody who has lost both their
phone and their laptop otherwise has no way back in except a support call — and a
support call means whoever runs the servers reaching into a dealership's identity,
which [ADR-003](0003-database-per-dealer-organization.md) and the control-plane
split exist to prevent.

## Alternatives considered

- **One method, chosen centrally.** Rejected: whichever is chosen is unavailable to
  a meaningful share of installations. Email fails the offline independent; the
  authenticator fails anybody who never enrolled.
- **Email only, and require a mail server.** Rejected: it makes an outbound
  dependency mandatory for a system whose stated posture is that nothing leaves the
  machine, and it turns deliverability into an operator's ongoing job before they
  have anything else to run.
- **OAuth2 as a recovery method.** Rejected as a category error. Federated sign-in
  *removes* the password rather than recovering it, so it belongs to authentication.
  It stays parked until there is a real provider to test against — a fake one would
  prove nothing.
- **Support-desk reset.** Rejected: it is the one path this architecture is built to
  make impossible.

## Consequences

- **Starting a recovery must answer identically** whether or not the address or
  number is known. Anything else turns the endpoint into a way to enumerate staff.
- **Every method issues a single-use, short-lived proof, hashed at rest**, on the
  same terms as an enrolment code — one place credentials are created, not several.
- **Recovery is rate limited** with the other credential endpoints, per caller.
- **A manager-issued reset is visible in the product**, not only in the audit trail:
  a dealership must be able to see that somebody handed out access.
- Verified phone numbers become a real field on a staff record, with the
  verification that word implies. Until then the WhatsApp/SMS method has nothing to
  send to and stays off.
- Enabling a method is a deployment-level decision, so the configuration belongs
  with secret protection rather than in a dealership's own settings screen.

## Validation / review trigger

Revisit if federated sign-in lands and covers enough of the user base that a
password is the exception rather than the rule, or if a method proves unusable in a
pilot — the point of the contract is that adding or dropping one is not a rewrite.
