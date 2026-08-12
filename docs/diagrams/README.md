# Visual Designs & Flows

Each Mermaid diagram is a visual companion, not a second specification.

| # | Diagram | Owning document |
|---|---|---|
| 01 | [System Architecture](01-system-architecture.md) | [Architecture & Decisions](../02-Architecture-and-Decisions.md) |
| 02 | [Project Structure](02-project-structure.md) | [Project Structure](../03-Project-Structure.md) |
| 03 | [Tenant and Rooftop Resolution](03-tenant-resolution.md) | [Data & Tenancy](../04-Data-and-Tenancy.md) |
| 04 | [Integration Flow](04-dms-sync-flow.md) | [Integration Framework](../05-Integration-Framework.md) |
| 05 | [Deal Lifecycle](05-deal-lifecycle.md) | [Vision & Scope](../01-Vision-and-Scope.md) |
| 06 | [Authentication and Scope](06-auth-flow.md) | [Security & API](../06-Security-and-API.md) |
| 07 | [Deployment Topology](07-deployment.md) | [Delivery Roadmap](../07-Delivery-Roadmap.md) |

## Reading them

These diagrams show the **designed** architecture, which is not the same thing as
the built one. Where the two differ, the difference is drawn rather than
described, using one convention throughout:

- **Solid outline** — built, and covered by tests.
- **Dashed outline, dimmed** — designed and specified, *not built yet*.

A diagram that shows only the target teaches the shape and hides the state; one
that shows only what exists loses the reasoning. Both are needed, so both are
drawn. [`implementation/STATUS.md`](../implementation/STATUS.md) remains the
authority on what is built, with a proving command against each claim.

A diagram may not invent structure its owning document does not describe. If
prose and a diagram disagree, the owning document governs and the diagram is
corrected in the same change (doc 00).
