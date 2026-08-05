// CaptureLead — taking down an enquiry: who asked, how they reached us, and
// what they are after.
//
// Use:  rendered by LeadsPage behind "Take an enquiry".
// Edit: the rooftop is the thing to be careful with. A lead belongs to one lot,
//       and LeadService filters every read to the caller's authorized rooftops.
//       So this asks the server which lots this person actually has, and when
//       there is only one it says which rather than offering a choice of one.
//       An empty picker at a single-lot dealership reads as a broken screen; a
//       full picker at a group implies you may file an enquiry anywhere, which
//       the server will refuse.

import { useEffect, useState } from 'react';
import { ApiError, api, post } from '../../shared/api';
import { leadSources } from '../../shared/contracts';
import type {
  CustomerSummary,
  InventoryUnitSummary,
  LeadDetail,
  LeadSource,
  OrganizationSummary,
  RooftopSummary,
} from '../../shared/contracts';

export function CaptureLead({
  onCaptured, onCancel,
}: {
  onCaptured: (lead: LeadDetail) => Promise<void>;
  onCancel: () => void;
}) {
  const [rooftops, setRooftops] = useState<RooftopSummary[]>([]);
  const [customers, setCustomers] = useState<CustomerSummary[]>([]);
  const [units, setUnits] = useState<InventoryUnitSummary[]>([]);

  const [rooftopId, setRooftopId] = useState('');
  const [customerId, setCustomerId] = useState('');
  const [source, setSource] = useState<LeadSource>('WalkIn');
  const [vehicleId, setVehicleId] = useState('');
  const [enquiry, setEnquiry] = useState('');
  const [search, setSearch] = useState('');

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
        setError(failure instanceof ApiError ? failure.message : 'Could not load your locations.');
      }

      try {
        // What they came in asking about. Cars already sold are no use here, but
        // one on hold for somebody else still is — an enquiry is not a claim on
        // the car, and the second person's interest is worth recording.
        setUnits(await api<InventoryUnitSummary[]>('/inventory?limit=200'));
      } catch {
        // A stock list that will not load must not stop an enquiry being taken.
        // The car of interest is optional; the enquiry is the thing.
        setUnits([]);
      }
    })();

    void findCustomers('');
  }, []);

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
          vehicleOfInterestId:
            vehicleId === '' ? null : units.find((u) => u.id === vehicleId)?.vehicleId ?? null,
          enquiry: enquiry.trim() === '' ? null : enquiry.trim(),
        }),
      );
    } catch (failure) {
      setError(failure instanceof ApiError ? failure.message : 'That enquiry could not be saved.');
    } finally {
      setBusy(false);
    }
  }

  const single = rooftops.length === 1 ? rooftops[0] : undefined;

  return (
    <section className="panel">
      <h2>Take an enquiry</h2>

      <form
        onSubmit={(e) => {
          e.preventDefault();
          void findCustomers(search);
        }}
      >
        <label htmlFor="enquirer-search">Find the customer</label>
        <input
          id="enquirer-search"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Name, phone, or email"
          autoComplete="off"
        />
      </form>

      <label htmlFor="enquirer">Who is asking</label>
      <select id="enquirer" value={customerId} onChange={(e) => setCustomerId(e.target.value)}>
        <option value="">Choose somebody…</option>
        {customers.map((customer) => (
          <option key={customer.id} value={customer.id}>
            {customer.displayName}
            {customer.primaryPhone === null ? '' : ` · ${customer.primaryPhone}`}
          </option>
        ))}
      </select>

      {customers.length === 0 ? (
        <p className="note">
          Search above to find them. An enquiry has to belong to somebody, so add
          them on the customers page first if they are new.
        </p>
      ) : null}

      {single === undefined ? (
        <>
          <label htmlFor="lead-rooftop">Which location</label>
          <select id="lead-rooftop" value={rooftopId} onChange={(e) => setRooftopId(e.target.value)}>
            <option value="">Choose a location…</option>
            {rooftops.map((rooftop) => (
              <option key={rooftop.id} value={rooftop.id}>
                {rooftop.name} · {rooftop.code}
              </option>
            ))}
          </select>
        </>
      ) : (
        <p className="note">
          This enquiry belongs to {single.name} ({single.code}), the only location
          you work at.
        </p>
      )}

      <label htmlFor="lead-source">How they reached us</label>
      <select
        id="lead-source"
        value={source}
        onChange={(e) => setSource(e.target.value as LeadSource)}
      >
        {leadSources.map((option) => (
          <option key={option} value={option}>
            {sourceLabel(option)}
          </option>
        ))}
      </select>

      <label htmlFor="lead-vehicle">Car they asked about (optional)</label>
      <select id="lead-vehicle" value={vehicleId} onChange={(e) => setVehicleId(e.target.value)}>
        <option value="">Nothing specific</option>
        {units.map((unit) => (
          <option key={unit.id} value={unit.id}>
            {unit.stockNumber} · {unit.vehicleDisplayName}
          </option>
        ))}
      </select>

      <label htmlFor="lead-enquiry">What they said</label>
      <textarea
        id="lead-enquiry"
        rows={3}
        value={enquiry}
        onChange={(e) => setEnquiry(e.target.value)}
        placeholder="Budget, trade-in, when they need it by…"
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
          {busy ? 'Saving…' : 'Save the enquiry'}
        </button>
        <button type="button" onClick={onCancel} disabled={busy}>
          Cancel
        </button>
      </div>
    </section>
  );
}

/** Enum names are for the wire. People read "Walk-in". */
export function sourceLabel(source: LeadSource): string {
  switch (source) {
    case 'WalkIn':
      return 'Walk-in';
    case 'Marketplace':
      return 'Marketplace listing';
    case 'Unknown':
      return 'Not recorded';
    default:
      return source;
  }
}
