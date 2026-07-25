# 06 — Security & API

← [Integration Framework](05-Integration-Framework.md) · Next: [Delivery Roadmap](07-Delivery-Roadmap.md)  
Visuals: [Auth flow](diagrams/06-auth-flow.md) · [Deal lifecycle](diagrams/05-deal-lifecycle.md)

## 1. Security model

OpenDealer360 stores PII, financial information, identity documents, and regulated evidence. Security is based on explicit tenant/rooftop scope, least privilege, strong sessions, immutable audit, protected files/secrets, and verifiable operations—not only JWT validation.

Threat models are maintained for authentication/session handling, tenant and rooftop resolution, connector webhooks/credentials, bulk import/export, documents/uploads, financial posting, support access, and software updates.

## 2. Identities and sessions

| Identity | Scope | Authentication |
|---|---|---|
| Tenant user | one dealer organization; assigned rooftops/departments | local Identity or OIDC; MFA based on role/policy |
| Global administrator | deployment control plane only | MFA required; no implicit tenant business access |
| Service account | named API capabilities and tenant scope | OAuth client credentials/certificate; no interactive login |
| Support session | approved tenant, time-limited purpose | strong admin identity, explicit approval, visible and audited |

The browser uses a backend-for-frontend session cookie marked Secure, HttpOnly, and SameSite, with CSRF protection and rotation after authentication/privilege changes. Access and idle timeouts are configurable; the default workday session has a 15-minute idle warning and a maximum eight-hour duration. “Remember me” is disabled for privileged roles.

OIDC/SAML-compatible federation is supported through ASP.NET Core. TOTP is the minimum MFA option; WebAuthn/passkeys are preferred for administrators. Passwords follow current ASP.NET Identity/NIST-style rules: block known-compromised passwords, allow password managers and long passphrases, avoid composition-rule dependence, rate-limit attempts, and provide secure recovery.

Session records are durable in SQL and include device/user-agent summary, issued/last-seen/expiry, MFA level, and revocation. Cache failure cannot revive a revoked privileged session. Users can view and revoke their sessions.

## 3. Authorization and segregation of duties

Permissions combine action and scope. A user assignment grants a role at organization, rooftop, or department level. The server checks both:

```text
Permission: Deals.Approve
Scope: Rooftop A
Resource: Deal belongs to Rooftop A
```

Organization-wide permission is explicit. Global administrator claims are rejected by tenant endpoints unless a time-limited support-access flow creates a separate audited tenant session.

Sensitive actions require step-up authentication, a reason, and optionally dual approval: fiscal-period reopen, vendor/bank detail change, large refund, F&I cancellation, bulk PII export, support access, and destructive configuration change. Configurable segregation-of-duty rules prevent the same user from originating and approving selected transactions.

Frontend gating is convenience only. Every endpoint and background command performs server authorization at execution time.

## 4. Data and secrets

- TLS is required; HSTS and secure headers are enabled.
- Sensitive configuration uses Windows certificate store/DPAPI or an external secret manager. Encryption specifies authenticated encryption, key ID, rotation, and recovery; “AES-256” alone is not a design.
- Connector credentials and selected restricted fields use envelope encryption.
- Database backups and document backups are encrypted off-host.
- Logs, traces, errors, and diagnostic bundles redact credentials, tokens, credit data, government IDs, and document content.
- Uploads enter quarantine, are type-sniffed and malware-scanned, have size limits, and are served as non-executable content.

## 5. Audit, privacy, and compliance evidence

Audit events are append-only and include actor, session, tenant, rooftop, action, target, timestamp, correlation ID, source IP, reason, and a safe before/after summary. High-value audit streams are hash-chained or exported to immutable storage. Audit access is itself audited.

Every record has a data classification and retention policy. Legal hold overrides purge. Privacy workflows support discovery, export, correction, restriction, and deletion where law permits without destroying required accounting/compliance evidence.

F&I and communications store the exact version of a menu, disclosure, notice, consent, message template, calculation inputs, delivery result, signer evidence, and resulting document hash. Provider integration does not outsource OpenDealer360’s evidence trail. Jurisdiction rule packs require compliance review before being marked supported.

## 6. API conventions

- REST endpoints live under `/api/v1/`; public contracts are documented by OpenAPI.
- Successful responses return their resource directly. Errors use RFC Problem Details with stable application error codes.
- Commands that could be retried—create, post, payment, export, connector write—accept an idempotency key.
- Mutable resources expose an ETag/concurrency token; conflicts return 409 rather than overwriting newer work.
- Lists use bounded paging; cursor pagination is preferred for rapidly changing or large datasets.
- Bulk import/export is an asynchronous job with progress, errors, cancellation rules, and result download.
- Public breaking changes require a new API version and at least a 12-month supported transition after general availability.
- File downloads authorize the document and scope before producing a short-lived stream; filesystem paths are never exposed.

## 7. Secure development and incident response

CI runs static analysis, dependency/license and secret scanning, SBOM generation, architecture/isolation tests, and artifact signing/provenance where supported. Dependency vulnerabilities have severity-based remediation targets.

Production readiness requires a security contact, private disclosure path, incident roles, evidence-preservation and notification runbook, supported-version policy, and an independent penetration test covering tenant/rooftop authorization, sessions, imports, documents, connectors, and administration.

## 8. Example: creating and approving a deal

1. The authenticated session resolves one dealer organization and the user’s rooftop assignments.
2. `POST /api/v1/deals` requires `Deals.Create` for the selected rooftop and an idempotency key.
3. Sales validates the customer, vehicle/inventory unit, currency, legal entity, and current record versions through published contracts.
4. The deal and its initial version are committed with an audit event and outbox event.
5. Approval checks `Deals.Approve`, segregation-of-duty rules, step-up policy, and the exact deal version. Changes invalidate earlier approval.
6. Connector export runs asynchronously; the UI distinguishes local approval from provider confirmation.
