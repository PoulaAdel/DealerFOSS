// PartsPage — the parts catalogue, the stock on each shelf, and how it is costed.
//
// Use:  reachable at /parts.
// Edit: three things here are deliberate.
//
//       (1) The costing choice is on this screen, and it says out loud that it
//       applies to future sales only. A manager who thinks switching to FIFO
//       will restate last month has been misled by the control, not by the
//       system — the ledger is immutable and a sold line's cost is frozen.
//
//       (2) The options come from the server (`/parts/costing`), including their
//       explanations. The screen keeps no copy of the list: a fourth method
//       added later should appear here without anybody remembering to.
//
//       (3) Layers are shown on a part, not hidden. "Why does this cost that?"
//       is a question a parts manager asks constantly, and the deliveries still
//       on the shelf are the honest answer.

import { useCallback, useEffect, useState } from 'react';
import { ApiError, api, post } from '../../shared/api';
import { useI18n } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';
import { Emphasised } from '../../shared/i18n/Emphasised';
import type {
  PartDetail,
  PartSummary,
  PartsCostingSetting,
  RooftopSummary,
} from '../../shared/contracts';

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; parts: PartSummary[] }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

/**
 * Money and shelf counts, in the reader's language.
 *
 * A hook rather than a module-level constant, because both now depend on the
 * chosen locale — grouping, the decimal mark, and where the currency symbol
 * sits all differ, and the previous `undefined` locale silently followed the
 * operating system instead of the application.
 *
 * Trailing zeros are still dropped: "18" reads better than "18.000" for a
 * shelf count, and `maximumFractionDigits` does that per locale rather than
 * via `toFixed` and a re-parse.
 */
function useAmounts() {
  const { format } = useI18n();

  return {
    money: (amount: number, currency: string) => format.money(amount, currency),
    quantity: (value: number) => format.number(value, { maximumFractionDigits: 3 }),
  };
}

export function PartsPage() {
  const { t } = useI18n();
  const describe = useApiMessage();
  const { money, quantity } = useAmounts();

  const [search, setSearch] = useState('');
  const [load, setLoad] = useState<Load>({ kind: 'loading' });
  const [costing, setCosting] = useState<PartsCostingSetting | null>(null);
  const [rooftops, setRooftops] = useState<RooftopSummary[]>([]);
  const [selected, setSelected] = useState<PartDetail | null>(null);
  const [adding, setAdding] = useState(false);

  const find = useCallback(async (term: string) => {
    setLoad({ kind: 'loading' });

    try {
      const query = term.trim() === '' ? '' : `?search=${encodeURIComponent(term.trim())}`;
      setLoad({ kind: 'ready', parts: await api<PartSummary[]>(`/parts${query}`) });
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 403) {
        setLoad({ kind: 'denied' });
        return;
      }

      setLoad({
        kind: 'failed',
        message: describe(failure),
      });
    }
  }, []);

  useEffect(() => {
    void find('');
  }, [find]);

  useEffect(() => {
    void (async () => {
      try {
        setCosting(await api<PartsCostingSetting>('/parts/costing'));
      } catch {
        setCosting(null);
      }

      try {
        const organization = await api<{ legalEntities: { rooftops: RooftopSummary[] }[] }>('/organization');
        setRooftops(organization.legalEntities.flatMap((entity) => entity.rooftops));
      } catch {
        setRooftops([]);
      }
    })();
  }, []);

  if (load.kind === 'loading') {
    return <p>{t('parts.loading')}</p>;
  }

  if (load.kind === 'denied') {
    return (
      <section className="page">
        <h1>{t('parts.title')}</h1>
        <p className="note">{t('parts.denied')}</p>
      </section>
    );
  }

  if (load.kind === 'failed') {
    return (
      <section className="page">
        <h1>{t('parts.title')}</h1>
        <p className="error">{load.message}</p>
        <button type="button" onClick={() => void find(search)}>
          {t('common.retry')}
        </button>
      </section>
    );
  }

  return (
    <section className="page">
      <header className="page__head">
        <h1>{t('parts.title')}</h1>
        <button type="button" className="primary" onClick={() => setAdding(true)}>
          {t('parts.add')}
        </button>
      </header>

      {costing === null ? null : (
        <Costing
          costing={costing}
          onChanged={async (updated) => {
            setCosting(updated);
            await find(search);
          }}
        />
      )}

      {adding ? (
        <AddPart
          onCancel={() => setAdding(false)}
          onAdded={async () => {
            setAdding(false);
            await find(search);
          }}
        />
      ) : null}

      {selected === null ? null : (
        <Part
          part={selected}
          rooftops={rooftops}
          onClose={() => setSelected(null)}
          onChanged={async (updated) => {
            setSelected(updated);
            await find(search);
          }}
        />
      )}

      <div className="field">
        <label htmlFor="part-search">{t('parts.find')}</label>
        <input
          id="part-search"
          value={search}
          placeholder={t('parts.findPlaceholder')}
          onChange={(event) => setSearch(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              void find(search);
            }
          }}
        />
        <p className="hint">{t('parts.findHint')}</p>
      </div>

      {load.parts.length === 0 ? (
        <p className="note">
          {search.trim() === '' ? t('parts.catalogueEmpty') : t('parts.noMatches')}
        </p>
      ) : (
        <div className="scroll">
          <table className="table">
            <caption className="visually-hidden">{t('parts.caption')}</caption>
            <thead>
              <tr>
                <th scope="col">{t('parts.colNumber')}</th>
                <th scope="col">{t('parts.colDescription')}</th>
                <th scope="col">{t('parts.colWhere')}</th>
                <th scope="col" className="num">
                  {t('parts.colOnHand')}
                </th>
                <th scope="col" className="num">
                  {t('parts.colCostEach')}
                </th>
              </tr>
            </thead>
            <tbody>
              {load.parts.map((part) => (
                <tr key={`${part.id}-${part.rooftopId}`}>
                  <td>
                    <button
                      type="button"
                      className="link"
                      onClick={() =>
                        void api<PartDetail>(`/parts/${part.id}`).then(setSelected)
                      }
                    >
                      {part.partNumber}
                    </button>
                  </td>
                  <td>{part.description}</td>
                  <td>
                    {part.rooftopId === null
                      ? // Never stocked anywhere. Saying "one location" would be a
                        // lie, and a blank cell would read as a loading bug.
                        <span className="muted">{t('parts.notStocked')}</span>
                      : (rooftops.find((r) => r.id === part.rooftopId)?.code ?? t('parts.oneLocation'))}
                  </td>
                  <td className="num">
                    {part.quantityOnHand <= 0 ? (
                      <span className="chip chip--warn">{t('parts.noneOnHand')}</span>
                    ) : (
                      quantity(part.quantityOnHand)
                    )}
                  </td>
                  <td className="num">
                    {part.rooftopId === null ? '—' : money(part.unitCost, part.currency)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

/**
 * The manager's control over how stock is valued. Every option and its
 * explanation comes from the server, so this screen never has to be edited when
 * a method is added.
 */
function Costing({
  costing,
  onChanged,
}: {
  costing: PartsCostingSetting;
  onChanged: (updated: PartsCostingSetting) => Promise<void>;
}) {
  const { t } = useI18n();
  const describe = useApiMessage();

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function choose(method: string) {
    setBusy(true);
    setError(null);

    try {
      await onChanged(await post<PartsCostingSetting>('/parts/costing', { method }));
    } catch (failure) {
      // Changing this needs organization-wide permission, and the server is the
      // one that knows whether this caller has it.
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  const current = costing.options.find((o) => o.method === costing.method);

  return (
    <section className="panel">
      <h2>{t('parts.costingTitle')}</h2>

      <div className="field">
        <label htmlFor="costing-method">{t('parts.costingMethod')}</label>
        <select
          id="costing-method"
          value={costing.method}
          disabled={busy}
          onChange={(event) => void choose(event.target.value)}
        >
          {costing.options.map((option) => (
            <option key={option.method} value={option.method}>
              {option.name}
            </option>
          ))}
        </select>
      </div>

      {current === undefined ? null : <p className="hint">{current.explanation}</p>}

      {/* The emphasis is on the clause a manager must not miss, and it moves
          with the translation rather than being a fixed slice of the sentence. */}
      <p className="note">
        <Emphasised
          sentence={t('parts.costingApplies', {
            futureOnly: t('parts.costingFutureOnly'),
            rest: t('parts.costingNote'),
          })}
          value={t('parts.costingFutureOnly')}
        />
      </p>

      <p className="error" aria-live="polite">
        {error ?? ''}
      </p>
    </section>
  );
}

function AddPart({
  onCancel,
  onAdded,
}: {
  onCancel: () => void;
  onAdded: () => Promise<void>;
}) {
  const { t } = useI18n();
  const describe = useApiMessage();

  const [partNumber, setPartNumber] = useState('');
  const [description, setDescription] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    setBusy(true);
    setError(null);

    try {
      await post<PartDetail>('/parts', { partNumber, description });
      await onAdded();
    } catch (failure) {
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="panel">
      <h2>{t('parts.addTitle')}</h2>
      <p className="note">{t('parts.addLede')}</p>

      <div className="field">
        <label htmlFor="new-part-number">{t('parts.partNumber')}</label>
        <input
          id="new-part-number"
          value={partNumber}
          onChange={(event) => setPartNumber(event.target.value)}
        />
      </div>

      <div className="field">
        <label htmlFor="new-part-description">{t('parts.description')}</label>
        <input
          id="new-part-description"
          value={description}
          onChange={(event) => setDescription(event.target.value)}
        />
      </div>

      <p className="error" aria-live="polite">
        {error ?? ''}
      </p>

      <div className="actions">
        <button
          type="button"
          className="primary"
          disabled={busy || partNumber.trim() === '' || description.trim() === ''}
          onClick={() => void submit()}
        >
          {t('parts.addIt')}
        </button>
        <button type="button" onClick={onCancel}>
          {t('common.cancel')}
        </button>
      </div>
    </section>
  );
}

function Part({
  part,
  rooftops,
  onClose,
  onChanged,
}: {
  part: PartDetail;
  rooftops: RooftopSummary[];
  onClose: () => void;
  onChanged: (updated: PartDetail) => Promise<void>;
}) {
  const { t, format } = useI18n();
  const describe = useApiMessage();
  const { money, quantity } = useAmounts();

  const [rooftopId, setRooftopId] = useState(rooftops[0]?.id ?? '');
  const [qty, setQty] = useState('');
  const [unitCost, setUnitCost] = useState('');
  const [reference, setReference] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function receive() {
    setBusy(true);
    setError(null);

    try {
      const updated = await post<PartDetail>(`/parts/${part.id}/receipts`, {
        quantity: Number(qty),
        unitCost: Number(unitCost),
        rooftopId,
        reference: reference.trim() === '' ? null : reference.trim(),
      });

      setQty('');
      setUnitCost('');
      setReference('');
      await onChanged(updated);
    } catch (failure) {
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  // The rooftop CODE is the dealership's own label and is printed as they set it.
  const codeFor = (id: string) => rooftops.find((r) => r.id === id)?.code ?? t('parts.oneLocation');

  return (
    <section className="panel panel--deal">
      <header className="page__head">
        <h2>
          {part.partNumber} <span className="muted">·</span> {part.description}
        </h2>
        <button type="button" onClick={onClose}>
          Close
        </button>
      </header>

      {part.stock.length === 0 ? (
        <p className="note">{t('parts.noneVisible')}</p>
      ) : (
        part.stock.map((shelf) => (
          <div key={shelf.rooftopId}>
            <h3>
              {t('parts.shelfHeading', {
                code: codeFor(shelf.rooftopId),
                quantity: quantity(shelf.quantityOnHand),
                cost: money(shelf.unitCost, shelf.currency),
              })}
            </h3>

            {/*
              The deliveries behind that figure. "Why does this cost that?" is a
              question a parts manager asks constantly, and this is the answer.
            */}
            <div className="scroll">
              <table className="table terms">
                <caption className="visually-hidden">
                  {t('parts.deliveriesCaption', {
                    part: part.partNumber,
                    code: codeFor(shelf.rooftopId),
                  })}
                </caption>
                <thead>
                  <tr>
                    <th scope="col">{t('parts.colReceived')}</th>
                    <th scope="col">{t('parts.colNote')}</th>
                    <th scope="col" className="num">
                      {t('parts.colCameIn')}
                    </th>
                    <th scope="col" className="num">
                      {t('parts.colLeft')}
                    </th>
                    <th scope="col" className="num">
                      {t('parts.colCostEach')}
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {shelf.layers.map((layer) => (
                    <tr key={layer.id}>
                      <td>{format.date(layer.receivedAt)}</td>
                      <td>{layer.reference ?? ''}</td>
                      <td className="num">{quantity(layer.quantityReceived)}</td>
                      <td className="num">{quantity(layer.remainingQuantity)}</td>
                      <td className="num">{money(layer.unitCost, shelf.currency)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        ))
      )}

      <h3>{t('parts.bookIn')}</h3>

      <div className="row">
        <div className="field">
          <label htmlFor="receipt-rooftop">{t('parts.ontoWhichShelf')}</label>
          <select
            id="receipt-rooftop"
            value={rooftopId}
            onChange={(event) => setRooftopId(event.target.value)}
          >
            {rooftops.map((rooftop) => (
              <option key={rooftop.id} value={rooftop.id}>
                {rooftop.code} — {rooftop.name}
              </option>
            ))}
          </select>
        </div>

        <div className="field">
          <label htmlFor="receipt-quantity">{t('parts.howMany')}</label>
          <input
            id="receipt-quantity"
            inputMode="decimal"
            value={qty}
            onChange={(event) => setQty(event.target.value)}
          />
        </div>

        <div className="field">
          <label htmlFor="receipt-cost">{t('parts.costEach')}</label>
          <input
            id="receipt-cost"
            inputMode="decimal"
            value={unitCost}
            onChange={(event) => setUnitCost(event.target.value)}
          />
        </div>

        <div className="field field--grow">
          <label htmlFor="receipt-reference">{t('parts.deliveryNote')}</label>
          <input
            id="receipt-reference"
            value={reference}
            onChange={(event) => setReference(event.target.value)}
          />
        </div>
      </div>

      <p className="error" aria-live="polite">
        {error ?? ''}
      </p>

      <div className="actions">
        <button
          type="button"
          disabled={busy || qty === '' || unitCost === '' || rooftopId === ''}
          onClick={() => void receive()}
        >
          {t('parts.bookItIn')}
        </button>
      </div>
    </section>
  );
}
