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

const money = (amount: number, currency: string) =>
  new Intl.NumberFormat(undefined, { style: 'currency', currency }).format(amount);

/** Trailing zeros dropped: "18" reads better than "18.000" for a shelf count. */
const quantity = (value: number) => Number(value.toFixed(3)).toLocaleString();

export function PartsPage() {
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
        message: failure instanceof ApiError ? failure.message : 'The catalogue could not be read.',
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
    return <p>Loading the parts catalogue…</p>;
  }

  if (load.kind === 'denied') {
    return (
      <section className="page">
        <h1>Parts</h1>
        <p className="note">
          You do not have access to parts at this location. Ask a manager if you
          think that is wrong.
        </p>
      </section>
    );
  }

  if (load.kind === 'failed') {
    return (
      <section className="page">
        <h1>Parts</h1>
        <p className="error">{load.message}</p>
        <button type="button" onClick={() => void find(search)}>
          Try again
        </button>
      </section>
    );
  }

  return (
    <section className="page">
      <header className="page__head">
        <h1>Parts</h1>
        <button type="button" className="primary" onClick={() => setAdding(true)}>
          Add a part
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
        <label htmlFor="part-search">Find a part</label>
        <input
          id="part-search"
          value={search}
          placeholder="Number or description"
          onChange={(event) => setSearch(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              void find(search);
            }
          }}
        />
        <p className="hint">
          The number is matched however it was typed — MZ-690411, mz690411, and
          MZ 690 411 all find the same part.
        </p>
      </div>

      {load.parts.length === 0 ? (
        <p className="note">
          {search.trim() === '' ? 'Nothing in the catalogue yet.' : 'Nothing matches that.'}
        </p>
      ) : (
        <div className="scroll">
          <table className="table">
            <caption className="visually-hidden">
              Parts, with what is on the shelf at the locations you cover.
            </caption>
            <thead>
              <tr>
                <th scope="col">Number</th>
                <th scope="col">Description</th>
                <th scope="col">Where</th>
                <th scope="col" className="num">
                  On hand
                </th>
                <th scope="col" className="num">
                  Cost each
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
                        <span className="muted">Not stocked</span>
                      : (rooftops.find((r) => r.id === part.rooftopId)?.code ?? 'one location')}
                  </td>
                  <td className="num">
                    {part.quantityOnHand <= 0 ? (
                      <span className="chip chip--warn">None</span>
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
      setError(failure instanceof ApiError ? failure.message : 'That did not work.');
    } finally {
      setBusy(false);
    }
  }

  const current = costing.options.find((o) => o.method === costing.method);

  return (
    <section className="panel">
      <h2>How parts are costed</h2>

      <div className="field">
        <label htmlFor="costing-method">Method</label>
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

      <p className="note">
        This applies to <strong>future sales only</strong>. Work already invoiced
        keeps the cost it was sold at — changing this cannot restate a month you
        have already reported on.
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
      setError(failure instanceof ApiError ? failure.message : 'That did not work.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="panel">
      <h2>Add a part</h2>
      <p className="note">
        A part number means the same component at every location, so this is a
        group-level change. The stock itself belongs to whichever shelf it is
        booked onto.
      </p>

      <div className="field">
        <label htmlFor="new-part-number">Part number</label>
        <input
          id="new-part-number"
          value={partNumber}
          onChange={(event) => setPartNumber(event.target.value)}
        />
      </div>

      <div className="field">
        <label htmlFor="new-part-description">Description</label>
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
          Add it
        </button>
        <button type="button" onClick={onCancel}>
          Cancel
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
      setError(failure instanceof ApiError ? failure.message : 'That did not work.');
    } finally {
      setBusy(false);
    }
  }

  const codeFor = (id: string) => rooftops.find((r) => r.id === id)?.code ?? 'one location';

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
        <p className="note">None of this on any shelf you can see.</p>
      ) : (
        part.stock.map((shelf) => (
          <div key={shelf.rooftopId}>
            <h3>
              {codeFor(shelf.rooftopId)} — {quantity(shelf.quantityOnHand)} on hand at{' '}
              {money(shelf.unitCost, shelf.currency)} each
            </h3>

            {/*
              The deliveries behind that figure. "Why does this cost that?" is a
              question a parts manager asks constantly, and this is the answer.
            */}
            <div className="scroll">
              <table className="table terms">
                <caption className="visually-hidden">
                  Deliveries of {part.partNumber} at {codeFor(shelf.rooftopId)}.
                </caption>
                <thead>
                  <tr>
                    <th scope="col">Received</th>
                    <th scope="col">Note</th>
                    <th scope="col" className="num">
                      Came in
                    </th>
                    <th scope="col" className="num">
                      Left
                    </th>
                    <th scope="col" className="num">
                      Cost each
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {shelf.layers.map((layer) => (
                    <tr key={layer.id}>
                      <td>{new Date(layer.receivedAt).toLocaleDateString()}</td>
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

      <h3>Book a delivery in</h3>

      <div className="row">
        <div className="field">
          <label htmlFor="receipt-rooftop">Onto which shelf</label>
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
          <label htmlFor="receipt-quantity">How many</label>
          <input
            id="receipt-quantity"
            inputMode="decimal"
            value={qty}
            onChange={(event) => setQty(event.target.value)}
          />
        </div>

        <div className="field">
          <label htmlFor="receipt-cost">Cost each</label>
          <input
            id="receipt-cost"
            inputMode="decimal"
            value={unitCost}
            onChange={(event) => setUnitCost(event.target.value)}
          />
        </div>

        <div className="field field--grow">
          <label htmlFor="receipt-reference">Delivery note</label>
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
          Book it in
        </button>
      </div>
    </section>
  );
}
