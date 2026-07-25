# Project Structure

Specified in [03 — Project Structure](../03-Project-Structure.md).

```mermaid
flowchart TB
    Root["OpenDealer360/"] --> Src["src/"]
    Root --> Front["frontend/"]
    Root --> Tests["tests/"]
    Root --> Deploy["deploy/"]
    Root --> Docs["docs/"]
    Src --> Host["Host/"]
    Src --> Platform["Platform/"]
    Src --> Modules["Modules/"]
    Src --> Integrations["Integrations/"]
    Src --> Cli["Cli/"]
    Modules --> Org["Organization/"]
    Modules --> Sales["Sales/"]
    Modules --> Service["Service/"]
    Modules --> Others["Customers · Vehicles · Inventory · CRM · Finance · ..."]
```

Small modules remain flat. Large modules use only the structure they need:

```mermaid
flowchart LR
    subgraph Sales["Modules/Sales/"]
        Domain["Domain/<br/>models and rules"]
        Features["Features/<br/>Create · Desk · Approve · Unwind"]
        Data["Data/<br/>EF and projections"]
        Contracts["Contracts/<br/>public interfaces/events"]
        Api["Endpoints"]
    end
    Api --> Features --> Domain
    Features --> Contracts
    Data --> Domain
```

Compiler references, architecture tests, and schema-ownership tests enforce the dependency rules.
