# Customers

The people and businesses the dealership deals with — the first thing anyone at a
dealership actually looks up ([doc 04 §3](../../../docs/04-Data-and-Tenancy.md)).

**A customer belongs to the whole dealer organization, not to one rooftop.** The
same person buys at one location and services at another; hiding them by location
would only make staff create duplicates. `HomeRooftopId` records where they were
first met and is for reporting — never for permissions.

## Layout

| File | Role |
|---|---|
| `Customer.cs`, `ContactPoint.cs`, `Address.cs` | entities and rules; no EF or ASP.NET dependency |
| `CustomerService.cs` | search, read, and add, with the permission checks |
| `CustomerEndpoints.cs` | HTTP surface under `/api/v1/customers` |
| `CustomerTables.cs` | EF configuration; owns the `customers` schema |
| `ICustomers.cs` | what other capabilities may call, and the read models |

## Two rules worth knowing before you change anything

**An email address is a way to reach someone, not their identity.** Two customers
may legitimately share one — a couple, a family business. There is deliberately no
unique constraint on a contact value, and customers are never matched on email
alone. Enforcing uniqueness pushes staff into inventing fake addresses to get past
the validation, which is worse than the duplicate you were trying to prevent.

**Contact values are stored normalized so search works.** A phone number keeps
only its digits and a leading `+`, so `(555) 010-2030` and `555-010-2030` are the
same number; an email is lower-cased. Search normalizes the term the same way
before comparing.

## Permissions

`Customers.Read` and `Customers.Create` are separate on purpose. The development
`Advisor` role holds only the first, which is what makes "can look a customer up
but cannot add one" a real assertion in the tests rather than an assumption.

## Boundaries (enforced by `tests/Architecture`)

- Entities have no EF or ASP.NET dependency.
- This capability owns the `customers` schema and reads no other's tables.
- Nothing here reaches into Organization, Vehicles, or Inventory. Sales and
  Service will depend on Customers later, so a reference back would be circular.

## Not built yet

Editing an existing customer, merging duplicates, consent records, relationships
between customers, and identity matching against an incoming feed. The staging and
duplicate-review workflow arrives with migration
([doc 05](../../../docs/05-Integration-Framework.md)).
