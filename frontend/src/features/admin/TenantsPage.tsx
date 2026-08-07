// TenantsPage — which dealerships exist, and whether each is in service.
//
// Use:  the console's landing screen.
// Edit: look at what a row carries — a name, a key, a state, a schema version,
//       a date. Nothing about the dealership's business, because that is the
//       whole point of this screen existing separately from the product.
//
//       Suspending is a real outage for a real dealership, so it asks first.
//       Confirmation lives here rather than in the API: the server is right to
//       do exactly what a properly authorized request tells it to.

import { useCallback, useEffect, useState } from 'react';
import { ApiError, adminApi, adminPost } from '../../shared/adminApi';
import type { ProvisionedTenant, TenantRow } from '../../shared/contracts';

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; tenants: TenantRow[] }
  | { kind: 'failed'; message: string };

export function TenantsPage() {
  const [load, setLoad] = useState<Load>({ kind: 'loading' });
  const [working, setWorking] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [provisioned, setProvisioned] = useState<ProvisionedTenant | null>(null);

  const fetchTenants = useCallback(async () => {
    setLoad({ kind: 'loading' });

    try {
      setLoad({ kind: 'ready', tenants: await adminApi<TenantRow[]>('/tenants') });
    } catch (failure) {
      setLoad({
        kind: 'failed',
        message:
          failure instanceof ApiError ? failure.message : 'Could not load the dealership list.',
      });
    }
  }, []);

  useEffect(() => {
    void fetchTenants();
  }, [fetchTenants]);

  async function setStatus(tenant: TenantRow, status: 'Active' | 'Suspended') {
    if (
      status === 'Suspended' &&
      !window.confirm(
        `Suspend ${tenant.name}? Everyone there is signed out of the system ` +
          `immediately and cannot work until it is resumed.`,
      )
    ) {
      return;
    }

    setWorking(tenant.slug);

    try {
      await adminPost(`/tenants/${encodeURIComponent(tenant.slug)}/status`, { status });
      await fetchTenants();
    } catch (failure) {
      setLoad({
        kind: 'failed',
        message: failure instanceof ApiError ? failure.message : 'That did not work.',
      });
    } finally {
      setWorking(null);
    }
  }

  return (
    <>
      <header className="page__head">
        <h1>Dealerships</h1>
        {creating ? null : (
          <button type="button" className="primary" onClick={() => setCreating(true)}>
            Set up a dealership
          </button>
        )}
      </header>

      {provisioned === null ? null : (
        <section className="panel panel--code" aria-live="polite">
          <h2>{provisioned.name} is ready</h2>
          <p>
            Their first manager is <strong>{provisioned.managerEmail}</strong>. Read
            this code out to them — they set their own password with it at the
            sign-in screen.
          </p>
          <p className="code">{provisioned.enrolmentCode}</p>
          <p className="note">
            <strong>This is the only time it can be shown.</strong> Only a scrambled
            copy is kept, so it cannot be looked up again — if it goes astray, the
            manager can be issued a new one from the dealership's own People
            screen. The books are open, so they can trade straight away.
          </p>
          <button type="button" onClick={() => setProvisioned(null)}>
            I have passed it on
          </button>
        </section>
      )}

      {creating ? (
        <CreateTenant
          onCancel={() => setCreating(false)}
          onCreated={async (result) => {
            setCreating(false);
            setProvisioned(result);
            await fetchTenants();
          }}
        />
      ) : null}

      {load.kind === 'loading' ? (
        <p className="state" aria-live="polite">
          Loading the dealership list…
        </p>
      ) : null}

      {load.kind === 'failed' ? (
        <div className="state" role="alert">
          <p>{load.message}</p>
          <button type="button" onClick={() => void fetchTenants()}>
            Try again
          </button>
        </div>
      ) : null}

      {load.kind === 'ready' && load.tenants.length === 0 ? (
        <p className="state">
          No dealerships on this installation yet. Creating one is still a
          developer’s job.
        </p>
      ) : null}

      {load.kind === 'ready' && load.tenants.length > 0 ? (
        <div className="scroll">
          <table>
            <caption className="visually-hidden">
              {load.tenants.length} dealerships on this installation
            </caption>
            <thead>
              <tr>
                <th scope="col">Name</th>
                <th scope="col">Key</th>
                <th scope="col">Status</th>
                <th scope="col">Schema</th>
                <th scope="col">In service</th>
              </tr>
            </thead>
            <tbody>
              {load.tenants.map((tenant) => (
                <tr key={tenant.slug}>
                  <td>{tenant.name}</td>
                  <td className="mono">{tenant.slug}</td>
                  <td>
                    <span className={`chip chip--${tenant.status.toLowerCase()}`}>
                      {tenant.status}
                    </span>
                  </td>
                  <td className="mono">{tenant.databaseVersion}</td>
                  <td>
                    {tenant.status === 'Suspended' ? (
                      <button
                        type="button"
                        disabled={working === tenant.slug}
                        onClick={() => void setStatus(tenant, 'Active')}
                      >
                        Resume
                      </button>
                    ) : (
                      <button
                        type="button"
                        disabled={working === tenant.slug}
                        onClick={() => void setStatus(tenant, 'Suspended')}
                      >
                        Suspend
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
    </>
  );
}

/**
 * Setting up a dealership.
 *
 * There is deliberately no password field. The manager gets a one-time code and
 * chooses their own — the same path a starter uses, and the reason nobody at the
 * vendor ever knows a dealership password.
 *
 * The short name is refused rather than tidied if it has capitals or spaces: it
 * becomes part of a database name and travels in a header on every request, and
 * silently storing something other than what was typed is a surprise waiting to
 * happen.
 */
function CreateTenant({
  onCancel,
  onCreated,
}: {
  onCancel: () => void;
  onCreated: (result: ProvisionedTenant) => Promise<void>;
}) {
  const [name, setName] = useState('');
  const [slug, setSlug] = useState('');
  const [rooftopName, setRooftopName] = useState('');
  const [rooftopCode, setRooftopCode] = useState('');
  const [managerName, setManagerName] = useState('');
  const [managerEmail, setManagerEmail] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Suggested, never forced — the operator can type over it, and the server has
  // the final say on whether it is usable.
  const suggested = name
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 40);

  const effectiveSlug = slug.trim() === '' ? suggested : slug.trim();

  async function submit() {
    setBusy(true);
    setError(null);

    try {
      await onCreated(
        await adminPost<ProvisionedTenant>('/tenants', {
          slug: effectiveSlug,
          name: name.trim(),
          legalEntityName: name.trim(),
          rooftopName: rooftopName.trim() === '' ? name.trim() : rooftopName.trim(),
          rooftopCode: rooftopCode.trim(),
          managerEmail: managerEmail.trim(),
          managerName: managerName.trim(),
        }),
      );
    } catch (failure) {
      setError(failure instanceof ApiError ? failure.message : 'The dealership was not created.');
    } finally {
      setBusy(false);
    }
  }

  const ready =
    name.trim() !== '' &&
    effectiveSlug !== '' &&
    rooftopCode.trim() !== '' &&
    managerName.trim() !== '' &&
    managerEmail.trim() !== '';

  return (
    <section className="panel">
      <h2>Set up a dealership</h2>
      <p className="note">
        This creates their database, opens their books for this month, and creates
        one manager who then adds everybody else. You will never see or choose
        their password.
      </p>

      <div className="field">
        <label htmlFor="tenant-name">Dealership name</label>
        <input id="tenant-name" value={name} onChange={(e) => setName(e.target.value)} />
      </div>

      <div className="field">
        <label htmlFor="tenant-slug">Short name</label>
        <input
          id="tenant-slug"
          value={slug}
          placeholder={suggested}
          onChange={(e) => setSlug(e.target.value)}
        />
        <p className="hint">
          Lowercase letters, digits and hyphens. Their staff type this to sign in,
          and it cannot be changed afterwards.
        </p>
      </div>

      <div className="row">
        <div className="field field--grow">
          <label htmlFor="tenant-rooftop">First location</label>
          <input
            id="tenant-rooftop"
            value={rooftopName}
            placeholder={name.trim() === '' ? 'Main site' : name.trim()}
            onChange={(e) => setRooftopName(e.target.value)}
          />
        </div>

        <div className="field">
          <label htmlFor="tenant-code">Location code</label>
          <input
            id="tenant-code"
            value={rooftopCode}
            placeholder="MAIN"
            onChange={(e) => setRooftopCode(e.target.value)}
          />
        </div>
      </div>

      <div className="row">
        <div className="field field--grow">
          <label htmlFor="tenant-manager">Manager's name</label>
          <input
            id="tenant-manager"
            value={managerName}
            onChange={(e) => setManagerName(e.target.value)}
          />
        </div>

        <div className="field field--grow">
          <label htmlFor="tenant-email">Manager's email</label>
          <input
            id="tenant-email"
            type="email"
            value={managerEmail}
            onChange={(e) => setManagerEmail(e.target.value)}
          />
        </div>
      </div>

      <p className="error" aria-live="polite">
        {error ?? ''}
      </p>

      <div className="actions">
        <button type="button" className="primary" disabled={busy || !ready} onClick={() => void submit()}>
          {busy ? 'Setting it up…' : 'Set it up'}
        </button>
        <button type="button" onClick={onCancel}>
          Cancel
        </button>
      </div>
    </section>
  );
}
