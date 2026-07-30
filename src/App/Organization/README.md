# Organization

Owns the tenant's structural backbone: **dealer organization → legal entity →
rooftop → department** ([doc 04 §1](../../../docs/04-Data-and-Tenancy.md)). Every
rooftop-scoped record in another capability references a rooftop defined here.

## Layout

| File | Role |
|---|---|
| `DealerOrganization.cs`, `LegalEntity.cs`, `Rooftop.cs`, `Department.cs` | entities and invariants; no EF or ASP.NET dependency |
| `OrganizationService.cs` | read workflows, and the rooftop-scope check applied to them |
| `OrganizationEndpoints.cs` | HTTP surface (`GET /api/v1/organization`, `…/rooftops/{id}`) |
| `OrganizationTables.cs` | EF configuration; owns the `org` schema |
| `IOrganization.cs` + `OrganizationViews.cs` | what other capabilities may call, and the read models |

## Boundaries (enforced by `tests/Architecture`)

- Entities have no EF or ASP.NET dependency.
- This capability owns the `org` schema and reads no other capability's tables.
- Other capabilities call `IOrganization`, never the entities directly.
- Nothing here reaches into Customers, Vehicles, or Inventory.

## Notes

- One `DealerOrganization` row exists per tenant database — it *is* the tenant.
- Queries go through `TenantDb`, which is bound to the database resolved by
  middleware for the current request. Nothing here selects a connection.
- Reads are filtered to the caller's authorized rooftops, and a rooftop addressed
  directly by id is refused with `403`. An unauthorized rooftop and an unknown one
  return the same failure, so the response cannot be used to enumerate rooftops.
  The check lives in the service, not the endpoint, so background callers are
  covered too ([doc 06 §3](../../../docs/06-Security-and-API.md)).
