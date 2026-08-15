// LabourPage — what the workshop sold over a period, and what an hour realised.
//
// Use:  reachable at /workshop/labour, linked from the workshop's head.
// Edit: THE "WHAT THIS DOES NOT MEASURE" BAND IS NOT DECORATION AND MUST NOT BE
//       QUIETLY DROPPED. Efficiency and productivity are the two figures a
//       service manager is trained to look for on a report like this, and this
//       system cannot produce either — there is no roster and no time clock, so
//       neither denominator exists. A report that shows three numbers and stays
//       silent about the missing two invites somebody to assume they were fine,
//       and both are used to judge individual people. The server names them in
//       `notMeasured` precisely so this screen can say so.
//
//       The other honesty here is the effective labour rate: revenue ÷ hours
//       sold, which is what an hour ACTUALLY realised as against the posted
//       rate. A workshop billing 120 and realising 94 is the single most useful
//       thing on this screen, and the two figures next to each other are what
//       make the gap visible.
//
//       Counted from invoiced work only. Work in progress is not revenue.

import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router';
import { ApiError, api } from '../../shared/api';
import type { LabourPerformance, StaffMember } from '../../shared/contracts';
import { useI18n, type MessageKey } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; labour: LabourPerformance }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

/** An ISO date the `<input type="date">` and the API both accept. */
function isoDate(value: Date): string {
  return value.toISOString().slice(0, 10);
}

/**
 * The month so far — the same default the server applies when asked for no
 * period at all. Chosen here as well so the pickers agree with the answer
 * rather than showing blanks over a period somebody has to infer.
 */
function monthToDate(): { from: string; to: string } {
  const today = new Date();
  return {
    from: isoDate(new Date(Date.UTC(today.getFullYear(), today.getMonth(), 1))),
    to: isoDate(today),
  };
}

/**
 * The figures the trade expects that this system cannot honestly produce. The
 * server sends the names; the sentences live here, because how much explanation
 * a reader needs is an interface decision.
 */
function notMeasuredKey(figure: string): MessageKey | null {
  switch (figure) {
    case 'Efficiency':
      return 'labour.noEfficiency';
    case 'Productivity':
      return 'labour.noProductivity';
    default:
      return null;
  }
}

export function LabourPage() {
  const { t, format } = useI18n();
  const describe = useApiMessage();

  const [period, setPeriod] = useState(monthToDate);
  const [load, setLoad] = useState<Load>({ kind: 'loading' });

  // Names for the technician table. Fetched separately because the report
  // answers with ids — the service has no business reading the staff directory,
  // and a report that could would be a way around who may see the staff list.
  const [names, setNames] = useState<Map<string, string> | null>(null);

  const find = useCallback(async () => {
    setLoad({ kind: 'loading' });

    const query = `?from=${encodeURIComponent(period.from)}&to=${encodeURIComponent(period.to)}`;

    try {
      setLoad({ kind: 'ready', labour: await api<LabourPerformance>(`/repair-orders/labour${query}`) });
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

  useEffect(() => {
    let current = true;

    void (async () => {
      try {
        const people = await api<StaffMember[]>('/staff');
        if (current) {
          setNames(new Map(people.map((person) => [person.id, person.displayName])));
        }
      } catch {
        // Somebody may read the workshop's numbers and not the staff list.
        // Null is "we could not ask", which the table says out loud — as
        // against an empty map, which would silently mean "nobody works here".
        if (current) {
          setNames(null);
        }
      }
    })();

    return () => {
      current = false;
    };
  }, []);

  return (
    <section className="page">
      <header className="page__head">
        <h1>{t('labour.title')}</h1>

        <div className="filter">
          <label htmlFor="labour-from">{t('labour.from')}</label>
          <input
            id="labour-from"
            type="date"
            value={period.from}
            onChange={(event) => setPeriod({ ...period, from: event.target.value })}
          />
          <label htmlFor="labour-to">{t('labour.to')}</label>
          <input
            id="labour-to"
            type="date"
            value={period.to}
            onChange={(event) => setPeriod({ ...period, to: event.target.value })}
          />
        </div>
      </header>

      <p className="note">
        <Link to="/workshop">{t('labour.backToWorkshop')}</Link>
      </p>

      <Body load={load} names={names} onRetry={() => void find()} />

      {load.kind === 'ready' ? <NotMeasured figures={load.labour.notMeasured} /> : null}

      {load.kind === 'ready' && names === null ? (
        <p className="note">{t('labour.namesUnavailable')}</p>
      ) : null}

      {load.kind === 'ready' ? (
        <p className="note note--footer">
          {t('labour.period', {
            from: format.date(load.labour.from),
            to: format.date(load.labour.to),
          })}
        </p>
      ) : null}
    </section>
  );
}

function Body({
  load,
  names,
  onRetry,
}: {
  load: Load;
  names: Map<string, string> | null;
  onRetry: () => void;
}) {
  const { t } = useI18n();

  switch (load.kind) {
    case 'loading':
      return (
        <p className="state" aria-live="polite">
          {t('labour.loading')}
        </p>
      );

    case 'denied':
      return (
        <p className="state" role="alert">
          {t('labour.denied')}
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
      return (
        <>
          <Headline labour={load.labour} />
          <ByTechnician labour={load.labour} names={names} />
          <ByPayer labour={load.labour} />
        </>
      );
  }
}

/** The three numbers the whole screen exists for. */
function Headline({ labour }: { labour: LabourPerformance }) {
  const { t, format } = useI18n();

  // The currency is not on the report: every figure is the dealership's own
  // books, and the repair orders behind them are denominated per job. USD is
  // what the seed and every existing screen assume; when a second currency
  // appears this is one of the places that has to be told about it.
  const money = (amount: number) => format.money(amount, 'USD');

  return (
    <table className="table totals">
      <caption className="visually-hidden">{t('labour.headlineCaption')}</caption>
      <tbody>
        <tr>
          <th scope="row">{t('labour.hoursSold')}</th>
          <td className="num">{format.number(labour.hoursSold, { maximumFractionDigits: 2 })}</td>
        </tr>
        <tr>
          <th scope="row">{t('labour.revenue')}</th>
          <td className="num">{money(labour.labourRevenue)}</td>
        </tr>
        <tr className="strong">
          <th scope="row">{t('labour.effectiveRate')}</th>
          <td className="num">{money(labour.effectiveLabourRate)}</td>
        </tr>
      </tbody>
    </table>
  );
}

function ByTechnician({
  labour,
  names,
}: {
  labour: LabourPerformance;
  names: Map<string, string> | null;
}) {
  const { t, format } = useI18n();
  const money = (amount: number) => format.money(amount, 'USD');

  if (labour.byTechnician.length === 0) {
    return <p className="note">{t('labour.nothingInvoiced')}</p>;
  }

  return (
    <>
      <h2>{t('labour.byTechnician')}</h2>
      <div className="scroll">
        <table className="table">
          <caption className="visually-hidden">{t('labour.technicianCaption')}</caption>
          <thead>
            <tr>
              <th scope="col">{t('labour.colWho')}</th>
              <th scope="col" className="num">
                {t('labour.colHours')}
              </th>
              <th scope="col" className="num">
                {t('labour.colRevenue')}
              </th>
              <th scope="col" className="num">
                {t('labour.colRate')}
              </th>
            </tr>
          </thead>
          <tbody>
            {labour.byTechnician.map((row) => (
              <tr key={row.technicianUserId ?? 'unassigned'}>
                <td>
                  {/* Work invoiced with nobody assigned is kept rather than
                      dropped: hours nobody is credited with are exactly what a
                      service manager wants to see. */}
                  {row.technicianUserId === null ? (
                    <span className="muted">{t('labour.nobodyCredited')}</span>
                  ) : (
                    (names?.get(row.technicianUserId) ?? (
                      <span className="muted">{t('labour.notNamed')}</span>
                    ))
                  )}
                </td>
                <td className="num">{format.number(row.hoursSold, { maximumFractionDigits: 2 })}</td>
                <td className="num">{money(row.revenue)}</td>
                <td className="num">{money(row.effectiveLabourRate)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

/**
 * The mix, which is itself the thing a service manager watches. A shop where
 * warranty has quietly become half the hours has a different business from the
 * one it had last quarter, and no single total shows that.
 */
function ByPayer({ labour }: { labour: LabourPerformance }) {
  const { t, format } = useI18n();
  const money = (amount: number) => format.money(amount, 'USD');

  if (labour.byPayer.length === 0) {
    return null;
  }

  return (
    <>
      <h2>{t('labour.byPayer')}</h2>
      <div className="scroll">
        <table className="table">
          <caption className="visually-hidden">{t('labour.payerCaption')}</caption>
          <thead>
            <tr>
              <th scope="col">{t('labour.colPayer')}</th>
              <th scope="col" className="num">
                {t('labour.colHours')}
              </th>
              <th scope="col" className="num">
                {t('labour.colRevenue')}
              </th>
            </tr>
          </thead>
          <tbody>
            {labour.byPayer.map((row) => (
              <tr key={row.payType}>
                <td>{t(`enum.servicePayType.${row.payType}` as MessageKey)}</td>
                <td className="num">{format.number(row.hoursSold, { maximumFractionDigits: 2 })}</td>
                <td className="num">{money(row.revenue)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

/**
 * See the file header. This band is the reason the report can be trusted: it
 * says which questions it is NOT answering, in the words of somebody who knows
 * the reader was looking for them.
 */
function NotMeasured({ figures }: { figures: string[] }) {
  const { t } = useI18n();

  const explained = figures
    .map((figure) => ({ figure, key: notMeasuredKey(figure) }))
    .filter((entry): entry is { figure: string; key: MessageKey } => entry.key !== null);

  if (explained.length === 0) {
    return null;
  }

  return (
    <section className="panel">
      <h2>{t('labour.notMeasuredTitle')}</h2>
      <p className="note">{t('labour.notMeasuredWhy')}</p>
      <ul className="calls">
        {explained.map((entry) => (
          <li key={entry.figure}>{t(entry.key)}</li>
        ))}
      </ul>
    </section>
  );
}
