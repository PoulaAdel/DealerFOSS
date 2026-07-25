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
| `OrganizationService.cs` | read workflows behind the contract |
| `OrganizationEndpoints.cs` | HTTP surface (`GET /api/v1/organization`) |
| `OrganizationModule.cs` | DI registration + endpoint mapping |

## Boundaries (enforced by `tests/Architecture`)

- Domain has no EF or ASP.NET dependency.
- The module owns the `org` schema and reads no other module's tables.
- Other modules call `IOrganizationDirectory`, never the entities or `OrganizationDbContext`.

## Notes

- One `DealerOrganization` row exists per tenant database — it is the tenant.
- The DbContext binds to the tenant database resolved by middleware for the
  current request; it never selects a connection itself.
- Server-side authorization arrives with the Identity module ([doc 06 §3](../../../docs/06-Security-and-API.md));
  today the endpoint requires a resolved tenant but not yet a permission.
