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
//   payments; never compute it here by subtracting what was just typed. The one
//   guard this component makes is refusing to send more than is outstanding, and
//   that is a courtesy — the server refuses it too, and it is the server that
//   decides.
//
//   A settled bill still shows. "Paid in full on the 3rd, by card" is the answer
//   somebody wants at the counter, and hiding the band once it is settled would
//   make the receipt unfindable.

import { useCallback, useEffect, useState } from 'react';
import { ApiError, api, post } from '../../shared/api';
import {
  paymentMethods,
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
  const [amount, setAmount] = useState('');
  const [method, setMethod] = useState<PaymentMethod>('Card');
  const [note, setNote] = useState('');

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      // 204 means "nothing is owed against this yet", which is an ordinary
      // answer rather than a missing page — a deal still being worked has no
      // receivable. api() hands back `undefined` for a no-content reply, so it
      // is normalised here: one absent value, checked in one place.
      const found = await api<ReceivableDetail | undefined>(
        `/receivables/for/${source}/${reference}`);

      setOwed(found ?? null);
    } catch (failure) {
      // A band that cannot load must not take the screen down with it. The job
      // sheet above it is still the useful thing.
      if (failure instanceof ApiError && failure.status === 403) {
        setOwed(null);
        return;
      }

      setError(describe(failure));
    }
  }, [source, reference, watch, describe]);

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
    && typed > 0
    && typed <= owed.outstanding;

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
