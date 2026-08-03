// SupportAccessPage — opening the one door into a dealership's data, and closing
// it again.
//
// Use:  the console's second screen.
// Edit: three things here are safeguards rather than decoration, and each has a
//       test. The reason field is required before the button does anything. The
//       requested minutes are shown as "an hour at most" because the server
//       clamps regardless of what is asked, and a form that implies otherwise
//       lies. And the list is the installation's whole record — closed visits
//       stay visible, because a log you can empty is not a log.
//
//       Opening a visit puts *dealership* cookies in this browser alongside the
//       administrator's. That is why the two clients are separate; see adminApi.

import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { ApiError, adminApi, adminPost } from '../../shared/adminApi';
import type { GrantedSupportAccess, SupportAccessRecord, TenantRow } from '../../shared/contracts';

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; grants: SupportAccessRecord[] }
  | { kind: 'failed'; message: string };

export function SupportAccessPage() {
  const [load, setLoad] = useState<Load>({ kind: 'loading' });
  const [tenants, setTenants] = useState<TenantRow[]>([]);

  const [tenant, setTenant] = useState('');
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [opened, setOpened] = useState<GrantedSupportAccess | null>(null);

  const fetchGrants = useCallback(async () => {
    try {
      setLoad({ kind: 'ready', grants: await adminApi<SupportAccessRecord[]>('/support-access') });
    } catch (failure) {
      setLoad({
        kind: 'failed',
        message: failure instanceof ApiError ? failure.message : 'Could not load the record.',
      });
    }
  }, []);

  useEffect(() => {
    void fetchGrants();
    void adminApi<TenantRow[]>('/tenants')
      .then(setTenants)
      .catch(() => {
        // The list is a convenience for picking one; the form still works if it
        // fails, because the key can be typed.
      });
  }, [fetchGrants]);

  async function open(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setOpened(null);
    setBusy(true);

    try {
      setOpened(
        await adminPost<GrantedSupportAccess>(
          '/support-access',
          { reason, minutes: 60 },
          tenant,
        ),
      );

      setReason('');
      await fetchGrants();
    } catch (failure) {
      setError(failure instanceof ApiError ? failure.message : 'That did not work.');
    } finally {
      setBusy(false);
    }
  }

  async function end(grant: SupportAccessRecord) {
    setError(null);
    setBusy(true);

    try {
      await adminPost(`/support-access/${grant.id}/end`, {}, grant.tenantSlug);
      setOpened(null);
      await fetchGrants();
    } catch (failure) {
      setError(failure instanceof ApiError ? failure.message : 'That did not work.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <header className="page__head">
        <h1>Support access</h1>
      </header>

      <section className="panel">
        <h2>Enter a dealership</h2>
        <p>
          You will be able to read their records and change nothing, for an hour
          at most. They see this in their own log, with your name and the reason
          you give here.
        </p>

        <form onSubmit={(e) => void open(e)} noValidate>
          <label htmlFor="support-tenant">Dealership</label>
          <input
            id="support-tenant"
            name="tenant"
            list="support-tenants"
            value={tenant}
            onChange={(e) => setTenant(e.target.value)}
            required
          />
          <datalist id="support-tenants">
            {tenants.map((t) => (
              <option key={t.slug} value={t.slug}>
                {t.name}
              </option>
            ))}
          </datalist>

          <label htmlFor="support-reason">Why you need to go in</label>
          <textarea
            id="support-reason"
            name="reason"
            rows={3}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            required
          />
          <p className="note">
            This is recorded permanently, in their log as well as ours. Write
            what you would be willing to have them read.
          </p>

          <p className="error" aria-live="polite">
            {error ?? ''}
          </p>

          <button type="submit" disabled={busy || reason.trim() === '' || tenant.trim() === ''}>
            {busy ? 'Opening…' : 'Open access'}
          </button>
        </form>

        {opened === null ? null : (
          <p className="verdict verdict--ok" role="status">
            You are in {opened.tenant} until{' '}
            {new Date(opened.expiresAt).toLocaleTimeString()}. Open the
            dealership’s screens in this browser to look; close the visit below
            when you are done.
          </p>
        )}
      </section>

      <h2>The record</h2>

      {load.kind === 'loading' ? (
        <p className="state" aria-live="polite">
          Loading the record…
        </p>
      ) : null}

      {load.kind === 'failed' ? (
        <div className="state" role="alert">
          <p>{load.message}</p>
          <button type="button" onClick={() => void fetchGrants()}>
            Try again
          </button>
        </div>
      ) : null}

      {load.kind === 'ready' && load.grants.length === 0 ? (
        <p className="state">Nobody has been into a dealership yet.</p>
      ) : null}

      {load.kind === 'ready' && load.grants.length > 0 ? (
        <div className="scroll">
          <table>
            <caption className="visually-hidden">
              {load.grants.length} support visits, newest first
            </caption>
            <thead>
              <tr>
                <th scope="col">Dealership</th>
                <th scope="col">Who</th>
                <th scope="col">Why</th>
                <th scope="col">Opened</th>
                <th scope="col">State</th>
              </tr>
            </thead>
            <tbody>
              {load.grants.map((grant) => (
                <tr key={grant.id}>
                  <td className="mono">{grant.tenantSlug}</td>
                  <td>{grant.administratorEmail}</td>
                  <td>{grant.reason}</td>
                  <td>{new Date(grant.grantedAt).toLocaleString()}</td>
                  <td>
                    {grant.isActive ? (
                      <button type="button" disabled={busy} onClick={() => void end(grant)}>
                        Close now
                      </button>
                    ) : (
                      <span className="muted">
                        {grant.endedAt === null
                          ? 'Expired'
                          : `Closed ${new Date(grant.endedAt).toLocaleString()}`}
                      </span>
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
