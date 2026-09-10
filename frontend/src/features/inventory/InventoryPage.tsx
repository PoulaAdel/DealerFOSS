// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   InventoryPage — what is on the lot.
//
// Usage:
//   The first screen worth showing somebody: it proves tenancy, rooftop
//   scope, and the inventory life cycle all at once.
//
// Coding Instructions:
//   Every state a real screen needs is here on purpose — loading, empty,
//   permission-denied, failure, and retry (doc 10 §5). A screen that only
//   renders the happy path is not finished.

import { useCallback, useEffect, useState } from 'react';
import { useSearchParams } from 'react-router';
import { ApiError, api, post } from '../../shared/api';
import {
  inventoryStatuses,
  type InventoryStatus,
  type InventoryUnitDetail,
  type InventoryUnitSummary,
} from '../../shared/contracts';
import { useI18n } from '../../shared/i18n';
import { useEnumLabel } from '../../shared/i18n/enums';
import { useApiMessage } from '../../shared/i18n/apiMessage';
import { RecallCheck } from '../vehicles/RecallCheck';
import { TakeIntoStock } from './TakeIntoStock';

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
  const [selected, setSelected] = useState<InventoryUnitDetail | null>(null);
  const [taking, setTaking] = useState(false);

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

  async function open(unitId: string) {
    try {
      setSelected(await api<InventoryUnitDetail>(`/inventory/${unitId}`));
    } catch (failure) {
      setLoad({ kind: 'failed', message: describe(failure) });
    }
  }

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

        <button type="button" onClick={() => setTaking((open) => !open)}>
          {t('stock.takeItIn')}
        </button>
      </header>

      {!taking ? null : (
        <TakeIntoStock
          onCancel={() => setTaking(false)}
          onReceived={(unit) => {
            setTaking(false);
            // Opened straight away rather than announced and left to be found.
            // Adding a customer closes its panel and changes nothing visible,
            // and somebody has to search to learn it worked; a car arriving is
            // the same event and gets the opposite treatment.
            setSelected(unit);
            void fetchUnits();
          }}
        />
      )}

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

      <Body
        load={load}
        onRetry={fetchUnits}
        selectedId={selected?.id ?? null}
        onOpen={(id) => void open(id)}
      />

      {selected === null ? null : (
        <UnitDetail
          unit={selected}
          onClose={() => setSelected(null)}
          onMoved={(moved) => {
            setSelected(moved);
            void fetchUnits();
          }}
        />
      )}
    </>
  );
}

/**
 * ADR-020's detail band: the selected unit, inline, below the list.
 *
 * Below and not instead. The zero-jump rule is the point — the list keeps its
 * filter and its scroll position, and going back is not an operation. A route
 * per record would lose all three every time somebody checked a cost.
 */
function UnitDetail({
  unit,
  onClose,
  onMoved,
}: {
  unit: InventoryUnitDetail;
  onClose: () => void;
  onMoved: (unit: InventoryUnitDetail) => void;
}) {
  const { t, format } = useI18n();
  const label = useEnumLabel();

  return (
    <section className="panel panel--detail" aria-label={t('stock.detailFor', { stock: unit.stockNumber })}>
      <h2>
        <span className="mono" dir="ltr">{unit.stockNumber}</span> — {unit.vehicleDisplayName}
      </h2>

      <dl className="facts">
        <dt>{t('stock.colVin')}</dt>
        <dd className="mono" dir="ltr">{unit.vin}</dd>

        <dt>{t('stock.colStatus')}</dt>
        <dd>
          <span className={`chip chip--${unit.status.toLowerCase()}`}>
            {label('inventoryStatus', unit.status)}
          </span>
        </dd>

        <dt>{t('stock.cost')}</dt>
        <dd>
          {/* A cost of zero is a real figure and must not read as "unknown",
              so the check is for null rather than falsy. */}
          {unit.costAmount === null || unit.costCurrency === null
            ? <span className="muted">{t('stock.costUnknown')}</span>
            : format.money(unit.costAmount, unit.costCurrency)}
        </dd>

        <dt>{t('stock.acquired')}</dt>
        <dd>
          {unit.acquiredOn === null
            ? <span className="muted">—</span>
            : format.date(unit.acquiredOn)}
        </dd>
      </dl>

      <h3>{t('stock.historyTitle')}</h3>
      {unit.history.length === 0 ? (
        <p className="note">{t('stock.historyEmpty')}</p>
      ) : (
        // Newest first, and named for a screen-reader user moving between
        // landmarks — the same shape the deal desk uses.
        <ol className="history" aria-label={t('stock.historyTitle')}>
          {[...unit.history].reverse().map((entry, index) => (
            <li key={`${entry.toStatus}-${entry.occurredAt}-${index}`}>
              <span className="strong">
                {entry.fromStatus === null
                  ? t('stock.takenIn', { to: label('inventoryStatus', entry.toStatus) })
                  : t('stock.moved', {
                      from: label('inventoryStatus', entry.fromStatus),
                      to: label('inventoryStatus', entry.toStatus),
                    })}
              </span>{' '}
              <span className="muted">
                {format.dateTime(entry.occurredAt)}
                {entry.note === null ? '' : ` — ${entry.note}`}
              </span>
            </li>
          ))}
        </ol>
      )}

      {/* Below the history, because "what the regulator says" is a different
          kind of answer from "what we did with this car" — it comes from
          somebody else's records and is about the MODEL. It asks nothing until
          somebody presses the button; see RecallCheck's header. */}
      <RecallCheck vehicleId={unit.vehicleId} />

      <MoveTheCar unit={unit} onMoved={onMoved} />

      <div className="actions">
        <button type="button" onClick={onClose}>
          {t('common.close')}
        </button>
      </div>
    </section>
  );
}

/**
 * Where the car goes next.
 *
 * This existed as an endpoint and as nothing else until 2026-09-10: a car could
 * be put into Reconditioning by a seeder and never leave it, because no screen
 * called `POST /inventory/{id}/status`. The status filter above the list was
 * therefore a filter over something nobody could change.
 *
 * Only the moves the domain actually allows from here are offered, and Sold is
 * never one of them — a car is sold by delivering a deal, which is what posts
 * the sale. Offering it here would be a second way to change the same fact, and
 * the one that skips the ledger.
 */
function MoveTheCar({
  unit,
  onMoved,
}: {
  unit: InventoryUnitDetail;
  onMoved: (unit: InventoryUnitDetail) => void;
}) {
  const { t } = useI18n();
  const label = useEnumLabel();
  const describe = useApiMessage();

  const [note, setNote] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const moves = movesFrom(unit.status);

  async function move(to: InventoryStatus) {
    setError(null);
    setBusy(true);

    try {
      onMoved(
        await post<InventoryUnitDetail>(`/inventory/${unit.id}/status`, {
          status: to,
          note: note.trim() === '' ? null : note.trim(),
        }),
      );
      setNote('');
    } catch (failure) {
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  if (moves.length === 0) {
    return (
      <p className="note">
        {unit.status === 'Sold' ? t('stock.soldNote') : t('stock.noMovesNote')}
      </p>
    );
  }

  return (
    <>
      <h3>{t('stock.moveTitle')}</h3>

      <label htmlFor="move-note">{t('stock.moveNote')}</label>
      <input
        id="move-note"
        value={note}
        onChange={(event) => setNote(event.target.value)}
        autoComplete="off"
      />

      {error === null ? null : (
        <p className="error" role="alert">
          {error}
        </p>
      )}

      <div className="actions">
        {moves.map((to) => (
          <button key={to} type="button" disabled={busy} onClick={() => void move(to)}>
            {t('stock.moveTo', { to: label('inventoryStatus', to) })}
          </button>
        ))}
      </div>
    </>
  );
}

/**
 * The moves a person may make from a given state.
 *
 * Kept here rather than derived from the whole status list because most pairs
 * are nonsense — a Sold car does not go back to Incoming, and a Removed one does
 * not come back at all. The server is still the authority and refuses anything
 * else; this only decides which buttons are worth showing.
 */
function movesFrom(status: InventoryStatus): InventoryStatus[] {
  switch (status) {
    case 'Incoming':
      return ['Reconditioning', 'Available', 'Removed'];
    case 'Reconditioning':
      return ['Available', 'OnHold', 'Removed'];
    case 'Available':
      return ['Reconditioning', 'OnHold', 'Removed'];
    case 'OnHold':
      return ['Available', 'Reconditioning', 'Removed'];
    default:
      // Sold and Removed are ends. A sold car is unwound by reversing the deal,
      // not by typing a different status onto the car.
      return [];
  }
}

function Body({
  load, onRetry, selectedId, onOpen,
}: {
  load: Load;
  onRetry: () => void;
  selectedId: string | null;
  onOpen: (unitId: string) => void;
}) {
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
        <UnitTable units={load.units} selectedId={selectedId} onOpen={onOpen} />
      );
  }
}

function UnitTable({
  units, selectedId, onOpen,
}: {
  units: InventoryUnitSummary[];
  selectedId: string | null;
  onOpen: (unitId: string) => void;
}) {
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
            <tr key={unit.id} aria-selected={unit.id === selectedId}>
              {/* Stock numbers and VINs are codes, not prose: they read left to
                  right whatever the page does, or the bidi algorithm reorders
                  the groups and somebody reads out the wrong VIN. */}
              <td className="mono" dir="ltr">
                {/* A button and not a row-level onClick. A clickable <tr> is
                    invisible to a keyboard and announces nothing; this is
                    reachable by Tab and reads as "open stock number X". */}
                <button
                  type="button"
                  className="cell-open mono"
                  onClick={() => onOpen(unit.id)}
                >
                  {unit.stockNumber}
                </button>
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
