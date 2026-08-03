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
import type { TenantRow } from '../../shared/contracts';

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; tenants: TenantRow[] }
  | { kind: 'failed'; message: string };

export function TenantsPage() {
  const [load, setLoad] = useState<Load>({ kind: 'loading' });
  const [working, setWorking] = useState<string | null>(null);

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
      </header>

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
