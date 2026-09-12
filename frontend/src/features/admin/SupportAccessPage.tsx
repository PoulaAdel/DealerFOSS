// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   SupportAccessPage — opening the one door into a dealership's data, and closing
//   it again.
//
// Usage:
//   The console's second screen.
//
// Coding Instructions:
//   Three things here are safeguards rather than decoration, and each has a
//   test. The reason field is required before the button does anything. The
//   requested minutes are shown as "an hour at most" because the server
//   clamps regardless of what is asked, and a form that implies otherwise
//   lies. And the list is the installation's whole record — closed visits
//   stay visible, because a log you can empty is not a log.
//
//   Opening a visit puts *dealership* cookies in this browser alongside the
//   administrator's. That is why the two clients are separate; see adminApi.

import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { adminApi, adminPost } from '../../shared/adminApi';
import type { GrantedSupportAccess, Page, SupportAccessRecord, TenantRow } from '../../shared/contracts';
import { useI18n } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; grants: SupportAccessRecord[] }
  | { kind: 'failed'; message: string };

export function SupportAccessPage() {
  const { t, format } = useI18n();
  const describe = useApiMessage();

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
      setLoad({ kind: 'failed', message: describe(failure) });
    }
  }, [describe]);

  useEffect(() => {
    void fetchGrants();
    void adminApi<Page<TenantRow>>('/tenants?limit=200')
      .then((listed) => setTenants(listed.rows))
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
        await adminPost<GrantedSupportAccess>('/support-access', { reason, minutes: 60 }, tenant),
      );

      setReason('');
      await fetchGrants();
    } catch (failure) {
      setError(describe(failure));
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
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <header className="page__head">
        <h1>{t('admin.supportAccess')}</h1>
      </header>

      <section className="panel">
        <h2>{t('admin.supportEnter')}</h2>
        <p>{t('admin.supportLede')}</p>

        <form onSubmit={(e) => void open(e)} noValidate>
          <label htmlFor="support-tenant">{t('admin.supportDealership')}</label>
          {/* The dealership key, not its name — lowercase ASCII either way. */}
          <input
            id="support-tenant"
            name="tenant"
            list="support-tenants"
            value={tenant}
            onChange={(e) => setTenant(e.target.value)}
            dir="ltr"
            required
          />
          <datalist id="support-tenants">
            {tenants.map((candidate) => (
              <option key={candidate.slug} value={candidate.slug}>
                {candidate.name}
              </option>
            ))}
          </datalist>

          <label htmlFor="support-reason">{t('admin.supportReason')}</label>
          <textarea
            id="support-reason"
            name="reason"
            rows={3}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            required
          />
          <p className="note">{t('admin.supportReasonNote')}</p>

          <p className="error" aria-live="polite">
            {error ?? ''}
          </p>

          <button type="submit" disabled={busy || reason.trim() === '' || tenant.trim() === ''}>
            {busy ? t('admin.supportOpening') : t('admin.supportOpen')}
          </button>
        </form>

        {opened === null ? null : (
          <p className="verdict verdict--ok" role="status">
            {/* format.time, not toLocaleTimeString: the latter follows the
                operating system, and the reader chose a language here. */}
            {t('admin.supportOpened', {
              tenant: opened.tenant,
              time: format.time(opened.expiresAt),
            })}
          </p>
        )}
      </section>

      <h2>{t('admin.supportRecord')}</h2>

      {load.kind === 'loading' ? (
        <p className="state" aria-live="polite">
          {t('admin.supportLoading')}
        </p>
      ) : null}

      {load.kind === 'failed' ? (
        <div className="state" role="alert">
          <p>{load.message}</p>
          <button type="button" onClick={() => void fetchGrants()}>
            {t('common.retry')}
          </button>
        </div>
      ) : null}

      {load.kind === 'ready' && load.grants.length === 0 ? (
        <p className="state">{t('admin.supportEmpty')}</p>
      ) : null}

      {load.kind === 'ready' && load.grants.length > 0 ? (
        <div className="scroll">
          <table>
            <caption className="visually-hidden">
              {t('admin.supportCaption', { count: load.grants.length })}
            </caption>
            <thead>
              <tr>
                <th scope="col">{t('admin.supportDealership')}</th>
                <th scope="col">{t('admin.supportColWho')}</th>
                <th scope="col">{t('admin.supportColWhy')}</th>
                <th scope="col">{t('admin.supportColOpened')}</th>
                <th scope="col">{t('admin.supportColState')}</th>
              </tr>
            </thead>
            <tbody>
              {load.grants.map((grant) => (
                <tr key={grant.id}>
                  <td className="mono" dir="ltr">
                    {grant.tenantSlug}
                  </td>
                  <td dir="ltr">{grant.administratorEmail}</td>
                  {/* Whoever typed the reason typed it in their own language.
                      It is a record, so it is printed exactly as stored. */}
                  <td>{grant.reason}</td>
                  <td>{format.dateTime(grant.grantedAt)}</td>
                  <td>
                    {grant.isActive ? (
                      <button type="button" disabled={busy} onClick={() => void end(grant)}>
                        {t('admin.supportCloseNow')}
                      </button>
                    ) : (
                      <span className="muted">
                        {grant.endedAt === null
                          ? t('admin.supportExpired')
                          : t('admin.supportClosed', { date: format.dateTime(grant.endedAt) })}
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
