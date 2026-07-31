# Frontend

React, TypeScript, and Vite. **Node is not installed on the development machine
and does not need to be** — the toolchain runs in a container
([`deploy/README.md`](../deploy/README.md)).

## Running it

Start the container once:

```bash
docker compose -f deploy/docker-compose.yml --profile node up -d
```

Then work inside it:

```bash
docker exec -it odms-node sh
```

```bash
cd /workspace/frontend && npm install
```

```bash
npm run dev
```

Open `http://localhost:5173`. The backend runs on the **host** meanwhile:

```bash
dotnet run --project src/App
```

### Why the dev server proxies `/api`

The session cookie is `SameSite=Strict`, so the browser only sends it on
same-origin requests. If the page were served from `:5173` and called `:5080`
directly, the cookie would silently be dropped and every request would look
unauthenticated. Vite proxies `/api` instead, so the browser sees one origin.

From inside a container, `localhost` is the container — the host is reachable as
`host.docker.internal`, which is the proxy default. Running the toolchain
directly on a host instead? Set `ODMS_API=http://localhost:5080`.

## Layout

| Path | Role |
|---|---|
| `src/app/` | routing, the authenticated shell, and who is signed in |
| `src/features/` | one folder per screen area, mirroring the backend capabilities |
| `src/shared/` | the API client and the response shapes |
| `src/theme/` | the whole visual language, in one file until it stops scanning |

## Three rules worth knowing

**The route guard is a convenience, not a control.** Every screen calls an API
that enforces the same rules server-side, and the server's answer is the one that
counts. Hiding a link protects nothing.

**The session cookie is HttpOnly, so this code cannot read it.** "Am I signed
in?" is answered by asking the server — which is also the only answer worth
having, because a session revoked on another device has to stop working here on
the next request.

**Every screen renders every state.** Loading, empty, permission-denied, failure,
and retry. A screen that only handles the happy path is not finished
([doc 10 §5](../docs/10-Claude-Code-Execution-Prompt.md)). `InventoryPage` is the
reference for what that looks like.

## Accessibility

Not a later pass. A visible focus ring on everything interactive, a skip link
ahead of the navigation, labels tied to inputs, errors announced with
`role="alert"`, focus moved to the code field when the second-factor step
appears, and status shown as a word rather than only a colour.

## Not built yet

Customers, vehicles, leads, deals, and the ledger all have working APIs and no
screens. `InventoryPage` is the pattern to copy. Also missing: an OpenAPI-generated
client (`src/shared/contracts.ts` is hand-written and must be changed alongside
the server), tests, and print layouts.
