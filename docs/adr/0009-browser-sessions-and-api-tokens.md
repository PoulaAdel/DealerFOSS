# ADR-009 — Browser sessions and API tokens

Date: 2026-07-25
Status: Accepted
Supersedes: —

## Context

The application handles PII and financial data from a browser used on a shared dealership floor. Tokens held in browser-accessible storage are exposed to cross-site scripting, and stateless tokens cannot be revoked promptly.

## Decision

The React client uses a backend-for-frontend session in Secure, HttpOnly, SameSite cookies with CSRF protection and rotation after authentication or privilege change. OAuth and OIDC bearer tokens are used for machine integrations and public APIs. ASP.NET Core Identity supplies local identity; MFA and OIDC federation are supported, and global administrators require MFA.

## Correction, 2026-08-15 — a fact in the Decision, not the decision itself

The sentence "ASP.NET Core Identity supplies local identity" was never
implemented and is not what this ADR decided. The decision is the *session
mechanism*: a backend-for-frontend cookie rather than a browser-held token. Which
library provides credentials was an implementation detail written into the same
sentence, and it turned out to be wrong.

What was built: `Microsoft.Extensions.Identity.Core` for `IPasswordHasher<T>`
**only**. Users, roles, sessions, TOTP, passkeys and audit are this project's own
code in `src/Identity` — there is no `UserManager` and no `IdentityDbContext`.

The decision stands unchanged and this file is not rewritten (ADRs are immutable).
[Doc 02 §4](../02-Architecture-and-Decisions.md) carries the accurate statement.
**Where an ADR and doc 02 differ on the *decision*, the ADR governs; where they
differ on a *fact about the code*, the one that matches the code governs.**

## Alternatives considered

- **Bearer tokens in browser storage.** Rejected: exfiltratable by XSS and hard to revoke.
- **Stateless JWT sessions with a deny list.** Rejected as the primary mechanism: revocation becomes dependent on cache availability.

## Consequences

- Session records are durable in SQL, so revocation survives a cache outage.
- Browser clients and machine clients follow deliberately different authentication paths.

## Validation / review trigger

Revisit if a supported client cannot use cookies, for example a native mobile application.
