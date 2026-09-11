// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ReportsPage — the two reports a dealer principal actually reads: what the
//   month made, and what the business is worth.
//
//   A trial balance is a bookkeeping instrument. It answers "did anything get
//   lost", which is a question for whoever keeps the books, not for whoever runs
//   the business. Until 2026-09-11 it was the only report this system had, and
//   "what did the month made" could only be answered as gross because there was
//   nowhere to record an overhead.
//
// Usage:
//   Reachable at /accounting/reports.
//
// Coding Instructions:
//   THE BALANCE SHEET SAYS WHEN IT DOES NOT BALANCE, LOUDLY. The server works
//   that out and sends `balances`; if it is false something has been posted the
//   report cannot classify, and a plausible-looking page with a hole in it is
//   worse than an alarming one. Do not soften it into a footnote.
//
//   Both reports are read-only. Recording an entry lives on the ledger screen,
//   behind its own permission — a report somebody can type into is a report
//   nobody can trust.

import { useCallback, useEffect, useState } from 'react';
import { ApiError, api } from '../../shared/api';
import type { BalanceSheet, ProfitAndLoss } from '../../shared/contracts';
import { useI18n } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; profit: ProfitAndLoss; sheet: BalanceSheet }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

/** The first and last day of a month, as the API wants them. */
function monthEdges(month: string): { from: string; to: string } {
  const [year, index] = month.split('-').map(Number);
  const last = new Date(Date.UTC(year!, index!, 0)).getUTCDate();

  return { from: `${month}-01`, to: `${month}-${String(last).padStart(2, '0')}` };
}

function thisMonth(): string {
  const now = new Date();
  return `${now.getUTCFullYear()}-${String(now.getUTCMonth() + 1).padStart(2, '0')}`;
}

export function ReportsPage() {
  const { t, format } = useI18n();
  const describe = useApiMessage();

  const [month, setMonth] = useState(thisMonth);
  const [load, setLoad] = useState<Load>({ kind: 'loading' });

  const fetchReports = useCallback(async () => {
    setLoad({ kind: 'loading' });

    const { from, to } = monthEdges(month);

    try {
      // The balance sheet takes only the closing date. It is a position, not a
      // period, and asking it for a month would be asking a wrong question.
      const [profit, sheet] = await Promise.all([
        api<ProfitAndLoss>(`/accounting/profit-and-loss?from=${from}&to=${to}`),
        api<BalanceSheet>(`/accounting/balance-sheet?asAt=${to}`),
      ]);

      setLoad({ kind: 'ready', profit, sheet });
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 403) {
        setLoad({ kind: 'denied' });
        return;
      }

      setLoad({ kind: 'failed', message: describe(failure) });
    }
  }, [month, describe]);

  useEffect(() => {
    void fetchReports();
  }, [fetchReports]);

  return (
    <section className="page">
      <header className="page__head">
        <h1>{t('reports.title')}</h1>

        <div className="filter">
          <label htmlFor="reports-month">{t('reports.month')}</label>
          <input
            id="reports-month"
            type="month"
            value={month}
            onChange={(event) => setMonth(event.target.value)}
          />
        </div>
      </header>

      {load.kind === 'loading' ? <p className="state" aria-live="polite">{t('reports.loading')}</p> : null}

      {load.kind === 'denied' ? <p className="note">{t('reports.denied')}</p> : null}

      {load.kind === 'failed' ? (
        <>
          <p className="error">{load.message}</p>
          <button type="button" onClick={() => void fetchReports()}>{t('common.retry')}</button>
        </>
      ) : null}

      {load.kind === 'ready' ? (
        <>
          <ProfitAndLossPanel report={load.profit} money={format.money} />
          <BalanceSheetPanel sheet={load.sheet} money={format.money} />
        </>
      ) : null}
    </section>
  );
}

type Money = (amount: number, currency: string) => string;

function ProfitAndLossPanel({ report, money }: { report: ProfitAndLoss; money: Money }) {
  const { t } = useI18n();

  return (
    <section className="panel" aria-label={t('reports.profitTitle')}>
      <h2>{t('reports.profitTitle')}</h2>

      <div className="scroller">
        <table>
          <thead>
            <tr>
              <th scope="col">{t('reports.department')}</th>
              <th scope="col" className="num">{t('reports.revenue')}</th>
              <th scope="col" className="num">{t('reports.cost')}</th>
              <th scope="col" className="num">{t('reports.gross')}</th>
            </tr>
          </thead>
          <tbody>
            {report.departments.map((department) => (
              <tr key={department.name}>
                <td>{department.name}</td>
                <td className="num">{money(department.revenue, report.currency)}</td>
                <td className="num">{money(department.cost, report.currency)}</td>
                <td className="num">{money(department.gross, report.currency)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <td className="strong">{t('reports.grossProfit')}</td>
              <td className="num">{money(report.totalRevenue, report.currency)}</td>
              <td className="num">{money(report.totalCost, report.currency)}</td>
              <td className="num strong">{money(report.grossProfit, report.currency)}</td>
            </tr>
          </tfoot>
        </table>
      </div>

      <h3>{t('reports.overheads')}</h3>
      <div className="scroller">
        <table>
          <tbody>
            {/* Every overhead account, including the ones at nothing. A missing
                figure and no spending must not look the same. */}
            {report.expenses.map((expense) => (
              <tr key={expense.code}>
                <td><span className="mono" dir="ltr">{expense.code}</span> {expense.name}</td>
                <td className="num">{money(expense.amount, report.currency)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <td className="strong">{t('reports.totalOverheads')}</td>
              <td className="num strong">{money(report.totalExpenses, report.currency)}</td>
            </tr>
          </tfoot>
        </table>
      </div>

      <p className={report.netProfit < 0 ? 'error strong' : 'strong'}>
        {t('reports.netProfit')}: {money(report.netProfit, report.currency)}
      </p>
    </section>
  );
}

function BalanceSheetPanel({ sheet, money }: { sheet: BalanceSheet; money: Money }) {
  const { t } = useI18n();

  const section = (heading: string, rows: BalanceSheet['assets'], total: number) => (
    <>
      <h3>{heading}</h3>
      <div className="scroller">
        <table>
          <tbody>
            {rows.length === 0 ? (
              <tr><td className="muted">{t('reports.nothingHere')}</td><td /></tr>
            ) : rows.map((row) => (
              <tr key={row.code}>
                <td><span className="mono" dir="ltr">{row.code}</span> {row.name}</td>
                <td className="num">{money(row.balance, sheet.currency)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <td className="strong">{t('reports.total')}</td>
              <td className="num strong">{money(total, sheet.currency)}</td>
            </tr>
          </tfoot>
        </table>
      </div>
    </>
  );

  return (
    <section className="panel" aria-label={t('reports.sheetTitle')}>
      <h2>{t('reports.sheetTitle')}</h2>

      {/* Said first and said plainly. If the two sides disagree, something has
          been posted this report cannot classify, and every figure below is
          suspect until somebody finds out what. */}
      {sheet.balances ? (
        <p className="note">{t('reports.sheetBalances')}</p>
      ) : (
        <p className="error strong" role="alert">{t('reports.sheetDoesNotBalance')}</p>
      )}

      {section(t('reports.assets'), sheet.assets, sheet.totalAssets)}
      {section(t('reports.liabilities'), sheet.liabilities, sheet.totalLiabilities)}
      {section(t('reports.equity'), sheet.equity, sheet.totalEquity)}

      <p>
        {t('reports.earningsToDate')}: <span className="strong">
          {money(sheet.earningsToDate, sheet.currency)}
        </span>
      </p>
      <p className="note">{t('reports.earningsNote')}</p>
    </section>
  );
}
