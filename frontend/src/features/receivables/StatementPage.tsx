// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   StatementPage — one customer's bills and payments over a period, with the
//   balance running through them. What a dealership hands somebody who asks
//   "what do I owe you".
//
// Usage:
//   Reachable at /receivables/statements.
//
// Coding Instructions:
//   Read-only, like the ageing report next to it. The balance is the
//   server's arithmetic end to end — this screen does not add a running
//   total locally, the same rule TakePayment follows for outstanding.

import { useCallback, useEffect, useState } from 'react';
import { ApiError, api } from '../../shared/api';
import type { CustomerStatement, CustomerSummary, Page } from '../../shared/contracts';
import { useI18n } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';
import { RecordPicker, type PickerOption } from '../../shared/RecordPicker';

type Load =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'ready'; statement: CustomerStatement }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

/** The last 30 days, ending today — a reasonable first answer before anybody picks a range. */
function defaultRange(): { from: string; to: string } {
  const to = new Date();
  const from = new Date(to);
  from.setUTCDate(from.getUTCDate() - 30);

  const iso = (date: Date) => date.toISOString().slice(0, 10);
  return { from: iso(from), to: iso(to) };
}

export function StatementPage() {
  const { t, format } = useI18n();
  const describe = useApiMessage();

  const [customer, setCustomer] = useState<PickerOption | null>(null);
  const [{ from, to }, setRange] = useState(defaultRange);
  const [load, setLoad] = useState<Load>({ kind: 'idle' });

  const findCustomers = useCallback(
    async (term: string, signal: AbortSignal) =>
      (await api<Page<CustomerSummary>>(
        `/customers?search=${encodeURIComponent(term)}&limit=15`,
        { signal },
      )).rows.map((c): PickerOption => ({
        id: c.id,
        label: c.displayName,
        hint: c.primaryEmail ?? c.primaryPhone ?? undefined,
      })),
    [],
  );

  const fetchStatement = useCallback(async () => {
    if (customer === null) {
      setLoad({ kind: 'idle' });
      return;
    }

    setLoad({ kind: 'loading' });

    try {
      setLoad({
        kind: 'ready',
        statement: await api<CustomerStatement>(
          `/receivables/statement/${customer.id}?from=${from}T00:00:00Z&to=${to}T23:59:59Z`,
        ),
      });
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 403) {
        setLoad({ kind: 'denied' });
        return;
      }

      setLoad({ kind: 'failed', message: describe(failure) });
    }
  }, [customer, from, to, describe]);

  useEffect(() => {
    void fetchStatement();
  }, [fetchStatement]);

  return (
    <section className="page">
      <header className="page__head">
        <h1>{t('statement.title')}</h1>
      </header>

      <RecordPicker
        id="statement-customer"
        label={t('statement.customer')}
        chosen={customer}
        onChoose={setCustomer}
        search={findCustomers}
      />

      <form className="filter" onSubmit={(e) => e.preventDefault()}>
        <label htmlFor="statement-from">{t('statement.from')}</label>
        <input
          id="statement-from"
          type="date"
          value={from}
          max={to}
          onChange={(e) => setRange((current) => ({ ...current, from: e.target.value }))}
        />

        <label htmlFor="statement-to">{t('statement.to')}</label>
        <input
          id="statement-to"
          type="date"
          value={to}
          min={from}
          onChange={(e) => setRange((current) => ({ ...current, to: e.target.value }))}
        />
      </form>

      {customer === null ? <p className="note">{t('statement.pickCustomer')}</p> : null}

      {load.kind === 'loading' ? (
        <p className="state" aria-live="polite">{t('statement.loading')}</p>
      ) : null}

      {load.kind === 'denied' ? <p className="note">{t('statement.denied')}</p> : null}

      {load.kind === 'failed' ? (
        <>
          <p className="error">{load.message}</p>
          <button type="button" onClick={() => void fetchStatement()}>{t('common.retry')}</button>
        </>
      ) : null}

      {load.kind === 'ready' ? <StatementView statement={load.statement} money={format.money} date={format.date} /> : null}
    </section>
  );
}

type Money = (amount: number, currency: string) => string;
type DateFmt = (value: string) => string;

function StatementView({
  statement, money, date,
}: {
  statement: CustomerStatement;
  money: Money;
  date: DateFmt;
}) {
  const { t } = useI18n();

  return (
    <section className="panel" aria-label={statement.customerName}>
      <h2>{statement.customerName}</h2>

      <dl className="facts">
        <dt>{t('statement.opening')}</dt>
        <dd className="num">{money(statement.openingBalance, statement.currency)}</dd>

        <dt>{t('statement.closing')}</dt>
        <dd className="num strong">{money(statement.closingBalance, statement.currency)}</dd>
      </dl>

      {statement.lines.length === 0 ? (
        <p className="state">{t('statement.empty')}</p>
      ) : (
        <div className="scroller">
          <table>
            <thead>
              <tr>
                <th scope="col">{t('statement.colDate')}</th>
                <th scope="col">{t('statement.colReference')}</th>
                <th scope="col" className="num">{t('statement.colAmount')}</th>
                <th scope="col" className="num">{t('statement.colBalance')}</th>
              </tr>
            </thead>
            <tbody>
              {statement.lines.map((line, index) => (
                // eslint-disable-next-line react/no-array-index-key -- two lines can share every visible field; the pair is stable, the index is not reordered.
                <tr key={`${line.date}-${index}`}>
                  <td>{date(line.date)}</td>
                  <td>
                    <span className="mono" dir="ltr">{line.reference}</span>{' '}
                    <span className={line.kind === 'Invoice' ? 'chip chip--invoice' : 'chip chip--payment'}>
                      {line.kind === 'Invoice' ? t('statement.kindInvoice') : t('statement.kindPayment')}
                    </span>
                  </td>
                  <td className="num">
                    {line.kind === 'Invoice'
                      ? money(line.amount, statement.currency)
                      : `−${money(line.amount, statement.currency)}`}
                  </td>
                  <td className="num strong">{money(line.balance, statement.currency)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
