# ADR-009 — Browser sessions and API tokens

Date: 2026-07-25
Status: Accepted
Supersedes: —

## Context

The application handles PII and financial data from a browser used on a shared dealership floor. Tokens held in browser-accessible storage are exposed to cross-site scripting, and stateless tokens cannot be revoked promptly.

## Decision

The React client uses a backend-for-frontend session in Secure, HttpOnly, SameSite cookies with CSRF protection and rotation after authentication or privilege change. OAuth and OIDC bearer tokens are used for machine integrations and public APIs. ASP.NET Core Identity supplies local identity; MFA and OIDC federation are supported, and global administrators require MFA.

## Alternatives considered

- **Bearer tokens in browser storage.** Rejected: exfiltratable by XSS and hard to revoke.
- **Stateless JWT sessions with a deny list.** Rejected as the primary mechanism: revocation becomes dependent on cache availability.

## Consequences

- Session records are durable in SQL, so revocation survives a cache outage.
- Browser clients and machine clients follow deliberately different authentication paths.

## Validation / review trigger

Revisit if a supported client cannot use cookies, for example a native mobile application.
