// PeriodsPage — the months of the books, and closing them.
//
// Use:  reachable at /accounting/periods.
// Edit: three things here are deliberate.
//
//       (1) There is no countdown, no "closes in N days", and no automatic
//       anything. Locking is an act somebody performs, because the close runs
//       over however many business days the work takes. A screen that implied a
//       deadline would be describing a system that does not exist.
//
//       (2) Closing asks for confirmation and reopening demands a reason. They
//       are not symmetrical acts: closing is the routine end of month-end work,
//       reopening lets a figure somebody already reported move.
//
//       (3) The entry count sits next to each month. Somebody about to close is
//       checking it is the month they think it is, and a count is the cheapest
//       version of that check.

import { useCallback, useEffect, useState } from 'react';
import { ApiError, api, post } from '../../shared/api';
import type { AccountingPeriodView } from '../../shared/contracts';

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; periods: AccountingPeriodView[] }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

const monthName = (year: number, month: number) =>
  new Date(Date.UTC(year, month - 1, 1)).toLocaleDateString(undefined, {
    month: 'long',
    year: 'numeric',
    timeZone: 'UTC',
  });

export function PeriodsPage() {
  const [load, setLoad] = useState<Load>({ kind: 'loading' });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reopening, setReopening] = useState<AccountingPeriodView | null>(null);
  const [reason, setReason] = useState('');
  const [opening, setOpening] = useState(false);

  const find = useCallback(async () => {
    setLoad({ kind: 'loading' });

    try {
      setLoad({ kind: 'ready', periods: await api<AccountingPeriodView[]>('/accounting/periods') });
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 403) {
        setLoad({ kind: 'denied' });
        return;
      }

      setLoad({
        kind: 'failed',
        message: failure instanceof ApiError ? failure.message : 'The books could not be read.',
      });
    }
  }, []);

  useEffect(() => {
    void find();
  }, [find]);

  async function act(work: () => Promise<unknown>) {
    setBusy(true);
    setError(null);

    try {
      await work();
      await find();
    } catch (failure) {
      // Closing needs one permission and reopening another, and the server is
      // the one that knows which this caller holds.
      setError(failure instanceof ApiError ? failure.message : 'That did not work.');
    } finally {
      setBusy(false);
    }
  }

  if (load.kind === 'loading') {
    return <p>Loading the books…</p>;
  }

  if (load.kind === 'denied') {
    return (
      <section className="page">
        <h1>The books</h1>
        <p className="note">You do not have access to the accounts.</p>
      </section>
    );
  }

  if (load.kind === 'failed') {
    return (
      <section className="page">
        <h1>The books</h1>
        <p className="error">{load.message}</p>
        <button type="button" onClick={() => void find()}>
          Try again
        </button>
      </section>
    );
  }

  const next = nextUnopened(load.periods);

  return (
    <section className="page">
      <header className="page__head">
        <h1>The books</h1>
        {opening ? null : (
          <button type="button" className="primary" onClick={() => setOpening(true)}>
            Open a month
          </button>
        )}
      </header>

      <p className="note">
        Nothing can be posted into a month until its books are open, and nothing
        can be posted into one that has been closed. Closing is something you do
        when the month-end work is finished — there is no date that does it for
        you.
      </p>

      {opening ? (
        <OpenMonth
          suggested={next}
          busy={busy}
          onCancel={() => setOpening(false)}
          onOpen={(year, month) =>
            void act(async () => {
              await post('/accounting/periods', { year, month, note: null });
              setOpening(false);
            })
          }
        />
      ) : null}

      {reopening === null ? null : (
        <section className="panel panel--warn">
          <h2>Reopen {monthName(reopening.year, reopening.month)}?</h2>
          <p className="note">
            This month has been closed, and its figures may already have been
            reported. Reopening it is recorded against the month with your reason,
            so anybody looking later can see what happened and why.
          </p>

          <div className="field">
            <label htmlFor="reopen-reason">Why is it being reopened?</label>
            <input
              id="reopen-reason"
              value={reason}
              placeholder="A supplier invoice arrived on the 4th"
              onChange={(event) => setReason(event.target.value)}
            />
          </div>

          <div className="actions">
            <button
              type="button"
              className="primary"
              disabled={busy || reason.trim() === ''}
              onClick={() =>
                void act(async () => {
                  await post(
                    `/accounting/periods/${reopening.year}/${reopening.month}/reopen`,
                    { note: reason.trim() },
                  );
                  setReopening(null);
                  setReason('');
                })
              }
            >
              Reopen it
            </button>
            <button
              type="button"
              onClick={() => {
                setReopening(null);
                setReason('');
              }}
            >
              Leave it closed
            </button>
          </div>
        </section>
      )}

      <p className="error" aria-live="polite">
        {error ?? ''}
      </p>

      {load.periods.length === 0 ? (
        <p className="note">
          No months are open yet. Nothing can be posted until you open one.
        </p>
      ) : (
        <div className="scroll">
          <table className="table">
            <caption className="visually-hidden">
              Every month of the books, newest first.
            </caption>
            <thead>
              <tr>
                <th scope="col">Month</th>
                <th scope="col">Cutoff</th>
                <th scope="col" className="num">
                  Entries
                </th>
                <th scope="col">State</th>
                <th scope="col">&nbsp;</th>
              </tr>
            </thead>
            <tbody>
              {load.periods.map((period) => (
                <tr key={period.id}>
                  <td>{monthName(period.year, period.month)}</td>
                  <td>{new Date(period.endsOn).toLocaleDateString()}</td>
                  <td className="num">{period.entries}</td>
                  <td>
                    {period.state === 'Open' ? (
                      <span className="chip chip--won">Open</span>
                    ) : (
                      <span className="chip chip--lost">Closed</span>
                    )}
                  </td>
                  <td>
                    {period.state === 'Open' ? (
                      <button
                        type="button"
                        disabled={busy}
                        onClick={() => {
                          // A real lock on real figures, so it asks first — the
                          // same reasoning as suspending a dealership.
                          if (
                            window.confirm(
                              `Close ${monthName(period.year, period.month)}? Nothing more can be posted into it until it is reopened.`,
                            )
                          ) {
                            void act(() =>
                              post(`/accounting/periods/${period.year}/${period.month}/close`, {
                                note: null,
                              }),
                            );
                          }
                        }}
                      >
                        Close it
                      </button>
                    ) : (
                      <button type="button" disabled={busy} onClick={() => setReopening(period)}>
                        Reopen
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {load.periods.some((p) => p.history.length > 1) ? (
        <>
          <h2>What has happened to the books</h2>
          <ol className="history">
            {load.periods
              .flatMap((period) =>
                period.history.map((entry) => ({ period, entry })),
              )
              .sort(
                (a, b) =>
                  new Date(b.entry.occurredAt).getTime() - new Date(a.entry.occurredAt).getTime(),
              )
              .slice(0, 20)
              .map(({ period, entry }, index) => (
                <li key={`${period.id}-${entry.occurredAt}-${index}`}>
                  <span className="strong">
                    {monthName(period.year, period.month)}{' '}
                    {entry.toState === 'Closed'
                      ? 'closed'
                      : entry.fromState === null
                        ? 'opened'
                        : 'reopened'}
                  </span>{' '}
                  <span className="muted">
                    {new Date(entry.occurredAt).toLocaleString()}
                    {entry.note === null ? '' : ` — ${entry.note}`}
                  </span>
                </li>
              ))}
          </ol>
        </>
      ) : null}
    </section>
  );
}

/**
 * The month after the newest one on record. A suggestion, not a rule — a
 * dealership may legitimately open a month out of order.
 */
function nextUnopened(periods: AccountingPeriodView[]): { year: number; month: number } {
  if (periods.length === 0) {
    const now = new Date();
    return { year: now.getUTCFullYear(), month: now.getUTCMonth() + 1 };
  }

  const newest = periods[0]!;
  return newest.month === 12
    ? { year: newest.year + 1, month: 1 }
    : { year: newest.year, month: newest.month + 1 };
}

function OpenMonth({
  suggested,
  busy,
  onCancel,
  onOpen,
}: {
  suggested: { year: number; month: number };
  busy: boolean;
  onCancel: () => void;
  onOpen: (year: number, month: number) => void;
}) {
  const [year, setYear] = useState(String(suggested.year));
  const [month, setMonth] = useState(String(suggested.month));

  return (
    <section className="panel">
      <h2>Open a month</h2>
      <p className="note">
        Until a month is open, nothing dated in it can be posted — a sale or a
        service invoice will be refused. Opening it is deliberate so the books
        have a start you chose rather than one inferred from the first thing
        anybody typed.
      </p>

      <div className="row">
        <div className="field">
          <label htmlFor="open-year">Year</label>
          <input
            id="open-year"
            inputMode="numeric"
            value={year}
            onChange={(event) => setYear(event.target.value)}
          />
        </div>

        <div className="field">
          <label htmlFor="open-month">Month</label>
          <select id="open-month" value={month} onChange={(event) => setMonth(event.target.value)}>
            {Array.from({ length: 12 }, (_, i) => i + 1).map((m) => (
              <option key={m} value={m}>
                {monthName(Number(year) || suggested.year, m)}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="actions">
        <button
          type="button"
          className="primary"
          disabled={busy || year.trim() === ''}
          onClick={() => onOpen(Number(year), Number(month))}
        >
          Open it
        </button>
        <button type="button" onClick={onCancel}>
          Cancel
        </button>
      </div>
    </section>
  );
}
