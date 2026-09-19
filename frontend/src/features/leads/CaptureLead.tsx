// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   CaptureLead — taking down an enquiry: who asked, how they reached us, and
//   what they are after.
//
// Usage:
//   Rendered by LeadsPage behind "Take an enquiry".
//
// Coding Instructions:
//   The rooftop is the thing to be careful with. A lead belongs to one lot,
//   and LeadService filters every read to the caller's authorized rooftops.
//   So this asks the server which lots this person actually has, and when
//   there is only one it says which rather than offering a choice of one.
//   An empty picker at a single-lot dealership reads as a broken screen; a
//   full picker at a group implies you may file an enquiry anywhere, which
//   the server will refuse.

import { useCallback, useEffect, useState } from 'react';
import { api, post } from '../../shared/api';
import { leadSources } from '../../shared/contracts';
import { useI18n } from '../../shared/i18n';
import { useEnumLabel } from '../../shared/i18n/enums';
import { useApiMessage } from '../../shared/i18n/apiMessage';
import { RecordPicker, type PickerOption } from '../../shared/RecordPicker';
import type {
  CustomerDetail,
  CustomerSummary,
  InventoryUnitSummary,
  LeadDetail,
  LeadSource,
  OrganizationSummary,
  RooftopSummary,
  Page,
} from '../../shared/contracts';

export function CaptureLead({
  onCaptured, onCancel,
}: {
  onCaptured: (lead: LeadDetail) => Promise<void>;
  onCancel: () => void;
}) {
  const { t } = useI18n();
  const label = useEnumLabel();
  const describe = useApiMessage();

  const [rooftops, setRooftops] = useState<RooftopSummary[]>([]);
  const [customers, setCustomers] = useState<CustomerSummary[]>([]);
  const [units, setUnits] = useState<InventoryUnitSummary[]>([]);

  const [rooftopId, setRooftopId] = useState('');
  const [customerId, setCustomerId] = useState('');
  const [source, setSource] = useState<LeadSource>('WalkIn');
  const [car, setCar] = useState<PickerOption | null>(null);
  const [enquiry, setEnquiry] = useState('');
  const [search, setSearch] = useState('');
  const [adding, setAdding] = useState(false);

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void (async () => {
      try {
        const organization = await api<OrganizationSummary>('/organization');
        const mine = organization.legalEntities.flatMap((entity) => entity.rooftops);

        setRooftops(mine);
        if (mine.length === 1) {
          setRooftopId(mine[0]!.id);
        }
      } catch (failure) {
        setError(describe(failure));
      }

      try {
        // What they came in asking about. Cars already sold are no use here, but
        // one on hold for somebody else still is — an enquiry is not a claim on
        // the car, and the second person's interest is worth recording.
        //
        // THE COMMENT ABOVE WAS TRUE OF THE INTENT AND FALSE OF THE CODE until
        // 2026-09-15: the next line asked for `/inventory?limit=200` with no
        // status filter at all, so the picker offered SOLD cars. Walked
        // 2026-09-10 and it offered A1001, which the API reported as Sold.
        setUnits((await api<Page<InventoryUnitSummary>>(
          '/inventory?stillGettable=true&limit=25')).rows);
      } catch {
        // A stock list that will not load must not stop an enquiry being taken.
        // The car of interest is optional; the enquiry is the thing.
        setUnits([]);
      }
    })();

    void findCustomers('');
  }, []);

  /**
   * A car on the lot, as something to pick.
   *
   * THE ID IS THE VEHICLE'S, NOT THE UNIT'S, because the lead records interest
   * in a car rather than in a row of stock — the enquiry has to survive that
   * particular unit being sold to somebody else. The stock number is the hint,
   * since that is what a salesperson has written on the windscreen and it is
   * what separates two identical cars on the same lot.
   */
  const asStockOption = useCallback(
    (unit: InventoryUnitSummary): PickerOption => ({
      id: unit.vehicleId,
      label: unit.vehicleDisplayName,
      hint: unit.stockNumber,
    }),
    [],
  );

  const findCars = useCallback(
    async (term: string, signal: AbortSignal) =>
      (await api<Page<InventoryUnitSummary>>(
        `/inventory?search=${encodeURIComponent(term)}&stillGettable=true&limit=15`,
        { signal },
      )).rows.map(asStockOption),
    [asStockOption],
  );

  async function findCustomers(term: string) {
    setError(null);

    try {
      const query = term.trim() === ''
        ? '?limit=25'
        : `?search=${encodeURIComponent(term.trim())}&limit=25`;

      setCustomers((await api<Page<CustomerSummary>>(`/customers${query}`)).rows);
    } catch (failure) {
      setError(describe(failure));
    }
  }

  async function capture() {
    setError(null);
    setBusy(true);

    try {
      await onCaptured(
        await post<LeadDetail>('/leads', {
          rooftopId,
          customerId,
          source,
          // The lead points at the vehicle, not at the unit on the lot: the
          // enquiry survives that particular car being sold to somebody else.
          // Which is why the picker's option id IS the vehicle id — see
          // asStockOption.
          vehicleOfInterestId: car?.id ?? null,
          enquiry: enquiry.trim() === '' ? null : enquiry.trim(),
        }),
      );
    } catch (failure) {
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  const single = rooftops.length === 1 ? rooftops[0] : undefined;

  return (
    <section className="panel">
      <h2>{t('leads.captureTitle')}</h2>

      <form
        onSubmit={(e) => {
          e.preventDefault();
          void findCustomers(search);
        }}
      >
        <label htmlFor="enquirer-search">{t('leads.findCustomer')}</label>
        <input
          id="enquirer-search"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={t('customers.findPlaceholder')}
          autoComplete="off"
        />

        {/* The search ran only on Enter, with no button and no as-you-type
            query, so somebody typing a name and tabbing onward saw the same
            first 25 customers and concluded the person was not on file. */}
        <button type="submit">{t('common.search')}</button>
      </form>

      <label htmlFor="enquirer">{t('leads.whoIsAsking')}</label>
      <select id="enquirer" value={customerId} onChange={(e) => setCustomerId(e.target.value)}>
        <option value="">{t('leads.chooseSomebody')}</option>
        {customers.map((customer) => (
          <option key={customer.id} value={customer.id}>
            {customer.displayName}
            {customer.primaryPhone === null ? '' : ` · ${customer.primaryPhone}`}
          </option>
        ))}
      </select>

      {customers.length === 0 ? <p className="note">{t('leads.searchAboveNote')}</p> : null}

      {/* A walk-in is by definition somebody you have never met, and "Walk-in"
          is one of the five sources this very form offers. Until 2026-09-11 the
          only way to record one was to abandon the enquiry, create the customer
          on another screen, come back, and start again. */}
      {adding ? (
        <NewCustomer
          onAdded={(customer) => {
            setAdding(false);
            setCustomers([customer]);
            setCustomerId(customer.id);
          }}
          onCancel={() => setAdding(false)}
        />
      ) : (
        <button type="button" className="link" onClick={() => setAdding(true)}>
          {t('leads.notOnFile')}
        </button>
      )}

      {single === undefined ? (
        <>
          <label htmlFor="lead-rooftop">{t('leads.whichLocation')}</label>
          <select id="lead-rooftop" value={rooftopId} onChange={(e) => setRooftopId(e.target.value)}>
            <option value="">{t('leads.chooseLocation')}</option>
            {rooftops.map((rooftop) => (
              <option key={rooftop.id} value={rooftop.id}>
                {rooftop.name} · {rooftop.code}
              </option>
            ))}
          </select>
        </>
      ) : (
        <p className="note">
          {t('leads.onlyLocation', { name: single.name, code: single.code })}
        </p>
      )}

      <label htmlFor="lead-source">{t('leads.howTheyReachedUs')}</label>
      <select
        id="lead-source"
        value={source}
        onChange={(e) => setSource(e.target.value as LeadSource)}
      >
        {leadSources.map((option) => (
          <option key={option} value={option}>
            {label('leadSource', option)}
          </option>
        ))}
      </select>

      {/* The car is OPTIONAL here — "nothing specific" is the commonest walk-in
          — so this keeps a way to choose nothing, which a search box alone
          cannot express. The shortlist is whatever is on the lot right now; the
          search reaches the rest of it. */}
      <RecordPicker
        id="lead-vehicle"
        label={t('leads.carAskedAbout')}
        chosen={car}
        onChoose={setCar}
        search={findCars}
        shortlist={units.map(asStockOption)}
      />

      {car === null ? <p className="note">{t('leads.nothingSpecific')}</p> : null}

      <label htmlFor="lead-enquiry">{t('leads.whatTheySaid')}</label>
      <textarea
        id="lead-enquiry"
        rows={3}
        value={enquiry}
        onChange={(e) => setEnquiry(e.target.value)}
        placeholder={t('leads.whatTheySaidPlaceholder')}
      />

      <p className="error" aria-live="polite">
        {error ?? ''}
      </p>

      <div className="actions">
        <button
          type="button"
          className="primary"
          disabled={busy || customerId === '' || rooftopId === ''}
          onClick={() => void capture()}
        >
          {busy ? t('common.saving') : t('leads.save')}
        </button>
        <button type="button" onClick={onCancel} disabled={busy}>
          {t('common.cancel')}
        </button>
      </div>
    </section>
  );
}

/**
 * Somebody the dealership has never met, recorded without leaving the enquiry.
 *
 * The same four fields the customers screen asks for, and deliberately no more:
 * a person standing at the desk is not going to wait while somebody fills in a
 * full record, and an enquiry with a name and a phone number is worth far more
 * than a perfect record nobody took.
 */
function NewCustomer({
  onAdded,
  onCancel,
}: {
  onAdded: (customer: CustomerSummary) => void;
  onCancel: () => void;
}) {
  const { t } = useI18n();
  const describe = useApiMessage();

  const [kind, setKind] = useState<'Person' | 'Business'>('Person');
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [businessName, setBusinessName] = useState('');
  const [phone, setPhone] = useState('');

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const isPerson = kind === 'Person';
  const ready = isPerson ? lastName.trim() !== '' : businessName.trim() !== '';

  async function add() {
    setError(null);
    setBusy(true);

    try {
      const created = await post<CustomerDetail>('/customers', {
        kind,
        firstName: isPerson && firstName.trim() !== '' ? firstName.trim() : null,
        lastName: isPerson ? lastName.trim() : null,
        businessName: isPerson ? null : businessName.trim(),
        phone: phone.trim() === '' ? null : phone.trim(),
      });

      onAdded({
        id: created.id,
        displayName: created.displayName,
        kind: created.kind,
        primaryEmail: null,
        primaryPhone: phone.trim() === '' ? null : phone.trim(),

        // Just created here, so no provider has had a chance to withdraw it.
        removedAtProviderOn: null,
      });
    } catch (failure) {
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="panel panel--nested">
      <h3>{t('leads.newCustomerTitle')}</h3>

      <label htmlFor="new-kind">{t('customers.kindLabel')}</label>
      <select
        id="new-kind"
        value={kind}
        onChange={(event) => setKind(event.target.value as 'Person' | 'Business')}
      >
        <option value="Person">{t('enum.customerKind.Person')}</option>
        <option value="Business">{t('enum.customerKind.Business')}</option>
      </select>

      {isPerson ? (
        <>
          <label htmlFor="new-first">{t('customers.firstName')}</label>
          <input
            id="new-first"
            value={firstName}
            onChange={(event) => setFirstName(event.target.value)}
            autoComplete="off"
          />

          <label htmlFor="new-last">{t('customers.lastName')}</label>
          <input
            id="new-last"
            value={lastName}
            onChange={(event) => setLastName(event.target.value)}
            autoComplete="off"
          />
        </>
      ) : (
        <>
          <label htmlFor="new-business">{t('customers.businessName')}</label>
          <input
            id="new-business"
            value={businessName}
            onChange={(event) => setBusinessName(event.target.value)}
            autoComplete="off"
          />
        </>
      )}

      <label htmlFor="new-phone">{t('customers.phone')}</label>
      <input
        id="new-phone"
        value={phone}
        onChange={(event) => setPhone(event.target.value)}
        autoComplete="off"
      />

      {error === null ? null : <p className="error" role="alert">{error}</p>}

      <div className="actions">
        <button type="button" disabled={!ready || busy} onClick={() => void add()}>
          {t('leads.addAndUse')}
        </button>
        <button type="button" onClick={onCancel} disabled={busy}>
          {t('common.cancel')}
        </button>
      </div>
    </div>
  );
}
