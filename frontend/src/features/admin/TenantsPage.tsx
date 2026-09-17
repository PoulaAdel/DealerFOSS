// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   TenantsPage — which dealerships exist, and whether each is in service.
//
// Usage:
//   The console's landing screen.
//
// Coding Instructions:
//   Look at what a row carries — a name, a key, a state, a schema version,
//   a date. Nothing about the dealership's business, because that is the
//   whole point of this screen existing separately from the product.
//
//   Suspending is a real outage for a real dealership, so it asks first.
//   Confirmation lives here rather than in the API: the server is right to
//   do exactly what a properly authorized request tells it to.

import { useCallback, useEffect, useState } from 'react';
import { adminApi, adminPost } from '../../shared/adminApi';
import { Confirm } from '../../shared/Confirm';
import type { Page, ProvisionedTenant, TenantRow } from '../../shared/contracts';
import { useI18n } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';
import { useEnumLabel } from '../../shared/i18n/enums';
import { Emphasised } from '../../shared/i18n/Emphasised';

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; tenants: TenantRow[] }
  | { kind: 'failed'; message: string };

export function TenantsPage() {
  const { t } = useI18n();
  const describe = useApiMessage();
  const label = useEnumLabel();

  const [load, setLoad] = useState<Load>({ kind: 'loading' });
  const [working, setWorking] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [provisioned, setProvisioned] = useState<ProvisionedTenant | null>(null);
  const [suspending, setSuspending] = useState<TenantRow | null>(null);

  const fetchTenants = useCallback(async () => {
    setLoad({ kind: 'loading' });

    try {
      // Only the rows are kept: the control plane pages this list like every
      // other, but an operator with more dealerships than one page holds is a
      // problem worth having and not one this screen has yet.
      setLoad({
        kind: 'ready',
        tenants: (await adminApi<Page<TenantRow>>('/tenants?limit=200')).rows,
      });
    } catch (failure) {
      setLoad({ kind: 'failed', message: describe(failure) });
    }
  }, [describe]);

  useEffect(() => {
    void fetchTenants();
  }, [fetchTenants]);

  async function setStatus(tenant: TenantRow, status: 'Active' | 'Suspended') {
    setSuspending(null);
    setWorking(tenant.slug);

    try {
      await adminPost(`/tenants/${encodeURIComponent(tenant.slug)}/status`, { status });
      await fetchTenants();
    } catch (failure) {
      setLoad({ kind: 'failed', message: describe(failure) });
    } finally {
      setWorking(null);
    }
  }

  return (
    <>
      <header className="page__head">
        <h1>{t('admin.dealerships')}</h1>
        {creating ? null : (
          <button type="button" className="primary" onClick={() => setCreating(true)}>
            {t('admin.setUpDealership')}
          </button>
        )}
      </header>

      {provisioned === null ? null : (
        <section className="panel panel--code" aria-live="polite">
          <h2>{t('admin.dealershipReady', { name: provisioned.name })}</h2>
          {/* The email is picked out of the FINISHED sentence rather than
              assembled around a <strong> in JSX — German and Arabic both move
              it, and the emphasis has to follow it there. */}
          <p>
            <Emphasised
              sentence={t('admin.firstManager', { email: provisioned.managerEmail })}
              value={provisioned.managerEmail}
            />
          </p>
          <p className="code" dir="ltr">
            {provisioned.enrolmentCode}
          </p>
          <p className="note">
            <strong>{t('admin.codeShownOnce')}</strong> {t('admin.codeShownOnceWhy')}
          </p>
          <button type="button" onClick={() => setProvisioned(null)}>
            {t('admin.passedItOn')}
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
          {t('admin.loadingDealerships')}
        </p>
      ) : null}

      {load.kind === 'failed' ? (
        <div className="state" role="alert">
          <p>{load.message}</p>
          <button type="button" onClick={() => void fetchTenants()}>
            {t('common.retry')}
          </button>
        </div>
      ) : null}

      {load.kind === 'ready' && load.tenants.length === 0 ? (
        <p className="state">{t('admin.noDealerships')}</p>
      ) : null}

      {load.kind === 'ready' && load.tenants.length > 0 ? (
        <div className="scroll">
          <table>
            <caption className="visually-hidden">
              {t('admin.dealershipsCaption', { count: load.tenants.length })}
            </caption>
            <thead>
              <tr>
                <th scope="col">{t('admin.colName')}</th>
                <th scope="col">{t('admin.colKey')}</th>
                <th scope="col">{t('admin.colStatus')}</th>
                <th scope="col">{t('admin.colSchema')}</th>
                <th scope="col">{t('admin.colInService')}</th>
              </tr>
            </thead>
            <tbody>
              {load.tenants.map((tenant) => (
                <tr key={tenant.slug}>
                  <td>{tenant.name}</td>
                  {/* The key travels in a header on every request and the schema
                      version is a dotted number. Both are codes, not prose. */}
                  <td className="mono" dir="ltr">
                    {tenant.slug}
                  </td>
                  <td>
                    {/* The class keys off the raw value, the word the reader
                        sees comes from the catalogue. */}
                    <span className={`chip chip--${tenant.status.toLowerCase()}`}>
                      {label('tenantStatus', tenant.status)}
                    </span>
                  </td>
                  <td className="mono" dir="ltr">
                    {tenant.databaseVersion}
                  </td>
                  <td>
                    {tenant.status === 'Suspended' ? (
                      <button
                        type="button"
                        disabled={working === tenant.slug}
                        onClick={() => void setStatus(tenant, 'Active')}
                      >
                        {t('admin.resume')}
                      </button>
                    ) : (
                      <button
                        type="button"
                        disabled={working === tenant.slug}
                        onClick={() => setSuspending(tenant)}
                      >
                        {t('admin.suspend')}
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}

      {suspending === null ? null : (
        <Confirm
          title={t('admin.suspendTitle', { name: suspending.name })}
          body={t('admin.suspendConfirm')}
          confirmLabel={t('admin.suspend')}
          typeToConfirm={suspending.name}
          busy={working === suspending.slug}
          onConfirm={() => void setStatus(suspending, 'Suspended')}
          onCancel={() => setSuspending(null)}
        />
      )}
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
  const { t } = useI18n();
  const describe = useApiMessage();

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
      setError(describe(failure));
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
      <h2>{t('admin.setUpDealership')}</h2>
      <p className="note">{t('admin.setUpNote')}</p>

      <div className="field">
        <label htmlFor="tenant-name">{t('admin.dealershipName')}</label>
        <input id="tenant-name" value={name} onChange={(e) => setName(e.target.value)} />
      </div>

      <div className="field">
        <label htmlFor="tenant-slug">{t('admin.shortName')}</label>
        {/* Lowercase ASCII by rule, and it becomes part of a database name. */}
        <input
          id="tenant-slug"
          value={slug}
          placeholder={suggested}
          onChange={(e) => setSlug(e.target.value)}
          dir="ltr"
        />
        <p className="hint">{t('admin.shortNameHint')}</p>
      </div>

      <div className="row">
        <div className="field field--grow">
          <label htmlFor="tenant-rooftop">{t('admin.firstLocation')}</label>
          <input
            id="tenant-rooftop"
            value={rooftopName}
            placeholder={name.trim() === '' ? t('admin.firstLocationPlaceholder') : name.trim()}
            onChange={(e) => setRooftopName(e.target.value)}
          />
        </div>

        <div className="field">
          <label htmlFor="tenant-code">{t('admin.locationCode')}</label>
          <input
            id="tenant-code"
            value={rooftopCode}
            placeholder="MAIN"
            onChange={(e) => setRooftopCode(e.target.value)}
            dir="ltr"
          />
        </div>
      </div>

      <div className="row">
        <div className="field field--grow">
          <label htmlFor="tenant-manager">{t('admin.managerName')}</label>
          <input
            id="tenant-manager"
            value={managerName}
            onChange={(e) => setManagerName(e.target.value)}
          />
        </div>

        <div className="field field--grow">
          <label htmlFor="tenant-email">{t('admin.managerEmail')}</label>
          <input
            id="tenant-email"
            type="email"
            value={managerEmail}
            onChange={(e) => setManagerEmail(e.target.value)}
            dir="ltr"
          />
        </div>
      </div>

      <p className="error" aria-live="polite">
        {error ?? ''}
      </p>

      <div className="actions">
        <button
          type="button"
          className="primary"
          disabled={busy || !ready}
          onClick={() => void submit()}
        >
          {busy ? t('admin.settingItUp') : t('admin.setItUp')}
        </button>
        <button type="button" onClick={onCancel}>
          {t('common.cancel')}
        </button>
      </div>
    </section>
  );
}
