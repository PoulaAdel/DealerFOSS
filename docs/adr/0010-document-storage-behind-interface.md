# ADR-010 — Document storage behind `IDocumentStore`

Date: 2026-07-25
Status: Accepted
Supersedes: —

## Context

Dealerships generate contracts, photos, and identity documents. On-premises installations expect filesystem storage; a future hosted offering needs object storage. Uploads are an attack surface.

## Decision

Document content sits behind an `IDocumentStore` abstraction, with filesystem storage as the on-premises default and an S3-compatible implementation configurable. Metadata, hash, classification, version, retention, and transaction links live in SQL. Uploads are quarantined and scanned before becoming available.

## Alternatives considered

- **Store documents in the database.** Rejected: it inflates backup size and restore time.
- **Filesystem access directly from business code.** Rejected: it prevents a hosted deployment without rewriting business logic.

## Consequences

- Swapping storage does not touch business logic.
- Filesystem paths are never exposed; downloads authorize the document and scope first.

## Validation / review trigger

Revisit when the hosted offering requires object storage as the default.
