# Frontend

React, TypeScript, and Vite. **Node is not installed on the development machine
and does not need to be** — the toolchain runs in a container
([`deploy/README.md`](../deploy/README.md)).

## Running it

**Start the backend first, and make sure it is actually serving the API.**

Without `src/App/appsettings.Development.json` supplying a `HostCatalog`
connection string, the application starts but maps **no API routes at all** — so
every call answers `404` and it looks as though the frontend is pointed at the
wrong place. Copy the example and fill it in:

```bash
cp src/App/appsettings.Development.json.example src/App/appsettings.Development.json
```

It needs a connection string and `Seed:Enabled` set to `true`. Then, on the host:

```bash
dotnet run --project src/App
```

Check it before going further — this should return the service banner:

```bash
curl http://localhost:5080/
```

Now the toolchain. Start the container once:

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

Open `http://localhost:5173`.

### Why the dev server proxies `/api`

The session cookie is `SameSite=Strict`, so the browser only sends it on
same-origin requests. If the page were served from `:5173` and called `:5080`
directly, the cookie would silently be dropped and every request would look
unauthenticated. Vite proxies `/api` instead, so the browser sees one origin.

From inside a container, `localhost` is the container — the host is reachable as
`host.docker.internal`, which is the proxy default. Running the toolchain
directly on a host instead? Set `ODMS_API=http://localhost:5080`.

## Testing

```bash
docker exec odms-node sh -c "cd /workspace/frontend && npm test"
```

The tests mount the real components into a real DOM (jsdom) and read what a
person would read — headings, labels, roles, visible text. That is deliberate:
until they existed, *"the frontend has never been seen rendering"* was an honest
limitation nobody could close without opening a browser, and every screen built
on top of it inherited the doubt.

Two habits keep them worth having:

- **Assert on what a person perceives**, not on class names or component
  internals. A restyle must not read as a regression, and a test that would
  still pass with the text removed is testing nothing.
- **Break it before you trust it.** Every test here has been watched failing
  against a deliberate defect. The procedure is the same one
  [`tests/Architecture/README.md`](../tests/Architecture/README.md) describes.

`src/test/setup.ts` answers `fetch` from a table of replies. A test that wants a
real server is an integration test and belongs in `tests/Integration`.

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
the next request. The *other* cookie, `odms_csrf`, is readable on purpose: `api`
copies it into the `X-CSRF-Token` header on every write, and the server refuses
writes that arrive without it. That happens in one place so no screen has to
remember it.

**Every screen renders every state.** Loading, empty, permission-denied, failure,
and retry. A screen that only handles the happy path is not finished
([doc 10 §5](../docs/10-Claude-Code-Execution-Prompt.md)). `InventoryPage` is the
reference for what that looks like, and `InventoryPage.test.tsx` is what stops
that claim being taken on trust.

## Accessibility

Not a later pass. A visible focus ring on everything interactive, a skip link
ahead of the navigation, labels tied to inputs, errors announced with
`role="alert"`, focus moved to the code field when the second-factor step
appears, and status shown as a word rather than only a colour.

## When something answers 404

| What returned it | Why |
|---|---|
| Every `/api/v1/...` call | The backend has no `HostCatalog` connection string, so it mapped no API routes. See above — this is the common one. |
| Only calls after signing in | The dealer group does not exist. An unknown tenant is a deliberate `404`, so the response cannot be used to discover which dealers are on an installation. `northgroup` and `citymotors` are the seeded ones. |
| The page itself, at `:5173` | Vite is serving from the wrong directory. It must be started from `frontend/`. |

## Not built yet

Customers, vehicles, leads, deals, and the ledger all have working APIs and no
screens. `InventoryPage` is the pattern to copy. So does the control plane —
signing in as an administrator, listing dealerships, and opening or closing a
support visit are API-only.

Also missing: an OpenAPI-generated client (`src/shared/contracts.ts` is
hand-written and must be changed alongside the server — it had already drifted
once, missing `mustEnrolSecondFactor`), and print layouts.

**Still unconfirmed by a person:** the tests prove the components render and
behave in jsdom, which is not the same as looking right in a browser. Fonts,
layout, colour contrast in dark mode, and whether a phone camera can actually
read the QR code are all things only somebody with a screen can tell you.
