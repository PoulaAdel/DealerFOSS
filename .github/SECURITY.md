# Security Policy

DealerFOSS stores PII, financial data, identity documents, and regulated
evidence. Security is treated as product work, not a hardening afterthought
(see [docs/06-Security-and-API.md](../docs/06-Security-and-API.md)).

## Reporting a vulnerability

**Do not open a public issue for a security vulnerability.** Use GitHub's private
vulnerability reporting for this repository, or email the maintainers' security
contact listed on the project page. Include: affected version/commit, impact, and
reproduction steps. You will receive an acknowledgement and a remediation timeline
based on severity.

Please do not run intrusive testing against deployments you do not own.

## Scope of concern

Threat models are maintained for authentication and sessions, tenant and rooftop
resolution, connector webhooks and credentials, bulk import/export, document
uploads, financial posting, support access, and software updates.

## Supported versions

Supported release lines and their security patch targets are published with each
release (see [docs/08-Governance-and-Standards.md](../docs/08-Governance-and-Standards.md) §4).
Run a supported version to receive fixes.
