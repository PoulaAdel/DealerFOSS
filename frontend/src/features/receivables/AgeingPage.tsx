// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   AgeingPage — who owes the dealership money, split by how overdue it is.
//
//   The question this answers is not "who owes us" — the receivables list
//   already answers that — but "who has owed us for so long it needs a
//   telephone call". Four columns instead of one `daysOutstanding` figure,
//   because a dealership chases those four differently: current is not a
//   concern yet, 31-60 gets a call, 61-90 gets a harder one, and over 90 is
//   what a manager asks about by name.
//
// Usage:
//   Reachable at /receivables/ageing.
//
// Coding Instructions:
//   Read-only, like the accounting reports it is styled after. Taking a
//   payment lives on the bill itself, behind TakePayment — this screen is
//   for deciding who to call, not for recording that the call worked.

import { useCallback, useEffect, useState } from 'react';
import { ApiError, api } from '../../shared/api';
import type { AgeingReport } from '../../shared/contracts';
import { useI18n } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; report: AgeingReport }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

export function AgeingPage() {
  const { t, format } = useI18n();
  const describe = useApiMessage();

  const [load, setLoad] = useState<Load>({ kind: 'loading' });

  const fetchReport = useCallback(async () => {
    setLoad({ kind: 'loading' });

    try {
      setLoad({ kind: 'ready', report: await api<AgeingReport>('/receivables/ageing') });
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 403) {
        setLoad({ kind: 'denied' });
        return;
      }

      setLoad({ kind: 'failed', message: describe(failure) });
    }
  }, [describe]);

  useEffect(() => {
    void fetchReport();
  }, [fetchReport]);

  return (
    <section className="page">
      <header className="page__head">
        <h1>{t('ageing.title')}</h1>
      </header>

      {load.kind === 'loading' ? <p className="state" aria-live="polite">{t('ageing.loading')}</p> : null}

      {load.kind === 'denied' ? <p className="note">{t('ageing.denied')}</p> : null}

      {load.kind === 'failed' ? (
        <>
          <p className="error">{load.message}</p>
          <button type="button" onClick={() => void fetchReport()}>{t('common.retry')}</button>
        </>
      ) : null}

      {load.kind === 'ready' ? <AgeingTable report={load.report} money={format.money} /> : null}
    </section>
  );
}

type Money = (amount: number, currency: string) => string;

function AgeingTable({ report, money }: { report: AgeingReport; money: Money }) {
  const { t } = useI18n();

  if (report.customers.length === 0) {
    return <p className="state">{t('ageing.empty')}</p>;
  }

  return (
    <div className="scroller">
      <table>
        <thead>
          <tr>
            <th scope="col">{t('ageing.colCustomer')}</th>
            <th scope="col" className="num">{t('ageing.colCurrent')}</th>
            <th scope="col" className="num">{t('ageing.col31to60')}</th>
            <th scope="col" className="num">{t('ageing.col61to90')}</th>
            <th scope="col" className="num">{t('ageing.colOver90')}</th>
            <th scope="col" className="num">{t('ageing.colTotal')}</th>
          </tr>
        </thead>
        <tbody>
          {report.customers.map((customer) => (
            <tr key={customer.customerId}>
              <td>{customer.customerName}</td>
              <td className="num">{money(customer.bucket.current, report.currency)}</td>
              <td className="num">{money(customer.bucket.days31To60, report.currency)}</td>
              <td className="num">{money(customer.bucket.days61To90, report.currency)}</td>
              <td className={customer.bucket.over90 > 0 ? 'num error strong' : 'num'}>
                {money(customer.bucket.over90, report.currency)}
              </td>
              <td className="num strong">{money(customer.bucket.total, report.currency)}</td>
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr>
            <td className="strong">{t('ageing.totals')}</td>
            <td className="num">{money(report.totals.current, report.currency)}</td>
            <td className="num">{money(report.totals.days31To60, report.currency)}</td>
            <td className="num">{money(report.totals.days61To90, report.currency)}</td>
            <td className="num">{money(report.totals.over90, report.currency)}</td>
            <td className="num strong">{money(report.totals.total, report.currency)}</td>
          </tr>
        </tfoot>
      </table>
    </div>
  );
}
