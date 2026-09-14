// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   TakePayment — what is still owed on one bill, and taking money against it.
//
//   One band, used by both the job sheet and the deal desk, because "what does
//   this customer still owe and how are they paying it" is the same question
//   whether the thing sold was a car or a clutch. Two copies of it would drift,
//   and the arithmetic is the part that must not.
//
// Usage:
//   <TakePayment source="RepairOrder" reference={job.id} />
//   Looks the receivable up itself. Renders nothing at all until there is one,
//   which is right: a job still on the ramp is not owed yet.
//
// Coding Instructions:
//   THE SERVER OWNS THE ARITHMETIC. `outstanding` comes back derived from the
//   payments; never compute it here by subtracting what was just typed.
//
//   MORE THAN IS OWED IS ALLOWED, AND THAT IS THE POINT. This band used to refuse
//   it before sending, matching a server that refused it too. Both were wrong in
//   the same way: a customer paying a $1,340.50 invoice with $1,400 in cash has
//   not made a mistake, and there is no version of "we cannot accept that" a
//   service counter can say out loud. The overpayment now becomes a credit the
//   dealership owes back, and the band SAYS SO BEFORE THE MONEY IS TAKEN — a
//   person about to key $1,400 sees what will happen to the extra $59.50 while
//   they can still change it.
//
//   A settled bill still shows. "Paid in full on the 3rd, by card" is the answer
//   somebody wants at the counter, and hiding the band once it is settled would
//   make the receipt unfindable.

import { useCallback, useEffect, useState } from 'react';
import { ApiError, api, post } from '../../shared/api';
import {
  paymentMethods,
  refundMethods,
  type CreditSummary,
  type Page,
  type PaymentMethod,
  type ReceivableDetail,
  type ReceivableSource,
} from '../../shared/contracts';
import { useI18n } from '../../shared/i18n';
import { useEnumLabel } from '../../shared/i18n/enums';
import { useApiMessage } from '../../shared/i18n/apiMessage';

export function TakePayment({
  source,
  reference,
  watch,
}: {
  source: ReceivableSource;
  reference: string;

  /**
   * Anything whose change means the debt may have appeared or moved — in
   * practice the job's or the deal's status.
   *
   * Without this the band looked up its receivable once, when the record was
   * opened, and never again. Invoicing a job in the same session then showed
   * nothing at all: the lookup had already answered "nothing is owed" while the
   * job was still on the ramp, and the reference it depends on does not change
   * when the status does. It looked right on a fresh page load and was wrong in
   * the one moment somebody would actually use it. Found by walking it, not by a
   * test — the tests mount the band against a bill that already exists.
   */
  watch?: unknown;
}) {
  const { t, format } = useI18n();
  const label = useEnumLabel();
  const describe = useApiMessage();

  const [owed, setOwed] = useState<ReceivableDetail | null>(null);
  const [credits, setCredits] = useState<CreditSummary[]>([]);
  const [amount, setAmount] = useState('');
  const [method, setMethod] = useState<PaymentMethod>('Card');
  const [note, setNote] = useState('');

  const [refundMethod, setRefundMethod] = useState<PaymentMethod>('BankTransfer');

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  /**
   * Empties the credit list WITHOUT forcing a render when it is already empty.
   *
   * `setCredits([])` would look identical and is not: a fresh array is never
   * `Object.is`-equal to the old one, so React re-renders every time. On a job
   * still on the ramp — the 204 case, which is most job sheets most of the time
   * — that is a state update after an await for no change at all, and the tests
   * report it as an update not wrapped in `act(...)`. `setOwed(null)` in the
   * same spot is silent precisely because null already equals null.
   *
   * Returning the current array when it is already empty restores that.
   */
  const clearCredits = useCallback(
    () => setCredits((current) => (current.length === 0 ? current : [])),
    [],
  );

  const load = useCallback(async () => {
    let found: ReceivableDetail | undefined;

    try {
      // 204 means "nothing is owed against this yet", which is an ordinary
      // answer rather than a missing page — a deal still being worked has no
      // receivable. api() hands back `undefined` for a no-content reply, so it
      // is normalised here: one absent value, checked in one place.
      found = await api<ReceivableDetail | undefined>(
        `/receivables/for/${source}/${reference}`);

      setOwed(found ?? null);
    } catch (failure) {
      // A band that cannot load must not take the screen down with it. The job
      // sheet above it is still the useful thing.
      if (failure instanceof ApiError && failure.status === 403) {
        setOwed(null);
        clearCredits();
        return;
      }

      setError(describe(failure));
      return;
    }

    if (found === undefined) {
      clearCredits();
      return;
    }

    // Everything this customer is owed, not only what THIS bill produced.
    // Somebody who overpaid a service invoice in March should be able to put it
    // against the tyres they are buying today, and the counter cannot do that
    // if the credit is only visible on the bill that created it.
    //
    // ITS OWN try, AND FAILURE IS SILENT. This is an extra convenience on top of
    // a band whose real job is taking money. A credits lookup that 403s or falls
    // over must not stop somebody recording a payment — that would turn a
    // secondary feature into an outage at the counter.
    try {
      setCredits(
        (await api<Page<CreditSummary>>(
          `/receivables/credits?customerId=${found.customerId}`)).rows,
      );
    } catch {
      setCredits([]);
    }
  }, [source, reference, watch, describe, clearCredits]);

  useEffect(() => {
    void load();
  }, [load]);

  if (owed === null) {
    return error === null ? null : (
      <p className="error" role="alert">
        {error}
      </p>
    );
  }

  const typed = Number(amount);
  const ready =
    !owed.isSettled
    && amount.trim() !== ''
    && !Number.isNaN(typed)
    && typed > 0;

  // What will become a credit if this is sent as typed. Shown before the button
  // is pressed, not after: the extra is the customer's money and they are still
  // standing there.
  const willOverpay = ready && typed > owed.outstanding ? typed - owed.outstanding : 0;

  // Money already held for this person that could settle part of this bill. Not
  // offered on a settled bill: there would be nothing for it to pay.
  const usable = owed.isSettled ? [] : credits.filter((credit) => !credit.isSpent);

  // Credits THIS bill produced and nobody has dealt with yet. Shown even when
  // the bill is settled, because a settled bill that quietly holds somebody's
  // change is exactly the state this feature exists to make visible.
  const raised = owed.creditsRaised.filter((credit) => !credit.isSpent);

  async function take() {
    if (owed === null) {
      return;
    }

    setError(null);
    setBusy(true);

    try {
      setOwed(
        await post<ReceivableDetail>(`/receivables/${owed.id}/payments`, {
          amount: typed,
          method,
          note: note.trim() === '' ? null : note.trim(),
        }),
      );

      setAmount('');
      setNote('');
    } catch (failure) {
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  /**
   * Puts a credit the dealership is already holding against this bill.
   *
   * No cash moves — the money arrived when they overpaid something — so this
   * rides on the same right as taking a payment. The amount is capped at
   * whichever is smaller, what is left on the credit or what is left on the
   * bill; the server caps it too, and the server is what decides.
   */
  async function useHere(creditId: string, amount: number) {
    if (owed === null) {
      return;
    }

    setError(null);
    setBusy(true);

    try {
      await post(`/receivables/credits/${creditId}/apply`, {
        receivableId: owed.id,
        amount,
        note: null,
      });

      await load();
    } catch (failure) {
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  /**
   * Hands a credit back to the customer.
   *
   * Needs `Accounting.Refund`, which whoever takes payments may well not hold —
   * taking money and giving it back are different rights. The control is shown
   * to everybody and the server decides, which is this application's current
   * behaviour everywhere and a known gap: "hide actions a role cannot take" is
   * its own register row. The refusal at least says what is missing.
   */
  async function giveBack(creditId: string, remaining: number) {
    setError(null);
    setBusy(true);

    try {
      await post(`/receivables/credits/${creditId}/refund`, {
        amount: remaining,
        method: refundMethod,
        note: null,
      });

      // Re-read rather than patch the credit in place: the bill is what carries
      // the credits, and the server is what decides whether this one is spent.
      await load();
    } catch (failure) {
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="panel" aria-label={t('money.title')}>
      <h3>{t('money.title')}</h3>

      <dl className="facts">
        <dt>{t('money.billed')}</dt>
        <dd className="num">{format.money(owed.amount, owed.currency)}</dd>

        <dt>{t('money.paid')}</dt>
        <dd className="num">{format.money(owed.paid, owed.currency)}</dd>

        <dt>{t('money.outstanding')}</dt>
        <dd className="num strong">{format.money(owed.outstanding, owed.currency)}</dd>
      </dl>

      {owed.isSettled ? (
        <p className="note">{t('money.settled')}</p>
      ) : (
        <p className="note">
          {t('money.owedFor', { count: owed.daysOutstanding })}
        </p>
      )}

      {usable.length === 0 ? null : (
        <div className="panel-inset" aria-label={t('money.creditUsable')}>
          <h4>{t('money.creditUsable')}</h4>
          <p className="note">{t('money.creditUsableNote')}</p>

          {usable.map((credit) => (
            <div key={credit.id} className="actions">
              <span className="strong">
                {format.money(credit.remaining, credit.currency)}
              </span>{' '}
              <span className="muted">
                {t('money.creditFrom', { date: format.date(credit.raisedAt) })}
              </span>
              <button
                type="button"
                disabled={busy}
                onClick={() =>
                  void useHere(credit.id, Math.min(credit.remaining, owed.outstanding))}
              >
                {t('money.useItHere', {
                  amount: format.money(
                    Math.min(credit.remaining, owed.outstanding), credit.currency),
                })}
              </button>
            </div>
          ))}
        </div>
      )}

      {raised.length === 0 ? null : (
        <div className="panel-inset" aria-label={t('money.creditTitle')}>
          <h4>{t('money.creditTitle')}</h4>
          <p className="note">{t('money.creditNote')}</p>

          {raised.map((credit) => (
            <div key={credit.id}>
              <p>
                <span className="strong">
                  {format.money(credit.remaining, credit.currency)}
                </span>{' '}
                <span className="muted">
                  {t('money.creditFrom', { date: format.date(credit.raisedAt) })}
                </span>
              </p>

              <label htmlFor={`refund-method-${credit.id}`}>{t('money.refundHow')}</label>
              <select
                id={`refund-method-${credit.id}`}
                value={refundMethod}
                onChange={(event) => setRefundMethod(event.target.value as PaymentMethod)}
              >
                {refundMethods.map((m) => (
                  <option key={m} value={m}>
                    {label('paymentMethod', m)}
                  </option>
                ))}
              </select>

              <div className="actions">
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => void giveBack(credit.id, credit.remaining)}
                >
                  {t('money.giveItBack')}
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {owed.payments.length === 0 ? null : (
        <ol className="history" aria-label={t('money.paymentsTitle')}>
          {[...owed.payments].reverse().map((payment) => (
            <li key={payment.id}>
              <span className="strong">{format.money(payment.amount, payment.currency)}</span>{' '}
              <span className="muted">
                {label('paymentMethod', payment.method)} &middot;{' '}
                {format.dateTime(payment.receivedAt)}
                {payment.note === null ? '' : ` — ${payment.note}`}
              </span>
            </li>
          ))}
        </ol>
      )}

      {owed.isSettled ? null : (
        <form
          onSubmit={(event) => {
            event.preventDefault();
            if (ready && !busy) {
              void take();
            }
          }}
        >
          <label htmlFor="pay-amount">{t('money.howMuch')}</label>
          <input
            id="pay-amount"
            inputMode="decimal"
            value={amount}
            onChange={(event) => setAmount(event.target.value)}
          />

          <label htmlFor="pay-method">{t('money.howPaid')}</label>
          <select
            id="pay-method"
            value={method}
            onChange={(event) => setMethod(event.target.value as PaymentMethod)}
          >
            {paymentMethods.map((m) => (
              <option key={m} value={m}>
                {label('paymentMethod', m)}
              </option>
            ))}
          </select>

          <label htmlFor="pay-note">{t('money.reference')}</label>
          <input
            id="pay-note"
            value={note}
            onChange={(event) => setNote(event.target.value)}
            autoComplete="off"
          />

          {willOverpay === 0 ? null : (
            <p className="note" role="status">
              {t('money.willOverpay', {
                extra: format.money(willOverpay, owed.currency),
              })}
            </p>
          )}

          {error === null ? null : (
            <p className="error" role="alert">
              {error}
            </p>
          )}

          <div className="actions">
            <button type="submit" disabled={!ready || busy}>
              {t('money.takeIt')}
            </button>
          </div>
        </form>
      )}
    </section>
  );
}
