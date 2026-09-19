// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   DealsPage — the deal desk: what is being sold, and what stage each one is at.
//
// Usage:
//   Reachable at /deals. Selecting one opens it.
//
// Coding Instructions:
//   Two server-side rules become visible here, and the screen's job is to
//   *explain* them rather than to become the second place they live —
//   a salesperson cannot approve their own deal, and the numbers freeze once
//   a deal is submitted. Both are enforced in DealService. If this file ever
//   starts deciding them instead of reflecting them, the two copies will
//   disagree and the browser's copy will be the wrong one.
//
//   So: a button a caller may not use is *absent*, with a sentence saying
//   why. A disabled button with no explanation teaches nobody anything, and
//   a button that is present and then refused wastes somebody's time.
//
//   The open deal lives at `/deals/:id` and the query string DOES NOT travel
//   with it — `carryQuery` stays off here on purpose. `?leadId=` is a one-shot
//   instruction to start a deal, not a filter, and a link carrying it would
//   start a second deal on the same enquiry for whoever opened it.

import { useCallback, useEffect, useState } from 'react';
import { useSearchParams } from 'react-router';
import { ApiError, api, openDocument, post } from '../../shared/api';
import { RecordBandStatus } from '../../shared/RecordBand';
import { useRecordRoute } from '../../shared/useRecordRoute';
import { DealTerms } from './DealTerms';
import { DealProducts } from './DealProducts';
import { DealTax, SoldTax } from './DealTax';
import { TakePayment } from '../receivables/TakePayment';
import { StartDeal } from './StartDeal';
import type { DealDetail, DealStatus, DealSummary, Page } from '../../shared/contracts';
import { useI18n } from '../../shared/i18n';
import { useEnumLabel } from '../../shared/i18n/enums';
import { useApiMessage } from '../../shared/i18n/apiMessage';
import { ListTable } from '../../shared/ListScreen';

const PageSize = 50;

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; page: Page<DealSummary> }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

export function DealsPage() {
  // A won enquiry sends the salesperson here with the lead attached. The deal
  // records which enquiry produced it, so this is a real link rather than a
  // convenience: without it the two halves of the same sale sit in the system
  // unaware of each other.
  const { t, format } = useI18n();
  const describe = useApiMessage();

  const [params, setParams] = useSearchParams();
  const fromLead = params.get('leadId');
  const forCustomer = params.get('customerId');

  const [openOnly, setOpenOnly] = useState(true);

  // Which page. Reset when the filter changes: page 3 of the open deals is not
  // page 3 of all of them.
  const [offset, setOffset] = useState(0);
  const [load, setLoad] = useState<Load>({ kind: 'loading' });
  const [starting, setStarting] = useState(fromLead !== null);

  const record = useRecordRoute<DealDetail>({
    area: '/deals',
    load: (dealId, signal) => api<DealDetail>(`/deals/${dealId}`, { signal }),
  });

  const find = useCallback(async (open: boolean, from: number) => {
    setLoad({ kind: 'loading' });

    try {
      setLoad({
        kind: 'ready',
        page: await api<Page<DealSummary>>(
          `/deals?openOnly=${open}&limit=${PageSize}&offset=${from}`,
        ),
      });
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 403) {
        setLoad({ kind: 'denied' });
        return;
      }

      setLoad({ kind: 'failed', message: describe(failure) });
    }
  }, [describe]);

  useEffect(() => {
    void find(openOnly, offset);
  }, [find, openOnly, offset]);

  // Submitted and not yet signed off. The server decides who may approve; this
  // is only the list of what is blocked, and it is drawn from the summary
  // already on screen rather than fetched again.
  const awaitingApproval =
    load.kind === 'ready' ? load.page.rows.filter((deal) => deal.status === 'Submitted') : [];

  return (
    <>
      <header className="page__head">
        <h1>{t('deals.title')}</h1>

        <div className="filter">
          <label htmlFor="open-only">{t('deals.show')}</label>
          <select
            id="open-only"
            value={openOnly ? 'open' : 'all'}
            onChange={(e) => {
              setOpenOnly(e.target.value === 'open');
              setOffset(0);
            }}
          >
            <option value="open">{t('deals.stillWorked')}</option>
            <option value="all">{t('deals.everything')}</option>
          </select>
        </div>
      </header>

      {starting ? (
        <StartDeal
          leadId={fromLead}
          customerId={forCustomer}
          onStarted={async (deal) => {
            setStarting(false);
            // The handoff is spent, and this is what spends it: `carryQuery` is
            // off for this screen, so navigating to the new deal's own address
            // leaves `?leadId=` behind. Leaving it in the address bar would
            // restart the same deal on a refresh, or on the back button.
            record.openWith(deal.id, deal);
            await find(openOnly, offset);
          }}
          onCancel={() => {
            setStarting(false);
            setParams({}, { replace: true });
          }}
        />
      ) : (
        <div className="actions actions--lead">
          <button type="button" className="primary" onClick={() => setStarting(true)}>
            {t('deals.start')}
          </button>
        </div>
      )}

      {/* ADR-020's signal band: what needs a person NOW, above the list of
          everything. A submitted deal is a car somebody has sold and cannot
          hand over, and the manager who has to sign it off has no other way to
          know it is sitting there. Drawn from the summary already loaded, so it
          costs no extra request — and it disappears when the queue is empty,
          because a permanent "nothing to approve" teaches people to stop
          looking at this spot. */}
      {awaitingApproval.length === 0 ? null : (
        <section className="panel panel--signal">
          <h2>{t('deals.awaitingTitle')}</h2>
          <p className="note">
            {t('deals.awaitingNote', { count: awaitingApproval.length })}
          </p>
          <ul className="calls">
            {awaitingApproval.map((deal) => (
              <li key={deal.id}>
                {/* Led by the stock number, as the workshop's band is led by
                    the job number. It names the specific car — which is what a
                    manager asks about — and keeps this control distinct from
                    the customer-name button on the row below. */}
                <button type="button" className="link" onClick={() => record.open(deal.id)}>
                  <span dir="ltr">{deal.stockNumber}</span> — {deal.customerName}
                </button>{' '}
                <span className="muted">
                  {deal.vehicle} · {format.money(deal.amountDue, deal.currency)}
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}

      <RecordBandStatus route={record} />

      {record.state.kind !== 'open' ? null : (
        <DealPanel
          deal={record.state.record}
          onChanged={async (updated) => {
            record.refresh(updated);
            await find(openOnly, offset);
          }}
          onClose={record.close}
        />
      )}

      <Body
        load={load}
        onRetry={() => void find(openOnly, offset)}
        onOpen={record.open}
        onPage={setOffset}
      />
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
  const { t, format } = useI18n();
  const label = useEnumLabel();
  const describe = useApiMessage();
  const money = (amount: number) => format.money(amount, deal.currency);

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
      setError(describe(failure));
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
      setError(describe(failure));
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
            {t('deals.printOrder')}
          </button>
          <button type="button" onClick={onClose}>
            {t('common.close')}
          </button>
        </div>
      </header>

      <p className="muted">
        {t('deals.stockLine', { stock: deal.stockNumber })} ·{' '}
        <span className={`chip chip--${deal.status.toLowerCase()}`}>
          {label('dealStatus', deal.status)}
        </span>
      </p>

      <div className="scroll">
        <table>
          <caption className="visually-hidden">{t('deals.numbersCaption')}</caption>
          <thead>
            <tr>
              <th scope="col">{t('deals.colLine')}</th>
              <th scope="col">{t('deals.colDescription')}</th>
              <th scope="col" className="num">
                {t('deals.colAmount')}
              </th>
            </tr>
          </thead>
          <tbody>
            {deal.charges.map((charge, index) => (
              <tr key={`${charge.kind}-${index}`}>
                {/* The KIND is our vocabulary and is translated. The DESCRIPTION
                    is what a salesperson typed on this deal and is printed
                    exactly as they wrote it. */}
                <td>{label('chargeKind', charge.kind)}</td>
                <td>{charge.description}</td>
                <td className="num">{money(charge.amount)}</td>
              </tr>
            ))}

            {/* Products are on the bill, so they belong in the column that adds
                up to it. Leaving them out made the summary show a vehicle price
                of 41,500 above a total of 42,450 with nothing to explain the
                difference — the same defect the trade-in had, found the same way,
                by reading down the column in a browser. */}
            {deal.products.map((product) => (
              <tr key={product.id}>
                <td>{t('deals.lineProduct')}</td>
                <td>{product.name}</td>
                <td className="num">{money(product.price)}</td>
              </tr>
            ))}

            {deal.tradeIn === null ? null : (
              <tr>
                <td>{t('deals.lineTradeIn')}</td>
                <td>
                  {deal.tradeIn.description}
                  {deal.tradeIn.isNegativeEquity ? (
                    // Worth more owing than the car is worth. It changes what the
                    // customer has to find, so it is said rather than left to be
                    // worked out from two numbers.
                    <>
                      {' — '}
                      <span className="strong">{t('deals.owesMore')}</span>
                    </>
                  ) : null}
                </td>
                {/* Negated, because this column is what the customer owes and a
                    trade-in reduces it. The discount above already shows as a
                    minus; leaving equity positive made the column stop adding
                    up — read down it and you got a different total from the one
                    printed at the bottom. Negative equity flips the other way
                    and correctly *increases* what is due. */}
                <td className="num">{money(-deal.tradeIn.equity)}</td>
              </tr>
            )}
          </tbody>
          <tfoot>
            <tr>
              <td colSpan={2}>{t('deals.dueFromCustomer')}</td>
              <td className="num strong">{money(deal.amountDue)}</td>
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
          <DealTax deal={deal} onChanged={onChanged} />
        </>
      ) : (
        <>
          <p className="note">{t('deals.frozen')}</p>
          {deal.products.length === 0 ? null : <SoldProducts deal={deal} onChanged={onChanged} />}
          {deal.taxLines.length === 0 ? null : <SoldTax deal={deal} />}

          {/* Renders nothing until the car is delivered, because nothing is
              owed until then. A deal being worked is not a debt. */}
          <TakePayment source="Deal" reference={deal.id} watch={deal.status} />
        </>
      )}

      <p className="error" aria-live="polite">
        {error ?? ''}
      </p>

      <Actions deal={deal} busy={busy} onMove={(status, note) => void move(status, note)} />

      <h3>{t('deals.whatHappened')}</h3>
      {/* Named, so it is distinguishable from the other lists on the screen —
          by a screen-reader user moving between landmarks as much as by a test. */}
      <ol className="history" aria-label={t('deals.whatHappened')}>
        {[...deal.history].reverse().map((entry, index) => (
          <li key={`${entry.toStatus}-${entry.occurredAt}-${index}`}>
            <span className="strong">{label('dealStatus', entry.toStatus)}</span>{' '}
            <span className="muted">
              {format.dateTime(entry.occurredAt)}
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
  const { t } = useI18n();

  if (deal.status === 'Delivered' || deal.status === 'Lost') {
    return <p className="note">{t('deals.finished')}</p>;
  }

  return (
    <>
      <div className="actions">
        {deal.status === 'Draft' ? (
          <button type="button" className="primary" disabled={busy} onClick={() => onMove('Submitted')}>
            {t('deals.sendToManager')}
          </button>
        ) : null}

        {deal.status === 'Submitted' ? (
          <button type="button" className="primary" disabled={busy} onClick={() => onMove('Approved')}>
            {t('deals.approve')}
          </button>
        ) : null}

        {deal.status === 'Approved' ? (
          <button type="button" className="primary" disabled={busy} onClick={() => onMove('Delivered')}>
            {t('deals.handOver')}
          </button>
        ) : null}

        {/* The note travels to the server and lands in the deal's permanent
            history, so it is written in the language of whoever marked it —
            which is the honest record of who did what. */}
        <button
          type="button"
          disabled={busy}
          onClick={() => onMove('Lost', t('deals.markedLostNote'))}
        >
          {t('deals.markLost')}
        </button>
      </div>

      {deal.status === 'Submitted' ? <p className="note">{t('deals.cannotApproveOwn')}</p> : null}
    </>
  );
}

function Body({
  load, onRetry, onOpen, onPage,
}: {
  load: Load;
  onRetry: () => void;
  onPage: (offset: number) => void;
  onOpen: (id: string) => void;
}) {
  const { t } = useI18n();

  switch (load.kind) {
    case 'loading':
      return (
        <p className="state" aria-live="polite">
          {t('deals.loading')}
        </p>
      );

    case 'denied':
      return (
        <p className="state" role="alert">
          {t('deals.denied')}
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
      return load.page.total === 0 ? (
        <p className="state">{t('deals.empty')}</p>
      ) : (
        <DealTable page={load.page} onOpen={onOpen} onPage={onPage} />
      );
  }
}

/**
 * What was sold with the car, once the deal is frozen. Read-only by definition —
 * the editor is gone at this point, and this is the record of what a manager
 * approved.
 */
function SoldProducts({
  deal, onChanged,
}: {
  deal: DealDetail;
  onChanged: (updated: DealDetail) => void;
}) {
  const { t, format } = useI18n();
  const money = (amount: number) => format.money(amount, deal.currency);

  // Which product's cancel form is open, if any. One at a time — cancelling
  // is a deliberate act with its own refund figure, not a batch operation.
  const [cancelling, setCancelling] = useState<string | null>(null);

  return (
    <>
      <h3>{t('deals.soldWithTheCar')}</h3>
      <div className="scroll">
        <table className="table terms">
          <thead>
            <tr>
              <th scope="col">{t('deals.colProduct')}</th>
              <th scope="col" className="num">
                {t('deals.colPrice')}
              </th>
              <th scope="col" className="num">
                {t('deals.colGross')}
              </th>
              {/* Cancelling only ever makes sense once the car has actually been
                  delivered — nothing was charged for it before that, and
                  DealService refuses the attempt anyway. Rather than show a
                  button that always fails on an earlier status, the column is
                  absent entirely. */}
              {deal.status === 'Delivered' ? <th scope="col" aria-hidden="true" /> : null}
            </tr>
          </thead>
          <tbody>
            {deal.products.map((product) => (
              <tr key={product.id} className={product.isCancelled ? 'muted' : undefined}>
                <td>
                  {product.name}
                  {product.provider === null ? null : (
                    <div className="muted">{product.provider}</div>
                  )}
                  {product.isCancelled ? (
                    <div className="muted">
                      {t('deals.productCancelled', {
                        date: format.date(product.cancelledAt!),
                        refund: money(product.refundAmount ?? 0),
                      })}
                      {product.cancellationReason === null ? '' : ` — ${product.cancellationReason}`}
                    </div>
                  ) : null}
                </td>
                <td className="num">{money(product.price)}</td>
                <td className="num">{money(product.gross)}</td>
                {deal.status === 'Delivered' ? (
                  <td>
                    {product.isCancelled ? null : cancelling === product.id ? null : (
                      <button type="button" onClick={() => setCancelling(product.id)}>
                        {t('deals.cancelProduct')}
                      </button>
                    )}
                  </td>
                ) : null}
              </tr>
            ))}

            {cancelling === null ? null : (
              <tr>
                <td colSpan={deal.status === 'Delivered' ? 4 : 3}>
                  <CancelProductForm
                    deal={deal}
                    product={deal.products.find((p) => p.id === cancelling)!}
                    onDone={(updated) => {
                      if (updated !== null) {
                        onChanged(updated);
                      }
                      setCancelling(null);
                    }}
                  />
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
      <p className="note">{t('deals.productGross', { amount: money(deal.productGross) })}</p>
    </>
  );
}

function CancelProductForm({
  deal, product, onDone,
}: {
  deal: DealDetail;
  product: DealDetail['products'][number];
  onDone: (updated: DealDetail | null) => void;
}) {
  const { t, format } = useI18n();
  const describe = useApiMessage();

  const [refund, setRefund] = useState(String(product.price));
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function confirm() {
    setBusy(true);
    setError(null);

    try {
      onDone(
        await post<DealDetail>(`/deals/${deal.id}/products/${product.id}/cancel`, {
          refundAmount: Number(refund) || 0,
          reason: reason.trim() === '' ? null : reason.trim(),
        }),
      );
    } catch (failure) {
      setError(describe(failure));
      setBusy(false);
    }
  }

  return (
    <section className="panel-inset" aria-label={t('deals.cancelProductTitle', { product: product.name })}>
      <h4>{t('deals.cancelProductTitle', { product: product.name })}</h4>
      <p className="note">
        {t('deals.cancelProductNote', { max: format.money(product.price, deal.currency) })}
      </p>

      <label htmlFor={`refund-${product.id}`}>{t('deals.refundAmount')}</label>
      <input
        id={`refund-${product.id}`}
        inputMode="decimal"
        value={refund}
        disabled={busy}
        onChange={(event) => setRefund(event.target.value)}
      />

      <label htmlFor={`reason-${product.id}`}>{t('deals.cancelReason')}</label>
      <input
        id={`reason-${product.id}`}
        value={reason}
        disabled={busy}
        autoComplete="off"
        onChange={(event) => setReason(event.target.value)}
      />

      <p className="error" aria-live="polite">
        {error ?? ''}
      </p>

      <div className="actions">
        <button type="button" disabled={busy} onClick={() => void confirm()}>
          {busy ? t('common.saving') : t('deals.confirmCancelProduct')}
        </button>
        <button type="button" disabled={busy} onClick={() => onDone(null)}>
          {t('common.cancel')}
        </button>
      </div>
    </section>
  );
}

function DealTable({
  page, onOpen, onPage,
}: {
  page: Page<DealSummary>;
  onOpen: (id: string) => void;
  onPage: (offset: number) => void;
}) {
  const { t, format } = useI18n();
  const label = useEnumLabel();

  return (
    <ListTable
      page={page}
      onPage={onPage}
      columns={
        <>
          <th scope="col">{t('deals.colCustomer')}</th>
          <th scope="col">{t('deals.colVehicle')}</th>
          <th scope="col">{t('deals.colStock')}</th>
          <th scope="col" className="num">
            {t('deals.colDue')}
          </th>
          <th scope="col">{t('deals.colStage')}</th>
        </>
      }
      row={(deal) => (
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
          <td className="mono" dir="ltr">
            {deal.stockNumber}
          </td>
          <td className="num">{format.money(deal.amountDue, deal.currency)}</td>
          <td>
            <span className={`chip chip--${deal.status.toLowerCase()}`}>
              {label('dealStatus', deal.status)}
            </span>
          </td>
        </tr>
      )}
    />
  );
}
