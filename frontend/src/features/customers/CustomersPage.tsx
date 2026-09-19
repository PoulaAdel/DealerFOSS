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
//
//   The open customer lives at `/customers/:id`. The search box does not: a
//   receptionist types into it fifty times a day and every keystroke would be
//   a history entry, so the back button would become an undo for typing. What
//   goes in the address is what somebody would want to send to a colleague.

import { useCallback, useEffect, useState } from 'react';
import { ApiError, api, post, put } from '../../shared/api';
import { RecordBandStatus } from '../../shared/RecordBand';
import { InlineEdit } from '../../shared/InlineEdit';
import { useRecordRoute } from '../../shared/useRecordRoute';
import { useDebounced } from '../../shared/useDebounced';
import type { AddressView, CustomerDetail, CustomerSummary, NewCustomer, Page } from '../../shared/contracts';
import { useI18n } from '../../shared/i18n';
import { useEnumLabel } from '../../shared/i18n/enums';
import { useApiMessage } from '../../shared/i18n/apiMessage';
import { ListScreen, type ListLoad } from '../../shared/ListScreen';

/**
 * What the server will return at most, however many are asked for. The screen
 * has to know: a full page is indistinguishable from "that is everybody", and
 * saying the wrong one puts a false number in front of somebody.
 */
const PageSize = 100;

type Load = ListLoad<CustomerSummary>;

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
  const label = useEnumLabel();

  const [search, setSearch] = useState('');

  // Which page. Reset on every keystroke, below: page 3 of one search term is
  // not page 3 of another.
  const [offset, setOffset] = useState(0);
  const [load, setLoad] = useState<Load>({ kind: 'loading' });

  const record = useRecordRoute<CustomerDetail>({
    area: '/customers',
    load: (customerId, signal) => api<CustomerDetail>(`/customers/${customerId}`, { signal }),
  });

  const [adding, setAdding] = useState<Adding>({ step: 'closed' });
  const [draft, setDraft] = useState<NewCustomer>(empty);
  const [error, setError] = useState<string | null>(null);

  const find = useCallback(async (term: string, from: number, signal?: AbortSignal) => {
    setLoad({ kind: 'loading' });

    try {
      // The search term leads, so that a request for a term is distinguishable
      // from the unfiltered first load by its prefix alone.
      const filters = term.trim() === ''
        ? []
        : [`search=${encodeURIComponent(term.trim())}`];

      filters.push(`limit=${PageSize}`, `offset=${from}`);

      setLoad({
        kind: 'ready',
        page: await api<Page<CustomerSummary>>(`/customers?${filters.join('&')}`, { signal }),
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
    void find(settled, offset, stop.signal);
    return () => stop.abort();
  }, [find, settled, offset]);

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
        const matches = await api<Page<CustomerSummary>>(
          `/customers?search=${encodeURIComponent(term)}&limit=10`,
        );

        for (const match of matches.rows) {
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
      await find(search, offset);
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
            onChange={(e) => {
              setSearch(e.target.value);
              setOffset(0);
            }}
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

      <ListScreen
        load={load}
        onRetry={() => void find(search, offset)}
        onPage={setOffset}
        loadingMessage={t('customers.looking')}
        deniedMessage={t('customers.denied')}
        emptyMessage={t('customers.noMatches')}
        columns={
          <>
            <th scope="col">{t('customers.colName')}</th>
            <th scope="col">{t('customers.colKind')}</th>
            <th scope="col">{t('customers.colEmail')}</th>
            <th scope="col">{t('customers.colPhone')}</th>
          </>
        }
        row={(customer) => (
          <tr key={customer.id} aria-selected={customer.id === record.openId}>
            <td>
              {/* A button and not a clickable row: a <tr> with an onClick is
                  unreachable by keyboard and announces nothing. */}
              <button type="button" className="cell-open" onClick={() => record.open(customer.id)}>
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
        )}
      />

      <RecordBandStatus route={record} />

      {record.state.kind !== 'open' ? null : (
        <CustomerPanel
          customer={record.state.record}
          onClose={record.close}
          onChanged={record.refresh}
        />
      )}
    </>
  );
}

/**
 * ADR-020's detail band: the selected customer, inline, below the results.
 *
 * The zero-jump rule matters more here than anywhere. A receptionist opens this
 * screen fifty times a day with somebody on the telephone; a screen that
 * REPLACED the results with the customer would throw away the search term they
 * just typed, and they would type it again with the caller waiting.
 *
 * Giving the customer an address does not do that, and the distinction is the
 * whole of the 2026-09-16 amendment to ADR-020: the results stay on screen with
 * their search term intact, and `/customers/:id` only says which of them is
 * open. What it buys is the thing the telephone makes obvious — "I will send
 * you the link" instead of "search for Okonkwo, no, the other one".
 */
function CustomerPanel({
  customer, onClose, onChanged,
}: {
  customer: CustomerDetail;
  onClose: () => void;

  /** The record changed in place after a save — see useRecordRoute.refresh. */
  onChanged: (customer: CustomerDetail) => void;
}) {
  const { t, format } = useI18n();
  const label = useEnumLabel();

  async function saveCreditLimit(next: string) {
    const trimmed = next.trim();
    const limit = trimmed === '' ? null : Number(trimmed);

    if (limit !== null && (Number.isNaN(limit) || limit < 0)) {
      throw new Error('A credit limit is a number of zero or more, or left blank for no limit.');
    }

    onChanged(
      await put<CustomerDetail>(`/customers/${customer.id}/credit-limit`, { limit }),
    );
  }

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

        <dt>{t('customers.creditLimit')}</dt>
        <dd>
          <InlineEdit
            label={t('customers.creditLimit')}
            value={customer.creditLimit === null ? '' : String(customer.creditLimit)}
            display={
              customer.creditLimit === null
                ? t('customers.creditLimitNone')
                : format.number(customer.creditLimit)
            }
            inputMode="decimal"
            onSave={saveCreditLimit}
          />
        </dd>
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

      <CustomerAddress customer={customer} onChanged={onChanged} />

      <div className="actions">
        <button type="button" onClick={onClose}>
          {t('common.close')}
        </button>
      </div>
    </section>
  );
}

/** The address editor's own draft shape — everything a string until it is saved. */
type AddressDraft = {
  line1: string;
  line2: string;
  city: string;
  administrativeArea: string;
  county: string;
  postalCode: string;
  country: string;
};

function draftFrom(address: AddressView | null): AddressDraft {
  return {
    line1: address?.line1 ?? '',
    line2: address?.line2 ?? '',
    city: address?.city ?? '',
    administrativeArea: address?.administrativeArea ?? '',
    county: address?.county ?? '',
    postalCode: address?.postalCode ?? '',
    country: address?.country ?? '',
  };
}

/**
 * The customer's own mailing address — not the registration address a sale is
 * taxed at, which lives on the deal instead and is frozen with it (ADR-024).
 * Idle shows what is on file, or that nothing is; editing replaces it with the
 * seven fields, saved as one call the same way the deal's tax address is.
 */
function CustomerAddress({
  customer, onChanged,
}: {
  customer: CustomerDetail;
  onChanged: (customer: CustomerDetail) => void;
}) {
  const { t } = useI18n();
  const describe = useApiMessage();

  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState<AddressDraft>(() => draftFrom(customer.address));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function startEditing() {
    setDraft(draftFrom(customer.address));
    setError(null);
    setEditing(true);
  }

  async function save(address: AddressView | null) {
    setError(null);
    setBusy(true);

    try {
      onChanged(await put<CustomerDetail>(`/customers/${customer.id}/address`, { address }));
      setEditing(false);
    } catch (failure) {
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  const address = customer.address;

  if (!editing) {
    return (
      <>
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
          <button type="button" onClick={startEditing}>
            {address === null ? t('customers.addAddress') : t('customers.editAddress')}
          </button>
        </div>
      </>
    );
  }

  return (
    <>
      <h3>{t('customers.address')}</h3>
      <fieldset>
        <legend className="visually-hidden">{t('customers.address')}</legend>

        <label htmlFor="address-line1">{t('customers.addressLine1')}</label>
        <input
          id="address-line1"
          value={draft.line1}
          disabled={busy}
          onChange={(e) => setDraft({ ...draft, line1: e.target.value })}
        />

        <label htmlFor="address-line2">{t('customers.addressLine2')}</label>
        <input
          id="address-line2"
          value={draft.line2}
          disabled={busy}
          onChange={(e) => setDraft({ ...draft, line2: e.target.value })}
        />

        <label htmlFor="address-city">{t('customers.addressCity')}</label>
        <input
          id="address-city"
          value={draft.city}
          disabled={busy}
          onChange={(e) => setDraft({ ...draft, city: e.target.value })}
        />

        <label htmlFor="address-area">{t('customers.addressArea')}</label>
        <input
          id="address-area"
          value={draft.administrativeArea}
          disabled={busy}
          onChange={(e) => setDraft({ ...draft, administrativeArea: e.target.value })}
        />

        {/* Its own field, not folded into the state: US sales tax varies by
            county too, and this is what feeds the deal's registration address. */}
        <label htmlFor="address-county">{t('customers.addressCounty')}</label>
        <input
          id="address-county"
          value={draft.county}
          disabled={busy}
          onChange={(e) => setDraft({ ...draft, county: e.target.value })}
        />

        <label htmlFor="address-postal">{t('customers.addressPostalCode')}</label>
        <input
          id="address-postal"
          value={draft.postalCode}
          disabled={busy}
          onChange={(e) => setDraft({ ...draft, postalCode: e.target.value })}
        />

        <label htmlFor="address-country">{t('customers.addressCountry')}</label>
        <input
          id="address-country"
          value={draft.country}
          disabled={busy}
          maxLength={2}
          onChange={(e) => setDraft({ ...draft, country: e.target.value })}
        />
      </fieldset>

      <p className="error" aria-live="polite">
        {error ?? ''}
      </p>

      <div className="actions">
        <button
          type="button"
          className="primary"
          disabled={busy}
          onClick={() =>
            void save({
              line1: draft.line1.trim(),
              line2: draft.line2.trim() === '' ? null : draft.line2.trim(),
              city: draft.city.trim(),
              administrativeArea: draft.administrativeArea.trim() === '' ? null : draft.administrativeArea.trim(),
              county: draft.county.trim() === '' ? null : draft.county.trim(),
              postalCode: draft.postalCode.trim() === '' ? null : draft.postalCode.trim(),
              country: draft.country.trim().toUpperCase(),
            })
          }
        >
          {busy ? t('common.saving') : t('common.save')}
        </button>
        <button type="button" disabled={busy} onClick={() => setEditing(false)}>
          {t('common.cancel')}
        </button>
        {address === null ? null : (
          <button type="button" disabled={busy} onClick={() => void save(null)}>
            {t('customers.removeAddress')}
          </button>
        )}
      </div>
    </>
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

