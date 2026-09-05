// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   CustomersPage — finding a customer, and adding one without making a second
//   copy of somebody who is already here.
//
// Usage:
//   Reachable at /customers. The page a receptionist opens fifty times a day.
//
// Coding Instructions:
//   The rule worth protecting is that **adding somebody goes through a
//   duplicate check first**. Two records for the same person is the failure
//   that quietly makes a dealer management system untrustworthy: their
//   service history splits, their deals sit under the wrong name, and
//   nobody notices until it matters. The check is not a warning that can be
//   clicked past without reading — it shows who it found and makes somebody
//   choose.
//
//   It is deliberately not a hard refusal. Two people genuinely do share a
//   name, and a shop that cannot record the second one will get a fake name
//   typed in instead. The screen's job is to make the duplicate obvious, not
//   to decide.

import { useCallback, useEffect, useState } from 'react';
import { ApiError, api, post } from '../../shared/api';
import { useDebounced } from '../../shared/useDebounced';
import type { CustomerDetail, CustomerSummary, NewCustomer } from '../../shared/contracts';
import { useI18n } from '../../shared/i18n';
import { useEnumLabel } from '../../shared/i18n/enums';
import { useApiMessage } from '../../shared/i18n/apiMessage';

/**
 * What the server will return at most, however many are asked for. The screen
 * has to know: a full page is indistinguishable from "that is everybody", and
 * saying the wrong one puts a false number in front of somebody.
 */
const PageSize = 100;

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; customers: CustomerSummary[] }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

/** The add form's state machine. `checking` is the duplicate look-up. */
type Adding =
  | { step: 'closed' }
  | { step: 'form' }
  | { step: 'checking' }
  | { step: 'confirm'; matches: CustomerSummary[] }
  | { step: 'saving' };

const empty: NewCustomer = { kind: 'Person', firstName: '', lastName: '', email: '', phone: '' };

export function CustomersPage() {
  const { t } = useI18n();
  const describe = useApiMessage();

  const [search, setSearch] = useState('');
  const [load, setLoad] = useState<Load>({ kind: 'loading' });

  const [selected, setSelected] = useState<CustomerDetail | null>(null);

  const [adding, setAdding] = useState<Adding>({ step: 'closed' });
  const [draft, setDraft] = useState<NewCustomer>(empty);
  const [error, setError] = useState<string | null>(null);

  const find = useCallback(async (term: string, signal?: AbortSignal) => {
    setLoad({ kind: 'loading' });

    try {
      const query = term.trim() === ''
        ? `?limit=${PageSize}`
        : `?search=${encodeURIComponent(term.trim())}&limit=${PageSize}`;

      setLoad({
        kind: 'ready',
        customers: await api<CustomerSummary[]>(`/customers${query}`, { signal }),
      });
    } catch (failure) {
      // Superseded by a later keystroke. Not a failure, and the request that
      // replaced this one is already showing its own loading state.
      if (failure instanceof DOMException && failure.name === 'AbortError') {
        return;
      }

      if (failure instanceof ApiError && failure.status === 403) {
        setLoad({ kind: 'denied' });
        return;
      }

      setLoad({ kind: 'failed', message: describe(failure) });
    }
  }, [describe]);

  // The list follows the box. Aborting on cleanup is what stops a slow answer
  // for "f" landing after the right answer for "focus" and overwriting it —
  // see useDebounced for why debouncing alone would only make that rarer.
  const settled = useDebounced(search);

  useEffect(() => {
    const stop = new AbortController();
    void find(settled, stop.signal);
    return () => stop.abort();
  }, [find, settled]);

  async function open(customerId: string) {
    try {
      setSelected(await api<CustomerDetail>(`/customers/${customerId}`));
    } catch (failure) {
      setLoad({ kind: 'failed', message: describe(failure) });
    }
  }

  /**
   * Looks for anybody who might already be this person before creating them.
   * Searches on each of the things that identify somebody separately, because a
   * duplicate usually differs in one of them — the phone matches but the name is
   * spelled differently, or the surname matches but they used a work email.
   */
  async function checkForDuplicates() {
    setError(null);
    setAdding({ step: 'checking' });

    const terms = [draft.lastName, draft.email, draft.phone]
      .map((t) => (t ?? '').trim())
      .filter((t) => t.length > 0);

    try {
      const found = new Map<string, CustomerSummary>();

      for (const term of terms) {
        const matches = await api<CustomerSummary[]>(
          `/customers?search=${encodeURIComponent(term)}&limit=10`,
        );

        for (const match of matches) {
          found.set(match.id, match);
        }
      }

      if (found.size === 0) {
        await save();
        return;
      }

      setAdding({ step: 'confirm', matches: [...found.values()] });
    } catch (failure) {
      setError(describe(failure));
      setAdding({ step: 'form' });
    }
  }

  async function save() {
    setError(null);
    setAdding({ step: 'saving' });

    try {
      await post('/customers', {
        kind: draft.kind,
        firstName: draft.kind === 'Business' ? null : draft.firstName,
        lastName: draft.lastName,
        email: draft.email === '' ? null : draft.email,
        phone: draft.phone === '' ? null : draft.phone,
      });

      setDraft(empty);
      setAdding({ step: 'closed' });
      await find(search);
    } catch (failure) {
      setError(describe(failure));
      setAdding({ step: 'form' });
    }
  }

  const busy = adding.step === 'checking' || adding.step === 'saving';

  return (
    <>
      <header className="page__head">
        <h1>{t('customers.title')}</h1>

        {/* Still a form, so Enter does the obvious thing — but Enter is no
            longer how you search. It only prevents the page reloading for
            somebody who presses it out of habit. */}
        <form className="filter" onSubmit={(e) => e.preventDefault()}>
          <label htmlFor="search">{t('customers.find')}</label>
          <input
            id="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder={t('customers.findPlaceholder')}
            // Not type="search": the browser's clear button does not fire an
            // input event in every engine, so the list could disagree with the box.
            autoComplete="off"
          />
        </form>
      </header>

      {adding.step === 'closed' ? (
        <div className="actions actions--lead">
          <button type="button" className="primary" onClick={() => setAdding({ step: 'form' })}>
            {t('customers.add')}
          </button>
        </div>
      ) : (
        <AddPanel
          adding={adding}
          draft={draft}
          error={error}
          busy={busy}
          onDraft={setDraft}
          onCheck={() => void checkForDuplicates()}
          onSaveAnyway={() => void save()}
          onCancel={() => {
            setAdding({ step: 'closed' });
            setDraft(empty);
            setError(null);
          }}
        />
      )}

      <Results
        load={load}
        onRetry={() => void find(search)}
        selectedId={selected?.id ?? null}
        onOpen={(id) => void open(id)}
      />

      {selected === null ? null : (
        <CustomerPanel customer={selected} onClose={() => setSelected(null)} />
      )}
    </>
  );
}

/**
 * ADR-020's detail band: the selected customer, inline, below the results.
 *
 * The zero-jump rule matters more here than anywhere. A receptionist opens this
 * screen fifty times a day with somebody on the telephone; a route per customer
 * would throw away the search term they just typed every time they looked at a
 * record, and they would type it again with the caller waiting.
 */
function CustomerPanel({
  customer, onClose,
}: {
  customer: CustomerDetail;
  onClose: () => void;
}) {
  const { t } = useI18n();
  const label = useEnumLabel();

  const address = customer.address;

  return (
    <section className="panel panel--detail" aria-label={customer.displayName}>
      <h2>{customer.displayName}</h2>

      <dl className="facts">
        <dt>{t('customers.colKind')}</dt>
        <dd>
          <span className={`chip chip--${customer.kind.toLowerCase()}`}>
            {label('customerKind', customer.kind)}
          </span>
        </dd>

        {customer.externalReference === null ? null : (
          <>
            <dt>{t('customers.cameFrom')}</dt>
            {/* An identifier from another system: a code, so it reads left to
                right however the page runs. */}
            <dd className="mono" dir="ltr">{customer.externalReference}</dd>
          </>
        )}
      </dl>

      <h3>{t('customers.waysToReach')}</h3>
      {customer.contactPoints.length === 0 ? (
        <p className="note">{t('customers.noContactDetails')}</p>
      ) : (
        <ul className="history" aria-label={t('customers.waysToReach')}>
          {customer.contactPoints.map((point) => (
            <li key={point.id}>
              {/* An email address and a telephone number both read left to
                  right, whichever way the page runs. */}
              <span className="strong" dir="ltr">{point.value}</span>{' '}
              <span className="muted">
                {label('contactKind', point.kind)}
                {point.isPrimary ? ` · ${t('customers.primary')}` : ''}
              </span>
            </li>
          ))}
        </ul>
      )}

      <h3>{t('customers.address')}</h3>
      {address === null ? (
        <p className="note">{t('customers.noAddress')}</p>
      ) : (
        <p className="note">
          {[
            address.line1,
            address.line2,
            address.city,
            address.administrativeArea,
            address.county,
            address.postalCode,
            address.country,
          ]
            .filter((part) => part !== null && part !== '')
            .join(', ')}
        </p>
      )}

      <div className="actions">
        <button type="button" onClick={onClose}>
          {t('common.close')}
        </button>
      </div>
    </section>
  );
}

function AddPanel({
  adding, draft, error, busy, onDraft, onCheck, onSaveAnyway, onCancel,
}: {
  adding: Adding;
  draft: NewCustomer;
  error: string | null;
  busy: boolean;
  onDraft: (next: NewCustomer) => void;
  onCheck: () => void;
  onSaveAnyway: () => void;
  onCancel: () => void;
}) {
  const { t } = useI18n();

  if (adding.step === 'confirm') {
    return (
      <section className="panel">
        <h2>{t('customers.duplicateTitle')}</h2>
        <p role="alert">{t('customers.duplicateLede')}</p>

        <ul className="matches">
          {adding.matches.map((match) => (
            <li key={match.id}>
              <span className="strong">{match.displayName}</span>{' '}
              <span className="muted">
                {[match.primaryEmail, match.primaryPhone].filter(Boolean).join(' · ') ||
                  t('customers.noContactDetails')}
              </span>
            </li>
          ))}
        </ul>

        <div className="actions">
          <button type="button" className="primary" onClick={onCancel}>
            {t('customers.oneOfTheseIsThem')}
          </button>
          <button type="button" onClick={onSaveAnyway} disabled={busy}>
            {busy ? t('customers.adding') : t('customers.addAnyway')}
          </button>
        </div>
      </section>
    );
  }

  return (
    <section className="panel">
      <h2>{t('customers.add')}</h2>

      <form
        onSubmit={(e) => {
          e.preventDefault();
          onCheck();
        }}
        noValidate
      >
        <label htmlFor="customer-kind">{t('customers.kindLabel')}</label>
        <select
          id="customer-kind"
          value={draft.kind}
          onChange={(e) => onDraft({ ...draft, kind: e.target.value as NewCustomer['kind'] })}
        >
          <option value="Person">{t('enum.customerKind.Person')}</option>
          <option value="Business">{t('enum.customerKind.Business')}</option>
        </select>

        {draft.kind === 'Person' ? (
          <>
            <label htmlFor="first-name">{t('customers.firstName')}</label>
            <input
              id="first-name"
              value={draft.firstName ?? ''}
              onChange={(e) => onDraft({ ...draft, firstName: e.target.value })}
            />
          </>
        ) : null}

        <label htmlFor="last-name">
          {draft.kind === 'Business' ? t('customers.businessName') : t('customers.lastName')}
        </label>
        <input
          id="last-name"
          value={draft.lastName}
          onChange={(e) => onDraft({ ...draft, lastName: e.target.value })}
          required
        />

        <label htmlFor="email">{t('customers.email')}</label>
        <input
          id="email"
          type="email"
          value={draft.email ?? ''}
          onChange={(e) => onDraft({ ...draft, email: e.target.value })}
        />

        <label htmlFor="phone">{t('customers.phone')}</label>
        <input
          id="phone"
          value={draft.phone ?? ''}
          onChange={(e) => onDraft({ ...draft, phone: e.target.value })}
        />

        <p className="error" aria-live="polite">
          {error ?? ''}
        </p>

        <div className="actions">
          <button
            type="submit"
            className="primary"
            disabled={busy || draft.lastName.trim() === ''}
          >
            {adding.step === 'checking'
              ? t('customers.checking')
              : adding.step === 'saving'
                ? t('customers.adding')
                : t('customers.submit')}
          </button>
          <button type="button" onClick={onCancel} disabled={busy}>
            {t('common.cancel')}
          </button>
        </div>
      </form>
    </section>
  );
}

function Results({
  load, onRetry, selectedId, onOpen,
}: {
  load: Load;
  onRetry: () => void;
  selectedId: string | null;
  onOpen: (customerId: string) => void;
}) {
  const { t } = useI18n();

  switch (load.kind) {
    case 'loading':
      return (
        <p className="state" aria-live="polite">
          {t('customers.looking')}
        </p>
      );

    case 'denied':
      return (
        <p className="state" role="alert">
          {t('customers.denied')}
        </p>
      );

    case 'failed':
      return (
        <div className="state" role="alert">
          <p>{load.message}</p>
          <button type="button" onClick={onRetry}>
            {t('common.retry')}
          </button>
        </div>
      );

    case 'ready':
      return load.customers.length === 0 ? (
        <p className="state">{t('customers.noMatches')}</p>
      ) : (
        <CustomerTable customers={load.customers} selectedId={selectedId} onOpen={onOpen} />
      );
  }
}

function CustomerTable({
  customers, selectedId, onOpen,
}: {
  customers: CustomerSummary[];
  selectedId: string | null;
  onOpen: (customerId: string) => void;
}) {
  const { t } = useI18n();
  const label = useEnumLabel();

  // A full page probably means there are more, and we cannot know how many.
  const capped = customers.length >= PageSize;

  return (
    <div className="scroll">
      <table>
        <caption className="visually-hidden">
          {capped
            ? t('customers.countCapped', { count: customers.length })
            : t('customers.count', { count: customers.length })}
        </caption>
        <thead>
          <tr>
            <th scope="col">{t('customers.colName')}</th>
            <th scope="col">{t('customers.colKind')}</th>
            <th scope="col">{t('customers.colEmail')}</th>
            <th scope="col">{t('customers.colPhone')}</th>
          </tr>
        </thead>
        <tbody>
          {customers.map((customer) => (
            <tr key={customer.id} aria-selected={customer.id === selectedId}>
              <td>
                {/* A button and not a clickable row: a <tr> with an onClick is
                    unreachable by keyboard and announces nothing. */}
                <button type="button" className="cell-open" onClick={() => onOpen(customer.id)}>
                  {customer.displayName}
                </button>
              </td>
              <td>
                <span className={`chip chip--${customer.kind.toLowerCase()}`}>
                  {label('customerKind', customer.kind)}
                </span>
              </td>
              {/* An email address and a phone number are both read left to
                  right, whichever way the page runs. */}
              <td dir="ltr">{customer.primaryEmail ?? <span className="muted">—</span>}</td>
              <td className="mono" dir="ltr">
                {customer.primaryPhone ?? <span className="muted">—</span>}
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {capped ? (
        <p className="note note--footer">
          {t('customers.cappedNote', { count: customers.length })}
        </p>
      ) : null}
    </div>
  );
}
