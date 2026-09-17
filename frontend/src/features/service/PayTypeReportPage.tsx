// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   PayTypeReportPage — what the workshop sold over a period, split by who
//   pays: the customer, the manufacturer under warranty, or the dealership
//   itself.
//
// Usage:
//   Reachable at /workshop/pay-type, linked from the workshop's head next to
//   the labour report.
//
// Coding Instructions:
//   GROSS PROFIT IS SHOWN FOR PARTS ONLY, AND THAT IS NOT AN OMISSION TO FIX.
//   A part's cost is frozen at invoicing; labour and sublet work carry no cost
//   basis this system has ever recorded. A blended "gross" across all three
//   would quietly average one real figure against two invented ones, so the
//   labour and sublet columns stop at revenue.
//
//   Counted from invoiced work only, the same rule the labour report follows.

import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router';
import { ApiError, api } from '../../shared/api';
import type { PayTypeReconciliation } from '../../shared/contracts';
import { useI18n, type MessageKey } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; report: PayTypeReconciliation }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

function isoDate(value: Date): string {
  return value.toISOString().slice(0, 10);
}

/** The same default the server applies when asked for no period at all. */
function monthToDate(): { from: string; to: string } {
  const today = new Date();
  return {
    from: isoDate(new Date(Date.UTC(today.getFullYear(), today.getMonth(), 1))),
    to: isoDate(today),
  };
}

export function PayTypeReportPage() {
  const { t, format } = useI18n();
  const describe = useApiMessage();

  const [period, setPeriod] = useState(monthToDate);
  const [load, setLoad] = useState<Load>({ kind: 'loading' });

  const find = useCallback(async () => {
    setLoad({ kind: 'loading' });

    const query = `?from=${encodeURIComponent(period.from)}&to=${encodeURIComponent(period.to)}`;

    try {
      setLoad({
        kind: 'ready',
        report: await api<PayTypeReconciliation>(`/repair-orders/pay-type-reconciliation${query}`),
      });
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 403) {
        setLoad({ kind: 'denied' });
        return;
      }

      setLoad({ kind: 'failed', message: describe(failure) });
    }
  }, [period, describe]);

  useEffect(() => {
    void find();
  }, [find]);

  return (
    <section className="page">
      <header className="page__head">
        <h1>{t('payType.title')}</h1>

        <div className="filter">
          <label htmlFor="paytype-from">{t('payType.from')}</label>
          <input
            id="paytype-from"
            type="date"
            value={period.from}
            onChange={(event) => setPeriod({ ...period, from: event.target.value })}
          />
          <label htmlFor="paytype-to">{t('payType.to')}</label>
          <input
            id="paytype-to"
            type="date"
            value={period.to}
            onChange={(event) => setPeriod({ ...period, to: event.target.value })}
          />
        </div>
      </header>

      <p className="note">
        <Link to="/workshop">{t('labour.backToWorkshop')}</Link>
        {' · '}
        <Link to="/workshop/labour">{t('workshop.labourReport')}</Link>
      </p>

      {load.kind === 'loading' ? <p className="state" aria-live="polite">{t('payType.loading')}</p> : null}

      {load.kind === 'denied' ? <p className="state" role="alert">{t('payType.denied')}</p> : null}

      {load.kind === 'failed' ? (
        <div className="state" role="alert">
          <p>{load.message}</p>
          <button type="button" onClick={() => void find()}>{t('common.retry')}</button>
        </div>
      ) : null}

      {load.kind === 'ready' ? <ByPayer report={load.report} /> : null}

      {load.kind === 'ready' ? (
        <p className="note note--footer">
          {t('labour.period', { from: format.date(load.report.from), to: format.date(load.report.to) })}
        </p>
      ) : null}
    </section>
  );
}

function ByPayer({ report }: { report: PayTypeReconciliation }) {
  const { t, format } = useI18n();
  const money = (amount: number) => format.money(amount, 'USD');

  if (report.byPayer.length === 0) {
    return <p className="note">{t('payType.nothingInvoiced')}</p>;
  }

  return (
    <>
      <table className="table totals">
        <caption className="visually-hidden">{t('payType.headlineCaption')}</caption>
        <tbody>
          <tr className="strong">
            <th scope="row">{t('payType.totalRevenue')}</th>
            <td className="num">{money(report.totalRevenue)}</td>
          </tr>
        </tbody>
      </table>

      <div className="scroll">
        <table className="table">
          <caption className="visually-hidden">{t('payType.tableCaption')}</caption>
          <thead>
            <tr>
              <th scope="col">{t('payType.colPayer')}</th>
              <th scope="col" className="num">{t('payType.colLabour')}</th>
              <th scope="col" className="num">{t('payType.colParts')}</th>
              <th scope="col" className="num">{t('payType.colSublet')}</th>
              <th scope="col" className="num">{t('payType.colRevenue')}</th>
              <th scope="col" className="num">{t('payType.colPartsCost')}</th>
              <th scope="col" className="num">{t('payType.colPartsGross')}</th>
              <th scope="col" className="num">{t('payType.colOrders')}</th>
            </tr>
          </thead>
          <tbody>
            {report.byPayer.map((row) => (
              <tr key={row.payType}>
                <td>{t(`enum.servicePayType.${row.payType}` as MessageKey)}</td>
                <td className="num">{money(row.labourRevenue)}</td>
                <td className="num">{money(row.partsRevenue)}</td>
                <td className="num">{money(row.subletRevenue)}</td>
                <td className="num strong">{money(row.revenue)}</td>
                <td className="num">{money(row.partsCost)}</td>
                <td className="num">{money(row.partsGrossProfit)}</td>
                <td className="num">{format.number(row.orderCount)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <p className="note">{t('payType.partsGrossNote')}</p>
    </>
  );
}
