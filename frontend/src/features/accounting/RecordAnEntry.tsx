// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   RecordAnEntry — writing a journal entry by hand: an expense, the capital the
//   owners put in, a correction.
//
//   The only screen in the application that chooses accounts directly, and it
//   exists because a dealership has overheads. Wages, rent, advertising and
//   floorplan interest are not the consequence of anything this system models,
//   so nothing else can post them.
//
// Usage:
//   Rendered on the ledger screen. Needs Accounting.ManualEntry, which a
//   salesperson does not hold even though they hold Accounting.Post.
//
// Coding Instructions:
//   IT SHOWS THE TWO TOTALS AND WHETHER THEY AGREE, BEFORE ANYTHING IS SENT.
//   Not to replace the server's check — JournalEntry.Post refuses an unbalanced
//   entry whatever this screen thinks — but because the person typing needs to
//   see the difference while they can still fix it. A round trip that comes back
//   "out by 90" is a worse way to learn the same thing.
//
//   Two lines minimum, and the memo is required. An entry with no explanation is
//   the one somebody will be asked about in a year and nobody will be able to
//   answer.

import { useEffect, useState } from 'react';
import { api, post } from '../../shared/api';
import type {
  AccountView,
  JournalEntryDetail,
  OrganizationSummary,
  RooftopSummary,
} from '../../shared/contracts';
import { useI18n } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';

interface Row {
  accountCode: string;
  debit: string;
  credit: string;
  memo: string;
}

const emptyRow: Row = { accountCode: '', debit: '', credit: '', memo: '' };

function today(): string {
  return new Date().toISOString().slice(0, 10);
}

export function RecordAnEntry({ onPosted }: { onPosted: () => void }) {
  const { t, format } = useI18n();
  const describe = useApiMessage();

  const [accounts, setAccounts] = useState<AccountView[]>([]);
  const [rooftops, setRooftops] = useState<RooftopSummary[]>([]);
  const [rooftopId, setRooftopId] = useState('');

  const [entryDate, setEntryDate] = useState(today);
  const [memo, setMemo] = useState('');
  const [rows, setRows] = useState<Row[]>([{ ...emptyRow }, { ...emptyRow }]);

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void (async () => {
      try {
        const [chart, organization] = await Promise.all([
          api<AccountView[]>('/accounting/accounts'),
          api<OrganizationSummary>('/organization'),
        ]);

        setAccounts(chart);

        const mine = organization.legalEntities.flatMap((entity) => entity.rooftops);
        setRooftops(mine);
        if (mine.length === 1) {
          setRooftopId(mine[0]!.id);
        }
      } catch (failure) {
        setError(describe(failure));
      }
    })();
  }, [describe]);

  const number = (value: string) => {
    const parsed = Number(value);
    return value.trim() === '' || Number.isNaN(parsed) ? 0 : parsed;
  };

  const debits = rows.reduce((sum, row) => sum + number(row.debit), 0);
  const credits = rows.reduce((sum, row) => sum + number(row.credit), 0);
  const difference = debits - credits;

  const ready =
    rooftopId !== ''
    && memo.trim() !== ''
    && debits > 0
    && difference === 0
    && rows.filter((row) => row.accountCode !== '').length >= 2;

  function update(index: number, change: Partial<Row>) {
    setRows((current) => current.map((row, i) => (i === index ? { ...row, ...change } : row)));
  }

  async function record() {
    setError(null);
    setBusy(true);

    try {
      await post<JournalEntryDetail>('/accounting/journal', {
        rooftopId,
        entryDate,
        memo: memo.trim(),
        currency: 'USD',
        lines: rows
          .filter((row) => row.accountCode !== '')
          .map((row) => ({
            accountCode: row.accountCode,
            debit: number(row.debit),
            credit: number(row.credit),
            memo: row.memo.trim() === '' ? null : row.memo.trim(),
          })),
      });

      setMemo('');
      setRows([{ ...emptyRow }, { ...emptyRow }]);
      onPosted();
    } catch (failure) {
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="panel" aria-label={t('entry.title')}>
      <h2>{t('entry.title')}</h2>
      <p className="note">{t('entry.note')}</p>

      <form
        onSubmit={(event) => {
          event.preventDefault();
          if (ready && !busy) {
            void record();
          }
        }}
      >
        {rooftops.length <= 1 ? null : (
          <>
            <label htmlFor="entry-rooftop">{t('entry.whichLocation')}</label>
            <select
              id="entry-rooftop"
              value={rooftopId}
              onChange={(event) => setRooftopId(event.target.value)}
            >
              <option value="">{t('entry.chooseLocation')}</option>
              {rooftops.map((rooftop) => (
                <option key={rooftop.id} value={rooftop.id}>{rooftop.name}</option>
              ))}
            </select>
          </>
        )}

        <label htmlFor="entry-date">{t('entry.when')}</label>
        {/* The person's to choose: an expense is dated when it was incurred, not
            when somebody got round to typing it. The server checks THAT date
            against the open month. */}
        <input
          id="entry-date"
          type="date"
          value={entryDate}
          onChange={(event) => setEntryDate(event.target.value)}
        />

        <label htmlFor="entry-memo">{t('entry.what')}</label>
        <input
          id="entry-memo"
          value={memo}
          onChange={(event) => setMemo(event.target.value)}
          autoComplete="off"
        />

        <div className="scroller">
          <table>
            <thead>
              <tr>
                <th scope="col">{t('entry.account')}</th>
                <th scope="col" className="num">{t('entry.debit')}</th>
                <th scope="col" className="num">{t('entry.credit')}</th>
                <th scope="col">{t('entry.lineNote')}</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row, index) => (
                <tr key={index}>
                  <td>
                    <select
                      aria-label={t('entry.accountOnLine', { line: index + 1 })}
                      value={row.accountCode}
                      onChange={(event) => update(index, { accountCode: event.target.value })}
                    >
                      <option value="">{t('entry.chooseAccount')}</option>
                      {accounts.map((account) => (
                        <option key={account.code} value={account.code}>
                          {account.code} — {account.name}
                        </option>
                      ))}
                    </select>
                  </td>
                  <td className="num">
                    <input
                      inputMode="decimal"
                      aria-label={t('entry.debitOnLine', { line: index + 1 })}
                      value={row.debit}
                      onChange={(event) => update(index, { debit: event.target.value })}
                    />
                  </td>
                  <td className="num">
                    <input
                      inputMode="decimal"
                      aria-label={t('entry.creditOnLine', { line: index + 1 })}
                      value={row.credit}
                      onChange={(event) => update(index, { credit: event.target.value })}
                    />
                  </td>
                  <td>
                    <input
                      aria-label={t('entry.noteOnLine', { line: index + 1 })}
                      value={row.memo}
                      onChange={(event) => update(index, { memo: event.target.value })}
                      autoComplete="off"
                    />
                  </td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr>
                <td className="strong">{t('entry.totals')}</td>
                <td className="num strong">{format.money(debits, 'USD')}</td>
                <td className="num strong">{format.money(credits, 'USD')}</td>
                <td />
              </tr>
            </tfoot>
          </table>
        </div>

        <button type="button" onClick={() => setRows((current) => [...current, { ...emptyRow }])}>
          {t('entry.addLine')}
        </button>

        {/* Shown while it can still be fixed, rather than after a round trip. */}
        {difference === 0 ? null : (
          <p className="error">{t('entry.outBy', { amount: format.money(Math.abs(difference), 'USD') })}</p>
        )}

        {error === null ? null : <p className="error" role="alert">{error}</p>}

        <div className="actions">
          <button type="submit" disabled={!ready || busy}>{t('entry.record')}</button>
        </div>
      </form>
    </section>
  );
}
