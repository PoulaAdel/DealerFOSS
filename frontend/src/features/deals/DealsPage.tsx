// DealsPage — the deal desk: what is being sold, and what stage each one is at.
//
// Use:  reachable at /deals. Selecting one opens it.
// Edit: two server-side rules become visible here, and the screen's job is to
//       *explain* them rather than to become the second place they live —
//       a salesperson cannot approve their own deal, and the numbers freeze once
//       a deal is submitted. Both are enforced in DealService. If this file ever
//       starts deciding them instead of reflecting them, the two copies will
//       disagree and the browser's copy will be the wrong one.
//
//       So: a button a caller may not use is *absent*, with a sentence saying
//       why. A disabled button with no explanation teaches nobody anything, and
//       a button that is present and then refused wastes somebody's time.

import { useCallback, useEffect, useState } from 'react';
import { useSearchParams } from 'react-router';
import { ApiError, api, openDocument, post } from '../../shared/api';
import { DealTerms } from './DealTerms';
import { DealProducts } from './DealProducts';
import { StartDeal } from './StartDeal';
import type { DealDetail, DealStatus, DealSummary } from '../../shared/contracts';

const PageSize = 50;

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; deals: DealSummary[] }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

export function DealsPage() {
  // A won enquiry sends the salesperson here with the lead attached. The deal
  // records which enquiry produced it, so this is a real link rather than a
  // convenience: without it the two halves of the same sale sit in the system
  // unaware of each other.
  const [params, setParams] = useSearchParams();
  const fromLead = params.get('leadId');
  const forCustomer = params.get('customerId');

  const [openOnly, setOpenOnly] = useState(true);
  const [load, setLoad] = useState<Load>({ kind: 'loading' });
  const [selected, setSelected] = useState<DealDetail | null>(null);
  const [starting, setStarting] = useState(fromLead !== null);

  const find = useCallback(async (open: boolean) => {
    setLoad({ kind: 'loading' });

    try {
      setLoad({
        kind: 'ready',
        deals: await api<DealSummary[]>(`/deals?openOnly=${open}&limit=${PageSize}`),
      });
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 403) {
        setLoad({ kind: 'denied' });
        return;
      }

      setLoad({
        kind: 'failed',
        message: failure instanceof ApiError ? failure.message : 'Could not load the deals.',
      });
    }
  }, []);

  useEffect(() => {
    void find(openOnly);
  }, [find, openOnly]);

  async function open(dealId: string) {
    try {
      setSelected(await api<DealDetail>(`/deals/${dealId}`));
    } catch (failure) {
      setLoad({
        kind: 'failed',
        message: failure instanceof ApiError ? failure.message : 'Could not open that deal.',
      });
    }
  }

  return (
    <>
      <header className="page__head">
        <h1>Deals</h1>

        <div className="filter">
          <label htmlFor="open-only">Show</label>
          <select
            id="open-only"
            value={openOnly ? 'open' : 'all'}
            onChange={(e) => setOpenOnly(e.target.value === 'open')}
          >
            <option value="open">Still being worked</option>
            <option value="all">Everything</option>
          </select>
        </div>
      </header>

      {starting ? (
        <StartDeal
          leadId={fromLead}
          customerId={forCustomer}
          onStarted={async (deal) => {
            setStarting(false);
            setSelected(deal);
            // The handoff is spent. Leaving it in the address bar would restart
            // the same deal on a refresh, or on the back button.
            setParams({}, { replace: true });
            await find(openOnly);
          }}
          onCancel={() => {
            setStarting(false);
            setParams({}, { replace: true });
          }}
        />
      ) : (
        <div className="actions actions--lead">
          <button type="button" className="primary" onClick={() => setStarting(true)}>
            Start a deal
          </button>
        </div>
      )}

      {selected === null ? null : (
        <DealPanel
          deal={selected}
          onChanged={async (updated) => {
            setSelected(updated);
            await find(openOnly);
          }}
          onClose={() => setSelected(null)}
        />
      )}

      <Body load={load} onRetry={() => void find(openOnly)} onOpen={(id) => void open(id)} />
    </>
  );
}

function DealPanel({
  deal, onChanged, onClose,
}: {
  deal: DealDetail;
  onChanged: (updated: DealDetail) => Promise<void>;
  onClose: () => void;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Not named `print`: that resolves to the global `window.print`, which silently
  // prints the application page instead. The compiler caught it as an unused
  // local, which is a lucky way to find out.
  async function printOrder() {
    setError(null);

    try {
      await openDocument(`/documents/deals/${deal.id}`);
    } catch (failure) {
      setError(failure instanceof ApiError ? failure.message : 'The document could not be opened.');
    }
  }

  async function move(status: DealStatus, note?: string) {
    setError(null);
    setBusy(true);

    try {
      await onChanged(await post<DealDetail>(`/deals/${deal.id}/status`, { status, note }));
    } catch (failure) {
      // The server's refusal is the honest one — it knows who is asking and what
      // rule they hit. Anything invented here would be a guess.
      setError(failure instanceof ApiError ? failure.message : 'That did not work.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="panel panel--deal">
      <header className="page__head">
        <h2>
          {deal.customerName} <span className="muted">·</span> {deal.vehicle}
        </h2>
        <div className="actions">
          {/* Here rather than with the stage buttons, which disappear once a deal
              is delivered — and a delivered deal is exactly when somebody asks for
              another copy of the paperwork. */}
          <button type="button" onClick={() => void printOrder()}>
            Print the order
          </button>
          <button type="button" onClick={onClose}>
            Close
          </button>
        </div>
      </header>

      <p className="muted">
        Stock {deal.stockNumber} · <span className={`chip chip--${deal.status.toLowerCase()}`}>{deal.status}</span>
      </p>

      <div className="scroll">
        <table>
          <caption className="visually-hidden">The numbers on this deal</caption>
          <thead>
            <tr>
              <th scope="col">Line</th>
              <th scope="col">Description</th>
              <th scope="col" className="num">Amount</th>
            </tr>
          </thead>
          <tbody>
            {deal.charges.map((charge, index) => (
              <tr key={`${charge.kind}-${index}`}>
                <td>{charge.kind}</td>
                <td>{charge.description}</td>
                <td className="num">{money(charge.amount, deal.currency)}</td>
              </tr>
            ))}

            {/* Products are on the bill, so they belong in the column that adds
                up to it. Leaving them out made the summary show a vehicle price
                of 41,500 above a total of 42,450 with nothing to explain the
                difference — the same defect the trade-in had, found the same way,
                by reading down the column in a browser. */}
            {deal.products.map((product) => (
              <tr key={product.id}>
                <td>Product</td>
                <td>{product.name}</td>
                <td className="num">{money(product.price, deal.currency)}</td>
              </tr>
            ))}

            {deal.tradeIn === null ? null : (
              <tr>
                <td>Trade-in</td>
                <td>
                  {deal.tradeIn.description}
                  {deal.tradeIn.isNegativeEquity ? (
                    // Worth more owing than the car is worth. It changes what the
                    // customer has to find, so it is said rather than left to be
                    // worked out from two numbers.
                    <> — <span className="strong">owes more than it is worth</span></>
                  ) : null}
                </td>
                {/* Negated, because this column is what the customer owes and a
                    trade-in reduces it. The discount above already shows as a
                    minus; leaving equity positive made the column stop adding
                    up — read down it and you got a different total from the one
                    printed at the bottom. Negative equity flips the other way
                    and correctly *increases* what is due. */}
                <td className="num">{money(-deal.tradeIn.equity, deal.currency)}</td>
              </tr>
            )}
          </tbody>
          <tfoot>
            <tr>
              <td colSpan={2}>Due from the customer</td>
              <td className="num strong">{money(deal.amountDue, deal.currency)}</td>
            </tr>
          </tfoot>
        </table>
      </div>

      {deal.termsAreOpen ? (
        // Mounted, not merely enabled. Once submitted this disappears entirely —
        // a disabled form still looks like somewhere to type, and somebody would
        // fill it in and lose the work.
        <>
          <DealTerms deal={deal} onSaved={onChanged} />
          <DealProducts deal={deal} onChanged={onChanged} />
        </>
      ) : (
        <>
          <p className="note">
            The numbers are frozen. They stopped being editable when this deal was
            submitted, so what a manager approves is what was put in front of them.
          </p>
          {deal.products.length === 0 ? null : <SoldProducts deal={deal} />}
        </>
      )}

      <p className="error" aria-live="polite">
        {error ?? ''}
      </p>

      <Actions deal={deal} busy={busy} onMove={(status, note) => void move(status, note)} />

      <h3>What happened</h3>
      <ol className="history">
        {[...deal.history].reverse().map((entry, index) => (
          <li key={`${entry.toStatus}-${entry.occurredAt}-${index}`}>
            <span className="strong">{entry.toStatus}</span>{' '}
            <span className="muted">
              {new Date(entry.occurredAt).toLocaleString()}
              {entry.note === null ? '' : ` — ${entry.note}`}
            </span>
          </li>
        ))}
      </ol>
    </section>
  );
}

/**
 * The moves available from here.
 *
 * Only the transitions the deal's own status allows are offered. Whether *this
 * caller* may make one is the server's answer, not this file's — so the refusal
 * appears after asking rather than being predicted, and the note below says what
 * the rule is instead of pretending to enforce it.
 */
function Actions({
  deal, busy, onMove,
}: {
  deal: DealDetail;
  busy: boolean;
  onMove: (status: DealStatus, note?: string) => void;
}) {
  if (deal.status === 'Delivered' || deal.status === 'Lost') {
    return <p className="note">This deal is finished. Nothing more can happen to it.</p>;
  }

  return (
    <>
      <div className="actions">
        {deal.status === 'Draft' ? (
          <button type="button" className="primary" disabled={busy} onClick={() => onMove('Submitted')}>
            Send to a manager
          </button>
        ) : null}

        {deal.status === 'Submitted' ? (
          <button type="button" className="primary" disabled={busy} onClick={() => onMove('Approved')}>
            Approve
          </button>
        ) : null}

        {deal.status === 'Approved' ? (
          <button type="button" className="primary" disabled={busy} onClick={() => onMove('Delivered')}>
            Hand the car over
          </button>
        ) : null}

        <button
          type="button"
          disabled={busy}
          onClick={() => onMove('Lost', 'Marked lost from the deal desk.')}
        >
          Mark lost
        </button>
      </div>

      {deal.status === 'Submitted' ? (
        <p className="note">
          Whoever built this deal cannot be the one who approves it. If that is
          you, a manager has to do it.
        </p>
      ) : null}
    </>
  );
}

function Body({
  load, onRetry, onOpen,
}: {
  load: Load;
  onRetry: () => void;
  onOpen: (id: string) => void;
}) {
  switch (load.kind) {
    case 'loading':
      return (
        <p className="state" aria-live="polite">
          Loading the deals…
        </p>
      );

    case 'denied':
      return (
        <p className="state" role="alert">
          You do not have access to deals at this location. Ask a manager if you
          think that is wrong.
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
      return load.deals.length === 0 ? (
        <p className="state">No deals here. One starts when a car is priced for somebody.</p>
      ) : (
        <DealTable deals={load.deals} onOpen={onOpen} />
      );
  }
}

/**
 * What was sold with the car, once the deal is frozen. Read-only by definition —
 * the editor is gone at this point, and this is the record of what a manager
 * approved.
 */
function SoldProducts({ deal }: { deal: DealDetail }) {
  const money = (amount: number) =>
    new Intl.NumberFormat(undefined, { style: 'currency', currency: deal.currency }).format(amount);

  return (
    <>
      <h3>Sold with the car</h3>
      <div className="scroll">
        <table className="table terms">
          <thead>
            <tr>
              <th scope="col">Product</th>
              <th scope="col" className="num">
                Price
              </th>
              <th scope="col" className="num">
                Gross
              </th>
            </tr>
          </thead>
          <tbody>
            {deal.products.map((product) => (
              <tr key={product.id}>
                <td>
                  {product.name}
                  {product.provider === null ? null : (
                    <div className="muted">{product.provider}</div>
                  )}
                </td>
                <td className="num">{money(product.price)}</td>
                <td className="num">{money(product.gross)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="note">{money(deal.productGross)} made on what was sold with the car.</p>
    </>
  );
}

function DealTable({ deals, onOpen }: { deals: DealSummary[]; onOpen: (id: string) => void }) {
  const capped = deals.length >= PageSize;

  return (
    <div className="scroll">
      <table>
        <caption className="visually-hidden">
          {capped ? `The first ${deals.length} deals. There may be more.` : `${deals.length} deals`}
        </caption>
        <thead>
          <tr>
            <th scope="col">Customer</th>
            <th scope="col">Vehicle</th>
            <th scope="col">Stock</th>
            <th scope="col" className="num">Due</th>
            <th scope="col">Stage</th>
          </tr>
        </thead>
        <tbody>
          {deals.map((deal) => (
            <tr key={deal.id}>
              <td>
                {/* A button, not a link: opening a deal changes what this page
                    shows rather than navigating anywhere, and a link that does
                    not navigate breaks middle-click and "open in new tab". */}
                <button type="button" className="link" onClick={() => onOpen(deal.id)}>
                  {deal.customerName}
                </button>
              </td>
              <td>{deal.vehicle}</td>
              <td className="mono">{deal.stockNumber}</td>
              <td className="num">{money(deal.amountDue, deal.currency)}</td>
              <td>
                <span className={`chip chip--${deal.status.toLowerCase()}`}>{deal.status}</span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {capped ? (
        <p className="note note--footer">
          Showing the first {deals.length}. There may be more — narrow it with the
          filter until paging exists.
        </p>
      ) : null}
    </div>
  );
}

/**
 * Money, in the deal's own currency. Formatted by the browser rather than by
 * hand: a dealership near a border sells in more than one, and a hard-coded
 * dollar sign in front of a euro amount is the kind of error nobody reports.
 */
function money(amount: number, currency: string): string {
  return new Intl.NumberFormat(undefined, { style: 'currency', currency }).format(amount);
}
