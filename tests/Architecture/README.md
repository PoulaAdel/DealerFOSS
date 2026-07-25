# Architecture tests

These tests are the mechanical half of ADR-014. They fail the build when a
dependency crosses a boundary that the compiler alone cannot prevent — a module
reaching into another module's internals, domain code taking an EF or ASP.NET
dependency, or a business module referencing the integration edge.

Run them with the rest of the suite:

```bash
dotnet test OpenDealer360.slnx -c Release
```

## Forbidden-reference rehearsal

An architecture test that has never failed is not evidence. Rehearse it
deliberately — this is an I0 exit criterion, and it should be repeated whenever
a new boundary rule is added.

**1. Introduce a violation.** In `src/Platform/Kernel/Result.cs`, add a
dependency the kernel is forbidden to have:

```csharp
using Microsoft.EntityFrameworkCore; // forbidden in Platform
```

and reference it so the compiler keeps it, for example by adding this member to
`Result`:

```csharp
public static DbContext? Offender => null;
```

You will also need to add the EF Core package reference to
`src/Platform/OpenDealer360.Platform.csproj`, since Platform deliberately does
not have one.

**2. Run the tests.** Expect a failure naming the offending type:

```
Platform_must_not_depend_on_web_or_persistence_frameworks
  Expected ... to be true because Platform is a domain-free kernel;
  offenders: OpenDealer360.Platform.Kernel.Result
```

**3. Revert both edits** and confirm the suite is green again.

If step 2 passes instead of failing, the rule is not actually enforced and must
be fixed before the boundary can be claimed as protected.

## Adding a rule

Add a rule whenever a boundary violation reaches code review: a violation a
human had to catch is a missing test. Once a rule passes it becomes a standing
gate, and a later phase may not regress it.
