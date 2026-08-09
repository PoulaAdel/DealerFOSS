// StartDeal — beginning a deal: who is buying, and which car.
//
// Use:  rendered by DealsPage behind "Start a deal".
// Edit: only cars that are actually **available** are offered. A car already on
//       somebody else's deal is held, and the server refuses a second deal on it
//       with a 409 — but offering it and then explaining the refusal wastes a
//       salesperson's time in front of a customer. The list is filtered, and the
//       server still refuses, because the list can go stale between loading it
//       and pressing the button.
//
//       The rooftop is taken from the chosen car rather than asked for. A car is
//       on exactly one lot, and asking somebody to name it again is an invitation
//       to pick the wrong one.

import { useEffect, useState } from 'react';
import { api, post } from '../../shared/api';
import type { CustomerSummary, DealDetail, InventoryUnitSummary } from '../../shared/contracts';
import { useI18n } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';
import { Emphasised } from '../../shared/i18n/Emphasised';

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
  const [unitId, setUnitId] = useState('');
  const [search, setSearch] = useState('');

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const buyerName = buyer?.displayName ?? t('startDeal.thatCustomer');

  useEffect(() => {
    void (async () => {
      try {
        setUnits(await api<InventoryUnitSummary[]>('/inventory?status=Available&limit=200'));
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

  async function findCustomers(term: string) {
    setError(null);

    try {
      const query = term.trim() === ''
        ? '?limit=25'
        : `?search=${encodeURIComponent(term.trim())}&limit=25`;

      setCustomers(await api<CustomerSummary[]>(`/customers${query}`));
    } catch (failure) {
      setError(describe(failure));
    }
  }

  async function start() {
    setError(null);
    setBusy(true);

    const unit = units.find((u) => u.id === unitId);
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
          inventoryUnitId: unitId,
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

      <label htmlFor="car">{t('startDeal.whichCar')}</label>
      <select id="car" value={unitId} onChange={(e) => setUnitId(e.target.value)}>
        <option value="">{t('startDeal.chooseCar')}</option>
        {units.map((unit) => (
          <option key={unit.id} value={unit.id}>
            {unit.stockNumber} · {unit.vehicleDisplayName}
          </option>
        ))}
      </select>

      {units.length === 0 ? <p className="note">{t('startDeal.nothingAvailable')}</p> : null}

      <p className="error" aria-live="polite">
        {error ?? ''}
      </p>

      <div className="actions">
        <button
          type="button"
          className="primary"
          disabled={busy || customerId === '' || unitId === ''}
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

