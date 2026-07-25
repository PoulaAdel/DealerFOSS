# ADR-013 — AGPLv3 with optional commercial license

Date: 2026-07-25
Status: Accepted
Supersedes: —

## Context

The project competes with well-funded incumbents. A permissive license would allow a competitor to take the code, close it, and run a proprietary hosted service without contributing back. The project also needs a funding path.

## Decision

The complete self-hosted core is licensed AGPLv3. An optional commercial license provides alternative terms for closed embedding or commercial arrangements. It is not required to obtain a functional self-hosted DMS, and no core feature is withheld.

## Alternatives considered

- **Apache 2.0 or MIT.** Rejected: permits a closed hosted fork of the community's work.
- **Open core with paywalled features.** Rejected: it contradicts the promise of a complete self-hosted product.

## Consequences

- Network use of a modified version triggers the source-offer obligation.
- Optional relicensing requires clear contributor provenance, so a DCO or CLA must be chosen with legal review before external code is accepted.
- Commercial dependencies must remain optional adapters so the AGPL core stays fully buildable.

## Validation / review trigger

Revisit only with legal review; the trademark policy is maintained separately from the code license.
