# Organization module

Owns the tenant's structural backbone: **dealer organization → legal entity →
rooftop → department** ([doc 04 §1](../../../docs/04-Data-and-Tenancy.md)). Every
rooftop-scoped record in other modules references a rooftop defined here.

## Layout

| Path | Role |
|---|---|
| `Domain/` | `DealerOrganization`, `LegalEntity`, `Rooftop`, `Department` — entities and invariants; no EF/ASP.NET dependency |
| `Data/` | `OrganizationDbContext` (owns the `org` schema), migrations, design-time factory |
| `Contracts/` | `IOrganizationDirectory` + read models — the only surface other modules use (ADR-008) |
| `OrganizationService.cs` | read workflows, and the rooftop-scope check applied to them |
| `OrganizationEndpoints.cs` | HTTP surface (`GET /api/v1/organization`, `…/rooftops/{id}`) |
| `OrganizationModule.cs` | DI registration + endpoint mapping |

## Boundaries (enforced by `tests/Architecture`)

- Domain has no EF or ASP.NET dependency.
- The module owns the `org` schema and reads no other module's tables.
- Other modules call `IOrganizationDirectory`, never the entities or `OrganizationDbContext`.
- Identity is reachable only through its `Contracts` namespace.

## Notes

- One `DealerOrganization` row exists per tenant database — it is the tenant.
- The DbContext binds to the tenant database resolved by middleware for the
  current request; it never selects a connection itself.
- Reads are filtered to the caller's authorized rooftops, and a rooftop addressed
  directly by id is refused with `403`. An unauthorized rooftop and an unknown one
  return the same failure, so the response cannot be used to enumerate rooftops.
  The check lives in the service, not the endpoint, so background callers are
  covered too ([doc 06 §3](../../../docs/06-Security-and-API.md)).
