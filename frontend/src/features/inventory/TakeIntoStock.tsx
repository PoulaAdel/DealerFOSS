// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   TakeIntoStock — recording a car arriving on the lot, and what it cost.
//
//   Two records in one panel on purpose. A car arriving is almost always one
//   the dealership has never seen: the VEHICLE (a VIN and what it is) and the
//   UNIT (which lot, what stock number, what it cost) are separate things in
//   the domain and one event to the person standing next to the car. Asking
//   them to create a vehicle somewhere else and come back is the shape that
//   made taking an enquiry for a walk-in impossible, and it is not repeated
//   here.
//
// Usage:
//   Rendered by InventoryPage behind its "take a car into stock" button.
//   Calls POST /vehicles then POST /inventory, and hands the new unit back.
//
// Coding Instructions:
//   THE COST IS THE POINT, and it is optional. Supplying one posts to the
//   ledger — 1300 debited, 1000 credited — so a car on the lot is a car on the
//   balance sheet. Leaving it out records the unit and posts nothing, which is
//   right for a part-exchange still being appraised: a cost nobody knows is not
//   a cost of zero. Do not "helpfully" default it.
//
//   The two calls are not a transaction and cannot be. If the unit fails after
//   the vehicle is created, the vehicle stands and the error says so, because a
//   silently orphaned vehicle is worse than a visible one.

import { useEffect, useState } from 'react';
import { api, post } from '../../shared/api';
import type {
  InventoryUnitDetail,
  OrganizationSummary,
  RooftopSummary,
} from '../../shared/contracts';
import { useI18n } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';

export function TakeIntoStock({
  onReceived,
  onCancel,
}: {
  onReceived: (unit: InventoryUnitDetail) => void;
  onCancel: () => void;
}) {
  const { t } = useI18n();
  const describe = useApiMessage();

  const [rooftops, setRooftops] = useState<RooftopSummary[]>([]);
  const [rooftopId, setRooftopId] = useState('');

  const [vin, setVin] = useState('');
  const [modelYear, setModelYear] = useState('');
  const [make, setMake] = useState('');
  const [model, setModel] = useState('');
  const [trim, setTrim] = useState('');

  const [stockNumber, setStockNumber] = useState('');
  const [cost, setCost] = useState('');

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
    })();
  }, [describe]);

  const year = Number(modelYear);
  const ready =
    rooftopId !== ''
    && make.trim() !== ''
    && model.trim() !== ''
    && stockNumber.trim() !== ''
    && Number.isInteger(year)
    && year > 1900;

  async function receive() {
    setError(null);
    setBusy(true);

    try {
      const vehicle = await post<{ id: string }>('/vehicles', {
        vin: vin.trim() === '' ? null : vin.trim(),
        modelYear: year,
        make: make.trim(),
        model: model.trim(),
        trim: trim.trim() === '' ? null : trim.trim(),
      });

      const typed = cost.trim() === '' ? null : Number(cost);

      onReceived(
        await post<InventoryUnitDetail>('/inventory', {
          vehicleId: vehicle.id,
          rooftopId,
          stockNumber: stockNumber.trim(),
          // Number('') is 0 and Number('abc') is NaN, so both are turned back
          // into "not recorded" rather than into a car that cost nothing.
          costAmount: typed === null || Number.isNaN(typed) ? null : typed,
          costCurrency: typed === null || Number.isNaN(typed) ? null : 'USD',
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
      <h2>{t('stock.takeInTitle')}</h2>
      <p className="note">{t('stock.takeInNote')}</p>

      <form
        onSubmit={(event) => {
          event.preventDefault();
          if (ready && !busy) {
            void receive();
          }
        }}
      >
        {rooftops.length <= 1 ? null : (
          <>
            <label htmlFor="take-rooftop">{t('stock.whichLocation')}</label>
            <select
              id="take-rooftop"
              value={rooftopId}
              onChange={(event) => setRooftopId(event.target.value)}
            >
              <option value="">{t('stock.chooseLocation')}</option>
              {rooftops.map((rooftop) => (
                <option key={rooftop.id} value={rooftop.id}>
                  {rooftop.name}
                </option>
              ))}
            </select>
          </>
        )}

        <label htmlFor="take-stock">{t('stock.colStock')}</label>
        {/* A code the dealership assigns, so it reads left to right even on an
            Arabic page — the same treatment the list and the detail band give it. */}
        <input
          id="take-stock"
          dir="ltr"
          value={stockNumber}
          onChange={(event) => setStockNumber(event.target.value)}
          autoComplete="off"
        />

        <label htmlFor="take-vin">{t('stock.vinOptional')}</label>
        <input
          id="take-vin"
          dir="ltr"
          value={vin}
          onChange={(event) => setVin(event.target.value)}
          autoComplete="off"
        />

        <label htmlFor="take-year">{t('stock.modelYear')}</label>
        <input
          id="take-year"
          inputMode="numeric"
          value={modelYear}
          onChange={(event) => setModelYear(event.target.value)}
        />

        <label htmlFor="take-make">{t('stock.make')}</label>
        <input id="take-make" value={make} onChange={(event) => setMake(event.target.value)} />

        <label htmlFor="take-model">{t('stock.model')}</label>
        <input id="take-model" value={model} onChange={(event) => setModel(event.target.value)} />

        <label htmlFor="take-trim">{t('stock.trimOptional')}</label>
        <input id="take-trim" value={trim} onChange={(event) => setTrim(event.target.value)} />

        <label htmlFor="take-cost">{t('stock.costOptional')}</label>
        <input
          id="take-cost"
          inputMode="decimal"
          value={cost}
          onChange={(event) => setCost(event.target.value)}
        />
        <p className="note">{t('stock.costNote')}</p>

        {error === null ? null : (
          <p className="error" role="alert">
            {error}
          </p>
        )}

        <div className="actions">
          <button type="submit" disabled={!ready || busy}>
            {t('stock.confirmTakeIn')}
          </button>
          <button type="button" onClick={onCancel} disabled={busy}>
            {t('common.cancel')}
          </button>
        </div>
      </form>
    </section>
  );
}
