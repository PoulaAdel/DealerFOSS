// InventoryPage — what is on the lot.
//
// Use:  the first screen worth showing somebody: it proves tenancy, rooftop
//       scope, and the inventory life cycle all at once.
// Edit: every state a real screen needs is here on purpose — loading, empty,
//       permission-denied, failure, and retry (doc 10 §5). A screen that only
//       renders the happy path is not finished.

import { useCallback, useEffect, useState } from 'react';
import { ApiError, api } from '../../shared/api';
import { inventoryStatuses, type InventoryStatus, type InventoryUnitSummary } from '../../shared/contracts';

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; units: InventoryUnitSummary[] }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

export function InventoryPage() {
  const [status, setStatus] = useState<InventoryStatus | ''>('');
  const [load, setLoad] = useState<Load>({ kind: 'loading' });

  const fetchUnits = useCallback(async () => {
    setLoad({ kind: 'loading' });

    try {
      const query = status ? `?status=${encodeURIComponent(status)}&limit=200` : '?limit=200';
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
  }, [status]);

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
  return (
    <div className="scroll">
      <table>
        <caption className="visually-hidden">
          {units.length} vehicles in stock
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
    </div>
  );
}
