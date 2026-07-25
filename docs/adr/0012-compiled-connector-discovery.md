# ADR-012 — Compiled connector discovery

Date: 2026-07-25
Status: Accepted
Supersedes: —

## Context

A third-party plugin ecosystem requires signing, isolation, version compatibility, revocation, and a support policy. None of that exists yet, and loading untrusted code into a process holding dealer PII is a serious risk.

## Decision

Connectors publish a manifest and are discovered at application startup. For v1 they are compiled and released with the application. Runtime third-party plugins are deferred.

## Alternatives considered

- **Runtime plugin loading now.** Rejected: unsigned, unisolated third-party code in a process handling PII and financial data.
- **Hard-coded connector registration.** Rejected: it makes adding a connector a core-code change.

## Consequences

- Adding a connector is a build-time contribution accompanied by conformance evidence.
- An ecosystem launch requires a separate ADR covering signing, isolation, compatibility, revocation, and support.

## Validation / review trigger

Revisit when third-party connector demand justifies designing the plugin trust model.
