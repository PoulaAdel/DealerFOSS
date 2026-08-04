# Architecture tests

These tests are the mechanical half of ADR-014, and after
[ADR-017](../../docs/adr/0017-three-projects-flat-features.md) they carry more
weight than before. Two boundaries are enforced by the compiler because a breach
would be a security incident — `Core` may not touch a database, and `Identity`'s
tables are unreachable. **Every other boundary is held by these tests alone.**

That includes the one contributors trip over most: the capabilities inside
`src/App` share a project, so a `using DealerFOSS.Vehicles;` inside
`Customers/` compiles perfectly and fails here instead.

| File | Holds |
|---|---|
| `BoundaryTests.cs` | the project-level walls: `Core` free of infrastructure, `Identity` below the application, and Identity's exported type list |
| `FeatureBoundaryTests.cs` | the capability walls inside `App`, plus entity purity and connection ownership |

Run them with the rest of the suite:

```bash
dotnet test DealerFOSS.slnx -c Release
```

## Forbidden-reference rehearsal

An architecture test that has never failed is not evidence. Rehearse it
deliberately — this is an I0 exit criterion, and it should be repeated whenever a
new boundary rule is added.

**1. Introduce a violation.** In `src/App/Customers/Customer.cs`, add a
dependency the rules forbid:

```csharp
using DealerFOSS.Vehicles; // a sibling capability's internals
```

and reference it so the compiler keeps it, for example by adding this member to
`Customer`:

```csharp
public static Vehicle? Offender => null;
```

**2. Run the tests.** Expect a failure naming the offending type:

```
FeatureBoundaryTests.A_feature_must_not_reach_into_another_feature
  Expected ... to be true because DealerFOSS.Customers must reach a sibling
  only through its published contract; offenders: DealerFOSS.Customers.Customer
```

**3. Revert the edit** and confirm the suite is green again.

If step 2 passes instead of failing, the rule is not actually enforced and must be
fixed before the boundary can be claimed as protected.

### Rehearsing the Core wall instead

The same procedure works on `src/Core/Result.cs` with
`using Microsoft.EntityFrameworkCore;` and a `public static DbContext? Offender`
member — but note that `Core` has no EF package reference, so this one fails at
the compiler before the test even runs. That is the point of putting it behind a
project boundary rather than a test.

## Adding a rule

Add a rule whenever a boundary violation reaches code review: a violation a human
had to catch is a missing test. Once a rule passes it becomes a standing gate, and
a later phase may not regress it.

A new capability inside `src/App` needs a row in
`FeatureBoundaryTests.ForbiddenFeatureDependencies` naming the siblings it may not
touch. Forgetting it means the capability is unguarded.
