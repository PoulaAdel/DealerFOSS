# Customers module

The people and businesses the dealership deals with — the first thing anyone at a
dealership actually looks up ([doc 04 §3](../../../docs/04-Data-and-Tenancy.md)).

**A customer belongs to the whole dealer organization, not to one rooftop.** The
same person buys at one location and services at another; hiding them by location
would only make staff create duplicates. `HomeRooftopId` records where they were
first met and is for reporting — never for permissions.

## Layout

| Path | Role |
|---|---|
| `Domain/` | `Customer` (person or business), `ContactPoint`, `Address` — entities and rules; no EF/ASP.NET dependency |
| `Data/` | `CustomersDbContext` (owns the `customers` schema), migrations, design-time factory |
| `Contracts/` | `ICustomerDirectory` + read models — the only surface other modules use (ADR-008) |
| `CustomerService.cs` | search, read, and add, with the permission checks |
| `CustomersEndpoints.cs` | HTTP surface under `/api/v1/customers` |
| `CustomersModule.cs` | DI registration + endpoint mapping |

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

- Domain has no EF or ASP.NET dependency.
- The module owns the `customers` schema and reads no other module's tables.
- Identity is reachable only through its `Contracts` namespace.
- This module does not reference the Host or a sibling module — Sales and Service
  will depend on it later, so a reference back would be circular.

## Not built yet

Editing an existing customer, merging duplicates, consent records, relationships
between customers, and identity matching against an incoming feed. The staging and
duplicate-review workflow arrives with migration ([doc 05](../../../docs/05-Integration-Framework.md)).
