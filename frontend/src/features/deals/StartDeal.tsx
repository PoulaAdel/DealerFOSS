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
import { ApiError, api, post } from '../../shared/api';
import type { CustomerSummary, DealDetail, InventoryUnitSummary } from '../../shared/contracts';

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
  const [customers, setCustomers] = useState<CustomerSummary[]>([]);
  const [units, setUnits] = useState<InventoryUnitSummary[]>([]);

  const [customerId, setCustomerId] = useState(fromLead ?? '');
  const [buyer, setBuyer] = useState<CustomerSummary | null>(null);
  const [unitId, setUnitId] = useState('');
  const [search, setSearch] = useState('');

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void (async () => {
      try {
        setUnits(await api<InventoryUnitSummary[]>('/inventory?status=Available&limit=200'));
      } catch (failure) {
        setError(failure instanceof ApiError ? failure.message : 'Could not load the stock list.');
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
            failure instanceof ApiError ? failure.message : 'Could not read that customer.',
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
      setError(failure instanceof ApiError ? failure.message : 'Could not look that up.');
    }
  }

  async function start() {
    setError(null);
    setBusy(true);

    const unit = units.find((u) => u.id === unitId);
    if (unit === undefined) {
      setError('Choose a car first.');
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
      setError(failure instanceof ApiError ? failure.message : 'That deal could not be started.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="panel">
      <h2>Start a deal</h2>

      {fromLead === null ? (
        <>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              void findCustomers(search);
            }}
          >
            <label htmlFor="buyer-search">Find the buyer</label>
            <input
              id="buyer-search"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Name, phone, or email"
              autoComplete="off"
            />
          </form>

          <label htmlFor="buyer">Who is buying</label>
          <select id="buyer" value={customerId} onChange={(e) => setCustomerId(e.target.value)}>
            <option value="">Choose somebody…</option>
            {customers.map((customer) => (
              <option key={customer.id} value={customer.id}>
                {customer.displayName}
                {customer.primaryPhone === null ? '' : ` · ${customer.primaryPhone}`}
              </option>
            ))}
          </select>

          {customers.length === 0 ? (
            <p className="note">Search above to find them. Add them on the customers page if they are new.</p>
          ) : null}
        </>
      ) : (
        // No picker. The enquiry already says who this is for, and offering a
        // choice here would let somebody build the deal for the wrong person
        // while the lead still claims credit for it.
        <p className="note">
          From the enquiry for <span className="strong">{buyer?.displayName ?? 'that customer'}</span>.
          The deal will be linked to it.
        </p>
      )}

      <label htmlFor="car">Which car</label>
      <select id="car" value={unitId} onChange={(e) => setUnitId(e.target.value)}>
        <option value="">Choose a car…</option>
        {units.map((unit) => (
          <option key={unit.id} value={unit.id}>
            {unit.stockNumber} · {unit.vehicleDisplayName}
          </option>
        ))}
      </select>

      {units.length === 0 ? (
        <p className="note">
          Nothing on the lot is available right now. A car already on another deal
          is held until that deal ends.
        </p>
      ) : null}

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
          {busy ? 'Starting…' : 'Start the deal'}
        </button>
        <button type="button" onClick={onCancel} disabled={busy}>
          Cancel
        </button>
      </div>
    </section>
  );
}
