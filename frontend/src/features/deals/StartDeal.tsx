// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   StartDeal — beginning a deal: who is buying, and which car.
//
// Usage:
//   Rendered by DealsPage behind "Start a deal".
//
// Coding Instructions:
//   Only cars that are actually **available** are offered. A car already on
//   somebody else's deal is held, and the server refuses a second deal on it
//   with a 409 — but offering it and then explaining the refusal wastes a
//   salesperson's time in front of a customer. The list is filtered, and the
//   server still refuses, because the list can go stale between loading it
//   and pressing the button.
//
//   The rooftop is taken from the chosen car rather than asked for. A car is
//   on exactly one lot, and asking somebody to name it again is an invitation
//   to pick the wrong one.

import { useCallback, useEffect, useRef, useState } from 'react';
import { api, post } from '../../shared/api';
import type { CustomerSummary, DealDetail, InventoryUnitSummary, Page } from '../../shared/contracts';
import { useI18n } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';
import { Emphasised } from '../../shared/i18n/Emphasised';
import { RecordPicker, type PickerOption } from '../../shared/RecordPicker';

export function StartDeal({
  onStarted, onCancel, leadId = null, customerId: fromLead = null,
}: {
  onStarted: (deal: DealDetail) => Promise<void>;
  onCancel: () => void;
  /** Set when a won enquiry sent us here; carried onto the deal. */
  leadId?: string | null;
  /** The buyer the enquiry was from, preselected so nobody retypes them. */
  customerId?: string | null;
}) {
  const { t } = useI18n();
  const describe = useApiMessage();

  const [customers, setCustomers] = useState<CustomerSummary[]>([]);
  const [units, setUnits] = useState<InventoryUnitSummary[]>([]);

  const [customerId, setCustomerId] = useState(fromLead ?? '');
  const [buyer, setBuyer] = useState<CustomerSummary | null>(null);
  const [car, setCar] = useState<PickerOption | null>(null);

  /**
   * Every unit this screen has been shown, by id.
   *
   * The picker hands back an id and a label; starting a deal needs the unit's
   * ROOFTOP as well, because a deal belongs to a lot. Rather than a second
   * round trip after every choice, both the shortlist and each search record
   * what they returned here -- the picker can only ever hand back something one
   * of them produced.
   */
  const seen = useRef(new Map<string, InventoryUnitSummary>());
  const [search, setSearch] = useState('');

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const buyerName = buyer?.displayName ?? t('startDeal.thatCustomer');

  useEffect(() => {
    void (async () => {
      try {
        const available = (await api<Page<InventoryUnitSummary>>(
          '/inventory?status=Available&limit=25')).rows;

        available.forEach((unit) => seen.current.set(unit.id, unit));
        setUnits(available);
      } catch (failure) {
        setError(describe(failure));
      }
    })();

    if (fromLead !== null) {
      // Arrived from a won enquiry. The buyer is already decided, and the recent
      // list would very likely not contain them — leaving the picker showing a
      // blank while the deal is in fact for somebody specific.
      void (async () => {
        try {
          setBuyer(await api<CustomerSummary>(`/customers/${fromLead}`));
        } catch (failure) {
          setError(
            describe(failure),
          );
        }
      })();

      return;
    }

    // An empty buyer list with a "search first" note makes somebody type before
    // they can do anything, even for a customer they served an hour ago. The
    // most recently added are a useful default; searching narrows from there.
    void findCustomers('');
  }, [fromLead]);

  /**
   * A car on the lot, as something to pick.
   *
   * THE ID IS THE UNIT'S, not the vehicle's — the opposite of the enquiry
   * screen, and deliberately. A deal is struck on one physical car on one lot;
   * an enquiry is interest in a car and survives that unit being sold to
   * somebody else.
   */
  const asUnitOption = useCallback(
    (unit: InventoryUnitSummary): PickerOption => ({
      id: unit.id,
      label: unit.vehicleDisplayName,
      hint: unit.stockNumber,
    }),
    [],
  );

  const findCars = useCallback(
    async (term: string, signal: AbortSignal) => {
      const rows = (await api<Page<InventoryUnitSummary>>(
        `/inventory?search=${encodeURIComponent(term)}&status=Available&limit=15`,
        { signal },
      )).rows;

      rows.forEach((unit) => seen.current.set(unit.id, unit));

      return rows.map(asUnitOption);
    },
    [asUnitOption],
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

  async function start() {
    setError(null);
    setBusy(true);

    const unit = car === null ? undefined : seen.current.get(car.id);
    if (unit === undefined) {
      setError(t('startDeal.chooseCarFirst'));
      setBusy(false);
      return;
    }

    try {
      await onStarted(
        await post<DealDetail>('/deals', {
          rooftopId: unit.rooftopId,
          customerId,
          inventoryUnitId: unit.id,
          leadId,
          // One currency until a dealership needs two. When that lands it comes
          // from the rooftop's legal entity, not from a box somebody types into.
          currency: 'USD',
        }),
      );
    } catch (failure) {
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="panel">
      <h2>{t('startDeal.title')}</h2>

      {fromLead === null ? (
        <>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              void findCustomers(search);
            }}
          >
            <label htmlFor="buyer-search">{t('startDeal.findBuyer')}</label>
            <input
              id="buyer-search"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={t('customers.findPlaceholder')}
              autoComplete="off"
            />
          </form>

          <label htmlFor="buyer">{t('startDeal.buyer')}</label>
          <select id="buyer" value={customerId} onChange={(e) => setCustomerId(e.target.value)}>
            <option value="">{t('startDeal.chooseBuyer')}</option>
            {customers.map((customer) => (
              <option key={customer.id} value={customer.id}>
                {customer.displayName}
                {customer.primaryPhone === null ? '' : ` · ${customer.primaryPhone}`}
              </option>
            ))}
          </select>

          {customers.length === 0 ? <p className="note">{t('startDeal.searchAbove')}</p> : null}
        </>
      ) : (
        // No picker. The enquiry already says who this is for, and offering a
        // choice here would let somebody build the deal for the wrong person
        // while the lead still claims credit for it.
        <p className="note">
          <Emphasised
            sentence={t('startDeal.fromEnquiry', { name: buyerName })}
            value={buyerName}
          />
        </p>
      )}

      <RecordPicker
        id="car"
        label={t('startDeal.whichCar')}
        chosen={car}
        onChoose={setCar}
        search={findCars}
        shortlist={units.map(asUnitOption)}
      />

      {units.length === 0 ? <p className="note">{t('startDeal.nothingAvailable')}</p> : null}

      <p className="error" aria-live="polite">
        {error ?? ''}
      </p>

      <div className="actions">
        <button
          type="button"
          className="primary"
          disabled={busy || customerId === '' || car === null}
          onClick={() => void start()}
        >
          {busy ? t('startDeal.starting') : t('startDeal.submit')}
        </button>
        <button type="button" onClick={onCancel} disabled={busy}>
          {t('common.cancel')}
        </button>
      </div>
    </section>
  );
}

