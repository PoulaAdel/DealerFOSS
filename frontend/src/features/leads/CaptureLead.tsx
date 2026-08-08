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
import { api, post } from '../../shared/api';
import { leadSources } from '../../shared/contracts';
import { useI18n } from '../../shared/i18n';
import { useEnumLabel } from '../../shared/i18n/enums';
import { useApiMessage } from '../../shared/i18n/apiMessage';
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
  const { t } = useI18n();
  const label = useEnumLabel();
  const describe = useApiMessage();

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
        setError(describe(failure));
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
          vehicleOfInterestId:
            vehicleId === '' ? null : units.find((u) => u.id === vehicleId)?.vehicleId ?? null,
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

      <label htmlFor="lead-vehicle">{t('leads.carAskedAbout')}</label>
      <select id="lead-vehicle" value={vehicleId} onChange={(e) => setVehicleId(e.target.value)}>
        <option value="">{t('leads.nothingSpecific')}</option>
        {units.map((unit) => (
          <option key={unit.id} value={unit.id}>
            {unit.stockNumber} · {unit.vehicleDisplayName}
          </option>
        ))}
      </select>

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
