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
docker exec -it dealerfoss-node sh
```

```bash
cd /workspace/frontend && npm install
```

```bash
npm run dev
```

Open `http://localhost:5173`.

> **If an edit seems to do nothing**, the watcher is the first suspect, not your
> code. The repository is bind-mounted from Windows into a Linux container and
> inotify events do not cross that boundary, so Vite's default watcher never
> fires — and a hard refresh serves the previous bundle, which reads as "my
> change had no effect". `vite.config.ts` enables polling to fix it; if you
> change that file, restart the dev server, because config is read once.

### Why the dev server proxies `/api`

The session cookie is `SameSite=Strict`, so the browser only sends it on
same-origin requests. If the page were served from `:5173` and called `:5080`
directly, the cookie would silently be dropped and every request would look
unauthenticated. Vite proxies `/api` instead, so the browser sees one origin.

From inside a container, `localhost` is the container — the host is reachable as
`host.docker.internal`, which is the proxy default. Running the toolchain
directly on a host instead? Set `DEALERFOSS_API=http://localhost:5080`.

## Testing

```bash
docker exec dealerfoss-node sh -c "cd /workspace/frontend && npm test"
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
| `src/app/` | routing, the two shells, and who is signed in to each |
| `src/features/` | one folder per screen area, mirroring the backend capabilities |
| `src/features/service/` | the workshop, and the diary of cars still to come, on one screen |
| `src/shared/` | the two API clients and the response shapes |
| `src/shared/i18n/` | every visible string, in five languages, and the direction that follows the language |
| `src/test/` | the fetch stub every component test shares |
| `src/theme/` | the whole visual language, in one file until it stops scanning |

## Two applications in one bundle

`/admin/*` is the control-plane console; everything else is the dealership
product. The split happens in `App.tsx` **above** `SessionProvider`, so the two
session contexts are never mounted at once — an administrator belongs to no
dealership, and asking `/auth/me` on their behalf is a meaningless question.

They also have **separate API clients**, and that is not duplication to be tidied
away:

| | Dealership | Control plane |
|---|---|---|
| Client | `shared/api.ts` | `shared/adminApi.ts` |
| Session cookie | `dfoss_session` | `dfoss_admin` |
| Anti-forgery cookie | `dfoss_csrf` | `dfoss_admin_csrf` |
| Anti-forgery header | `X-CSRF-Token` | `X-Admin-CSRF-Token` |
| Tenant header | always | only when opening support access |

During a support visit one browser holds **both** sets at once. A single client
deciding between them from a flag would eventually send a dealership's token to
the control plane, or the reverse; two functions cannot make that mistake.
`adminApi.test.ts` asserts it in both directions.

## Rules worth knowing

**The route guard is a convenience, not a control.** Every screen calls an API
that enforces the same rules server-side, and the server's answer is the one that
counts. Hiding a link protects nothing.

**The session cookie is HttpOnly, so this code cannot read it.** "Am I signed
in?" is answered by asking the server — which is also the only answer worth
having, because a session revoked on another device has to stop working here on
the next request. The *other* cookie, `dfoss_csrf`, is readable on purpose: `api`
copies it into the `X-CSRF-Token` header on every write, and the server refuses
writes that arrive without it. That happens in one place so no screen has to
remember it.

**A safeguard belongs in the component, not in a comment.** `RecordsPage` will
not enable the real import until a practice run has finished on that exact file,
and re-locks when the file or kind changes. That is the kind of rule that gets
"simplified" away by somebody who reads it as friction — `RecordsPage.test.tsx`
is what makes removing it fail loudly.

**A screen is five bands, in order of urgency.** Head, what needs a person now,
the next thing in place, the record list, the selected record inline. Selecting a
row is never a second route — the operator keeps their filter and their place.
[ADR-020](../docs/adr/0020-screen-shape-and-interface-standards.md) has the shape,
the rules a band obeys, and an honest list of which interface standards this build
meets, meets partly, and does not meet yet.

**Every screen renders every state.** Loading, empty, permission-denied, failure,
and retry. A screen that only handles the happy path is not finished.
`InventoryPage` is the
reference for what that looks like, and `InventoryPage.test.tsx` is what stops
that claim being taken on trust.

**No visible string is written in a component.** It goes in
`shared/i18n/locales/en.ts` and comes back through `t()`. English is the schema:
the other four catalogues are typed against it, so a key you add and forget to
translate fails `npm run typecheck` rather than surfacing an English sentence in
the middle of a Russian screen. See
[ADR-019](../docs/adr/0019-language-owns-direction-and-ui-only-translation.md).

Four things follow from that and are easy to get wrong:

- **Never `n === 1`.** Counted nouns use a plural entry and `t(key, { count })`.
  Russian has four plural categories and Arabic six; two forms are wrong for
  most numbers in both. `Intl.PluralRules` picks, and the call site never counts.
- **Never `new Intl.NumberFormat(undefined, …)`.** `undefined` follows the
  *operating system*, not the application. Use `format.money`, `format.number`,
  `format.date`, `format.dateTime`, `format.monthAndYear` and `format.list` from
  `useI18n()`.
- **Never print an API enum.** `OnHold` is not a word. Use `useEnumLabel()`,
  which is typed from the catalogue keys, so adding a value to a union without a
  label is a compile error at the call site.
- **Records are not translated.** Customer names, vehicle descriptions, part
  numbers, notes, rooftop codes and imported rows print exactly as stored, in
  every language. If the dealership typed it, it is theirs.

**Codes carry `dir="ltr"`.** VINs, stock numbers, account codes, recovery codes,
the TOTP secret, email addresses, raw CSV rows. Inside an Arabic paragraph the
bidirectional algorithm reorders their groups otherwise — and a VIN read out in
the wrong order is a different car.

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

**Vehicles** have a working API and no screen of their own. A customer's car can
be chosen when booking one in, and that is the only place the list surfaces —
there is nowhere to correct a VIN or see a car's history. (This entry used to say
the same of customers, leads, deals and the ledger; all four have had screens for
some time and the sentence was stale. `InventoryPage` is still the pattern to
copy.)

A refused anti-forgery check is detected (`ApiError.needsSignIn`) and acted on
nowhere: such a write shows the raw refusal instead of sending the person to sign
in again. Only `api.test.ts` reads the flag.

Also missing: an OpenAPI-generated client (`src/shared/contracts.ts` is
hand-written and must be changed alongside the server — it had already drifted
once, missing `mustEnrolSecondFactor`), and print layouts.

**Confirmed on a real browser, 2026-08-03.** Every screen signed into and walked
at 1280 and 375. jsdom has no layout engine, and opening the real thing found
three defects it could not have: a list overstating a total, two sign-in screens
that looked alike, and a dead file watcher. Do that after any visual change —
it is not blocked on anybody. The one thing still needing a person is physical:
whether a phone camera reads the QR code.
