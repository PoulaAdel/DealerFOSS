// TrialBalancePage — what every account adds up to, and whether the books agree.
//
// Use:  /accounting.
// Edit: the "balances" flag is the headline, not a footnote. A trial balance that
//       does not balance means something was lost on the way in, and that is the
//       most important thing this screen can tell somebody.

import { useCallback, useEffect, useState } from 'react';
import { ApiError, api } from '../../shared/api';
import type { TrialBalance } from '../../shared/contracts';

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; balance: TrialBalance }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

export function TrialBalancePage() {
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

      setLoad({
        kind: 'failed',
        message: failure instanceof ApiError ? failure.message : 'Could not load the balances.',
      });
    }
  }, []);

  useEffect(() => {
    void fetchBalance();
  }, [fetchBalance]);

  return (
    <>
      <header className="page__head">
        <h1>Trial balance</h1>
      </header>

      <Body load={load} onRetry={fetchBalance} />
    </>
  );
}

function Body({ load, onRetry }: { load: Load; onRetry: () => void }) {
  switch (load.kind) {
    case 'loading':
      return (
        <p className="state" aria-live="polite">
          Adding it up…
        </p>
      );

    case 'denied':
      return (
        <p className="state" role="alert">
          You do not have access to these figures.
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
      return load.balance.accounts.length === 0 ? (
        <p className="state">
          Nothing posted yet. Entries appear here once a car has been delivered.
        </p>
      ) : (
        <Balances balance={load.balance} />
      );
  }
}

function Balances({ balance }: { balance: TrialBalance }) {
  const money = new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: balance.currency || 'USD',
  });

  return (
    <>
      <p className={balance.balances ? 'verdict verdict--ok' : 'verdict verdict--bad'} role="status">
        {balance.balances
          ? `In balance — debits and credits both come to ${money.format(balance.totalDebits)}.`
          : `Out of balance by ${money.format(Math.abs(balance.totalDebits - balance.totalCredits))}. Something was lost on the way in.`}
      </p>

      <div className="scroll">
        <table>
          <thead>
            <tr>
              <th scope="col">Code</th>
              <th scope="col">Account</th>
              <th scope="col">Kind</th>
              <th scope="col" className="num">
                Debits
              </th>
              <th scope="col" className="num">
                Credits
              </th>
              <th scope="col" className="num">
                Balance
              </th>
            </tr>
          </thead>
          <tbody>
            {balance.accounts.map((account) => (
              <tr key={account.code}>
                <td className="mono">{account.code}</td>
                <td>{account.name}</td>
                <td className="muted">{account.kind}</td>
                <td className="num mono">{money.format(account.debits)}</td>
                <td className="num mono">{money.format(account.credits)}</td>
                <td className="num mono strong">{money.format(account.balance)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <td colSpan={3}>Total</td>
              <td className="num mono strong">{money.format(balance.totalDebits)}</td>
              <td className="num mono strong">{money.format(balance.totalCredits)}</td>
              <td />
            </tr>
          </tfoot>
        </table>
      </div>
    </>
  );
}
