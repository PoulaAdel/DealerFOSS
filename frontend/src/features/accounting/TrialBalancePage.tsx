// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   TrialBalancePage — what every account adds up to, and whether the books agree.
//
// Usage:
//   /accounting.
//
// Coding Instructions:
//   The "balances" flag is the headline, not a footnote. A trial balance that
//   does not balance means something was lost on the way in, and that is the
//   most important thing this screen can tell somebody.

import { useCallback, useEffect, useState } from 'react';
import { ApiError, api } from '../../shared/api';
import type { TrialBalance } from '../../shared/contracts';
import { useI18n } from '../../shared/i18n';
import { useEnumLabel } from '../../shared/i18n/enums';
import { useApiMessage } from '../../shared/i18n/apiMessage';
import { RecordAnEntry } from './RecordAnEntry';

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; balance: TrialBalance }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

export function TrialBalancePage() {
  const { t } = useI18n();
  const describe = useApiMessage();
  const [load, setLoad] = useState<Load>({ kind: 'loading' });

  const fetchBalance = useCallback(async () => {
    setLoad({ kind: 'loading' });

    try {
      setLoad({ kind: 'ready', balance: await api<TrialBalance>('/accounting/balances') });
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 403) {
        setLoad({ kind: 'denied' });
        return;
      }

      setLoad({ kind: 'failed', message: describe(failure) });
    }
  }, [describe]);

  useEffect(() => {
    void fetchBalance();
  }, [fetchBalance]);

  return (
    <>
      <header className="page__head">
        <h1>{t('trialBalance.title')}</h1>
      </header>

      <Body load={load} onRetry={fetchBalance} />

      {/* Below the balance, because reading the books is the common case and
          writing an entry by hand is the rare one. It refuses itself for anyone
          without Accounting.ManualEntry, which is most people. */}
      <RecordAnEntry onPosted={() => void fetchBalance()} />
    </>
  );
}

function Body({ load, onRetry }: { load: Load; onRetry: () => void }) {
  const { t } = useI18n();

  switch (load.kind) {
    case 'loading':
      return (
        <p className="state" aria-live="polite">
          {t('trialBalance.loading')}
        </p>
      );

    case 'denied':
      return (
        <p className="state" role="alert">
          {t('trialBalance.denied')}
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
      return load.balance.accounts.length === 0 ? (
        <p className="state">{t('trialBalance.empty')}</p>
      ) : (
        <Balances balance={load.balance} />
      );
  }
}

function Balances({ balance }: { balance: TrialBalance }) {
  const { t, format } = useI18n();
  const label = useEnumLabel();

  // Grouping and the position of the currency symbol both follow the reader's
  // language now: "$1,250.00" in English, "1 250,00 $US" in French, and the
  // whole figure mirrored on an Arabic page.
  const money = (amount: number) => format.money(amount, balance.currency || 'USD');

  return (
    <>
      <p className={balance.balances ? 'verdict verdict--ok' : 'verdict verdict--bad'} role="status">
        {balance.balances
          ? t('trialBalance.inBalance', { total: money(balance.totalDebits) })
          : t('trialBalance.outOfBalance', {
              difference: money(Math.abs(balance.totalDebits - balance.totalCredits)),
            })}
      </p>

      <div className="scroll">
        <table>
          <thead>
            <tr>
              <th scope="col">{t('trialBalance.colCode')}</th>
              <th scope="col">{t('trialBalance.colAccount')}</th>
              <th scope="col">{t('trialBalance.colKind')}</th>
              <th scope="col" className="num">
                {t('trialBalance.colDebits')}
              </th>
              <th scope="col" className="num">
                {t('trialBalance.colCredits')}
              </th>
              <th scope="col" className="num">
                {t('trialBalance.colBalance')}
              </th>
            </tr>
          </thead>
          <tbody>
            {balance.accounts.map((account) => (
              <tr key={account.code}>
                {/* The account code is a number in a chart of accounts, read
                    left to right whatever the page direction. The account NAME
                    is dealership data and is shown as they typed it. */}
                <td className="mono" dir="ltr">
                  {account.code}
                </td>
                <td>{account.name}</td>
                <td className="muted">{label('accountKind', account.kind)}</td>
                <td className="num mono">{money(account.debits)}</td>
                <td className="num mono">{money(account.credits)}</td>
                <td className="num mono strong">{money(account.balance)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <td colSpan={3}>{t('trialBalance.total')}</td>
              <td className="num mono strong">{money(balance.totalDebits)}</td>
              <td className="num mono strong">{money(balance.totalCredits)}</td>
              <td />
            </tr>
          </tfoot>
        </table>
      </div>
    </>
  );
}
