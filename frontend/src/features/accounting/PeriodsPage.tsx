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
import { useI18n } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; periods: AccountingPeriodView[] }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

export function PeriodsPage() {
  const { t, format } = useI18n();
  const describe = useApiMessage();

  // "August 2026" in the reader's language, built in UTC so a period is not
  // shown as the month before to somebody west of Greenwich — the month is a
  // fact about the books, not an instant in their day.
  const monthName = (year: number, month: number) => format.monthAndYear(year, month);

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

      setLoad({ kind: 'failed', message: describe(failure) });
    }
  }, [describe]);

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
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  if (load.kind === 'loading') {
    return <p>{t('periods.loading')}</p>;
  }

  if (load.kind === 'denied') {
    return (
      <section className="page">
        <h1>{t('periods.title')}</h1>
        <p className="note">{t('periods.denied')}</p>
      </section>
    );
  }

  if (load.kind === 'failed') {
    return (
      <section className="page">
        <h1>{t('periods.title')}</h1>
        <p className="error">{load.message}</p>
        <button type="button" onClick={() => void find()}>
          {t('common.retry')}
        </button>
      </section>
    );
  }

  const next = nextUnopened(load.periods);

  return (
    <section className="page">
      <header className="page__head">
        <h1>{t('periods.title')}</h1>
        {opening ? null : (
          <button type="button" className="primary" onClick={() => setOpening(true)}>
            {t('periods.openAMonth')}
          </button>
        )}
      </header>

      <p className="note">{t('periods.lede')}</p>

      {opening ? (
        <OpenMonth
          suggested={next}
          busy={busy}
          monthName={monthName}
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
          <h2>
            {t('periods.reopenTitle', { month: monthName(reopening.year, reopening.month) })}
          </h2>
          <p className="note">{t('periods.reopenLede')}</p>

          <div className="field">
            <label htmlFor="reopen-reason">{t('periods.reopenWhy')}</label>
            <input
              id="reopen-reason"
              value={reason}
              placeholder={t('periods.reopenPlaceholder')}
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
              {t('periods.reopenIt')}
            </button>
            <button
              type="button"
              onClick={() => {
                setReopening(null);
                setReason('');
              }}
            >
              {t('periods.leaveClosed')}
            </button>
          </div>
        </section>
      )}

      <p className="error" aria-live="polite">
        {error ?? ''}
      </p>

      {load.periods.length === 0 ? (
        <p className="note">{t('periods.none')}</p>
      ) : (
        <div className="scroll">
          <table className="table">
            <caption className="visually-hidden">{t('periods.caption')}</caption>
            <thead>
              <tr>
                <th scope="col">{t('periods.colMonth')}</th>
                <th scope="col">{t('periods.colCutoff')}</th>
                <th scope="col" className="num">
                  {t('periods.colEntries')}
                </th>
                <th scope="col">{t('periods.colState')}</th>
                <th scope="col">&nbsp;</th>
              </tr>
            </thead>
            <tbody>
              {load.periods.map((period) => (
                <tr key={period.id}>
                  <td>{monthName(period.year, period.month)}</td>
                  <td>{format.date(period.endsOn)}</td>
                  <td className="num">{format.number(period.entries)}</td>
                  <td>
                    {period.state === 'Open' ? (
                      <span className="chip chip--won">{t('enum.periodState.Open')}</span>
                    ) : (
                      <span className="chip chip--lost">{t('enum.periodState.Closed')}</span>
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
                              t('periods.confirmClose', {
                                month: monthName(period.year, period.month),
                              }),
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
                        {t('periods.closeIt')}
                      </button>
                    ) : (
                      <button type="button" disabled={busy} onClick={() => setReopening(period)}>
                        {t('periods.reopen')}
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
          <h2>{t('periods.historyTitle')}</h2>
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
                  {/* The month and the verb are one catalogue sentence rather
                      than a name with a word appended. German puts the verb
                      last and Arabic puts it first; neither can be built by
                      concatenating in English order. */}
                  <span className="strong">
                    {t(
                      entry.toState === 'Closed'
                        ? 'periods.wasClosed'
                        : entry.fromState === null
                          ? 'periods.wasOpened'
                          : 'periods.wasReopened',
                      { month: monthName(period.year, period.month) },
                    )}
                  </span>{' '}
                  <span className="muted">
                    {format.dateTime(entry.occurredAt)}
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
  monthName,
  onCancel,
  onOpen,
}: {
  suggested: { year: number; month: number };
  busy: boolean;
  monthName: (year: number, month: number) => string;
  onCancel: () => void;
  onOpen: (year: number, month: number) => void;
}) {
  const { t } = useI18n();
  const [year, setYear] = useState(String(suggested.year));
  const [month, setMonth] = useState(String(suggested.month));

  return (
    <section className="panel">
      <h2>{t('periods.openAMonth')}</h2>
      <p className="note">{t('periods.openLede')}</p>

      <div className="row">
        <div className="field">
          <label htmlFor="open-year">{t('periods.year')}</label>
          <input
            id="open-year"
            inputMode="numeric"
            value={year}
            onChange={(event) => setYear(event.target.value)}
          />
        </div>

        <div className="field">
          <label htmlFor="open-month">{t('periods.month')}</label>
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
          {t('periods.openIt')}
        </button>
        <button type="button" onClick={onCancel}>
          {t('common.cancel')}
        </button>
      </div>
    </section>
  );
}
