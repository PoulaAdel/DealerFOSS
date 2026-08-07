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

      setLoad({
        kind: 'failed',
        message: failure instanceof ApiError ? failure.message : 'Could not load the stock list.',
      });
    }
  }, [status, stockNumber]);

  useEffect(() => {
    void fetchUnits();
  }, [fetchUnits]);

  return (
    <>
      <header className="page__head">
        <h1>Stock</h1>

        <div className="filter">
          <label htmlFor="status">Status</label>
          <select
            id="status"
            value={status}
            onChange={(e) => setStatus(e.target.value as InventoryStatus | '')}
          >
            <option value="">All</option>
            {inventoryStatuses.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
        </div>
      </header>

      {stockNumber === '' ? null : (
        <p className="notice" role="status">
          Showing stock number <span className="mono">{stockNumber}</span> only.{' '}
          <button type="button" className="link" onClick={() => setParams({})}>
            Show everything
          </button>
        </p>
      )}

      <Body load={load} onRetry={fetchUnits} />
    </>
  );
}

function Body({ load, onRetry }: { load: Load; onRetry: () => void }) {
  switch (load.kind) {
    case 'loading':
      return (
        <p className="state" aria-live="polite">
          Loading the stock list…
        </p>
      );

    case 'denied':
      return (
        <p className="state" role="alert">
          You do not have access to this location&rsquo;s stock. Ask a manager if
          you think that is wrong.
        </p>
      );

    case 'failed':
      return (
        <div className="state" role="alert">
          <p>{load.message}</p>
          <button type="button" onClick={onRetry}>
            Try again
          </button>
        </div>
      );

    case 'ready':
      return load.units.length === 0 ? (
        <p className="state">
          Nothing here yet. Cars appear once they are taken into stock.
        </p>
      ) : (
        <UnitTable units={load.units} />
      );
  }
}

function UnitTable({ units }: { units: InventoryUnitSummary[] }) {
  // A full page means there are probably more, and we cannot know how many.
  // Saying "200 vehicles in stock" to somebody with 400 cars is a false
  // statement on a screen they are using to count their own stock.
  const capped = units.length >= PageSize;

  return (
    <div className="scroll">
      <table>
        <caption className="visually-hidden">
          {/* "1 vehicles" is what a caption reads out when somebody arrives here
              from a link that named one car — which is now a route this screen
              genuinely has. */}
          {capped
            ? `The first ${units.length} vehicles in stock. There may be more.`
            : `${units.length} ${units.length === 1 ? 'vehicle' : 'vehicles'} in stock`}
        </caption>
        <thead>
          <tr>
            <th scope="col">Stock</th>
            <th scope="col">Vehicle</th>
            <th scope="col">VIN</th>
            <th scope="col">Status</th>
          </tr>
        </thead>
        <tbody>
          {units.map((unit) => (
            <tr key={unit.id}>
              <td className="mono">{unit.stockNumber}</td>
              <td>{unit.vehicleDisplayName}</td>
              <td className="mono vin">{unit.vin}</td>
              <td>
                <span className={`chip chip--${unit.status.toLowerCase()}`}>{unit.status}</span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {capped ? (
        <p className="note note--footer">
          Showing the first {units.length}. There may be more — narrow it with the
          status filter until paging exists.
        </p>
      ) : null}
    </div>
  );
}
