// InventoryPage — what is on the lot.
//
// Use:  the first screen worth showing somebody: it proves tenancy, rooftop
//       scope, and the inventory life cycle all at once.
// Edit: every state a real screen needs is here on purpose — loading, empty,
//       permission-denied, failure, and retry (doc 10 §5). A screen that only
//       renders the happy path is not finished.

import { useCallback, useEffect, useState } from 'react';
import { useSearchParams } from 'react-router';
import { ApiError, api } from '../../shared/api';
import { inventoryStatuses, type InventoryStatus, type InventoryUnitSummary } from '../../shared/contracts';
import { useI18n } from '../../shared/i18n';
import { useEnumLabel } from '../../shared/i18n/enums';
import { useApiMessage } from '../../shared/i18n/apiMessage';

/**
 * What the server will return at most, however many are asked for — it clamps
 * to this in `InventoryService`. The screen has to know, because a full page is
 * indistinguishable from "that is all of them" and saying the wrong one puts a
 * false number in front of somebody counting their own stock.
 */
const PageSize = 200;

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; units: InventoryUnitSummary[] }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

export function InventoryPage() {
  const { t } = useI18n();
  const label = useEnumLabel();
  const describe = useApiMessage();

  const [status, setStatus] = useState<InventoryStatus | ''>('');
  const [load, setLoad] = useState<Load>({ kind: 'loading' });

  // Somebody arrived here from a car named somewhere else — the dashboard's
  // oldest-stock list is the one that does this. Landing on the whole list
  // instead would make that link a promise the screen does not keep.
  const [params, setParams] = useSearchParams();
  const stockNumber = params.get('stock') ?? '';

  const fetchUnits = useCallback(async () => {
    setLoad({ kind: 'loading' });

    const filters = [
      `limit=${PageSize}`,
      ...(status ? [`status=${encodeURIComponent(status)}`] : []),
      ...(stockNumber ? [`stock=${encodeURIComponent(stockNumber)}`] : []),
    ];

    try {
      const query = `?${filters.join('&')}`;
      setLoad({ kind: 'ready', units: await api<InventoryUnitSummary[]>(`/inventory${query}`) });
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 403) {
        setLoad({ kind: 'denied' });
        return;
      }

      setLoad({ kind: 'failed', message: describe(failure) });
    }
  }, [status, stockNumber, describe]);

  useEffect(() => {
    void fetchUnits();
  }, [fetchUnits]);

  return (
    <>
      <header className="page__head">
        <h1>{t('stock.title')}</h1>

        <div className="filter">
          <label htmlFor="status">{t('stock.status')}</label>
          <select
            id="status"
            value={status}
            onChange={(e) => setStatus(e.target.value as InventoryStatus | '')}
          >
            <option value="">{t('common.all')}</option>
            {inventoryStatuses.map((s) => (
              <option key={s} value={s}>
                {label('inventoryStatus', s)}
              </option>
            ))}
          </select>
        </div>
      </header>

      {stockNumber === '' ? null : (
        <p className="notice" role="status">
          {/* The stock number is a code the dealership assigns, so it reads left
              to right even on an Arabic page. */}
          {t('stock.onlyStockNumber', { stock: stockNumber })}{' '}
          <button type="button" className="link" onClick={() => setParams({})}>
            {t('stock.showEverything')}
          </button>
        </p>
      )}

      <Body load={load} onRetry={fetchUnits} />
    </>
  );
}

function Body({ load, onRetry }: { load: Load; onRetry: () => void }) {
  const { t } = useI18n();

  switch (load.kind) {
    case 'loading':
      return (
        <p className="state" aria-live="polite">
          {t('stock.loading')}
        </p>
      );

    case 'denied':
      return (
        <p className="state" role="alert">
          {t('stock.denied')}
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
      return load.units.length === 0 ? (
        <p className="state">{t('stock.empty')}</p>
      ) : (
        <UnitTable units={load.units} />
      );
  }
}

function UnitTable({ units }: { units: InventoryUnitSummary[] }) {
  const { t } = useI18n();
  const label = useEnumLabel();

  // A full page means there are probably more, and we cannot know how many.
  // Saying "200 vehicles in stock" to somebody with 400 cars is a false
  // statement on a screen they are using to count their own stock.
  const capped = units.length >= PageSize;

  return (
    <div className="scroll">
      <table>
        <caption className="visually-hidden">
          {/* A plural entry rather than `n === 1 ? 'vehicle' : 'vehicles'`.
              Russian needs four forms of this sentence and Arabic six, and
              somebody arriving from a link that named one car is exactly the
              case that used to read "1 vehicles". */}
          {capped
            ? t('stock.countCapped', { count: units.length })
            : t('stock.count', { count: units.length })}
        </caption>
        <thead>
          <tr>
            <th scope="col">{t('stock.colStock')}</th>
            <th scope="col">{t('stock.colVehicle')}</th>
            <th scope="col">{t('stock.colVin')}</th>
            <th scope="col">{t('stock.colStatus')}</th>
          </tr>
        </thead>
        <tbody>
          {units.map((unit) => (
            <tr key={unit.id}>
              {/* Stock numbers and VINs are codes, not prose: they read left to
                  right whatever the page does, or the bidi algorithm reorders
                  the groups and somebody reads out the wrong VIN. */}
              <td className="mono" dir="ltr">
                {unit.stockNumber}
              </td>
              <td>{unit.vehicleDisplayName}</td>
              <td className="mono vin" dir="ltr">
                {unit.vin}
              </td>
              <td>
                {/* The class still keys off the raw API value, so the colour
                    does not depend on what language the page is in. */}
                <span className={`chip chip--${unit.status.toLowerCase()}`}>
                  {label('inventoryStatus', unit.status)}
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {capped ? (
        <p className="note note--footer">{t('stock.cappedNote', { count: units.length })}</p>
      ) : null}
    </div>
  );
}
