// DashboardPage — how did we do this month?
//
// Use:  / and /dashboard. The screen somebody lands on.
// Edit: everything on this page comes from ONE call. That is what makes it a
//       single view rather than four panels arriving at four different times,
//       each briefly showing a figure next to a stale one.
//
//       Two rules worth keeping. A withheld section says so in words — a blank
//       panel reads as "the dealership sold nothing", which is a very different
//       statement from "this is not yours to see". And a comparison against a
//       month that made nothing is not a percentage: dividing by zero produces
//       "∞% up", which is worse than saying there is nothing to compare.

import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router';
import { ApiError, api } from '../../shared/api';
import { useHotkeys } from '../../shared/useHotkeys';
import { departments } from '../../shared/contracts';
import type {
  DepartmentResult,
  LedgerPerformance,
  MonthInReview,
  OrganizationSummary,
  StockAging,
} from '../../shared/contracts';

/** Remembered between visits, so a manager of one site is not re-choosing it daily. */
const ROOFTOP_KEY = 'dfoss.dashboard.rooftop';

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; review: MonthInReview }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

interface Chosen {
  year: number;
  month: number;
}

function thisMonth(): Chosen {
  const now = new Date();
  return { year: now.getFullYear(), month: now.getMonth() + 1 };
}

function shift({ year, month }: Chosen, by: number): Chosen {
  const moved = new Date(year, month - 1 + by, 1);
  return { year: moved.getFullYear(), month: moved.getMonth() + 1 };
}

function monthName({ year, month }: Chosen): string {
  return new Date(year, month - 1, 1).toLocaleDateString(undefined, {
    month: 'long',
    year: 'numeric',
  });
}

function isCurrent(chosen: Chosen): boolean {
  const now = thisMonth();
  return chosen.year === now.year && chosen.month === now.month;
}

export function DashboardPage() {
  const [chosen, setChosen] = useState<Chosen>(thisMonth);
  const [rooftopId, setRooftopId] = useState(() => localStorage.getItem(ROOFTOP_KEY) ?? '');
  const [load, setLoad] = useState<Load>({ kind: 'loading' });
  const [rooftops, setRooftops] = useState<{ id: string; name: string }[]>([]);

  // Softens the number swap when the month changes. Without it the figures snap
  // between two unrelated sets and it is genuinely hard to tell the page
  // responded at all.
  const [settling, setSettling] = useState(false);

  const fetchMonth = useCallback(async () => {
    setSettling(true);

    const scope = rooftopId === '' ? '' : `&rooftopId=${rooftopId}`;

    try {
      setLoad({
        kind: 'ready',
        review: await api<MonthInReview>(
          `/reporting/month?year=${chosen.year}&month=${chosen.month}${scope}`,
        ),
      });
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 403) {
        setLoad({ kind: 'denied' });
      } else {
        setLoad({
          kind: 'failed',
          message: failure instanceof ApiError ? failure.message : 'Could not load the month.',
        });
      }
    } finally {
      setSettling(false);
    }
  }, [chosen, rooftopId]);

  useEffect(() => {
    void fetchMonth();
  }, [fetchMonth]);

  // The rooftop list is a convenience. A caller who may not read the
  // organization still gets the whole dashboard for everywhere they can see, so
  // a failure here is silence rather than an error.
  useEffect(() => {
    void (async () => {
      try {
        const organization = await api<OrganizationSummary>('/organization');
        setRooftops(
          organization.legalEntities.flatMap((entity) =>
            entity.rooftops.map((rooftop) => ({ id: rooftop.id, name: rooftop.name })),
          ),
        );
      } catch {
        setRooftops([]);
      }
    })();
  }, []);

  const goto = useCallback((next: Chosen) => setChosen(next), []);

  // Guarded the same way the buttons are. A shortcut that reaches a month the
  // button refuses to offer is a second, quieter set of rules.
  useHotkeys({
    '[': () => goto(shift(chosen, -1)),
    ']': () => {
      if (!isCurrent(chosen)) {
        goto(shift(chosen, 1));
      }
    },
    t: () => goto(thisMonth()),
  });

  function chooseRooftop(id: string) {
    localStorage.setItem(ROOFTOP_KEY, id);
    setRooftopId(id);
  }

  return (
    <>
      <header className="page__head">
        <div>
          <h1>{monthName(chosen)}</h1>
          <p className="muted dash__lede">
            {isCurrent(chosen) ? 'So far this month.' : 'The month as it finished.'}
          </p>
        </div>

        <div className="dash__controls">
          {/* Inline, and no navigation: moving month is a change to this view,
              not a different page. */}
          <div className="switcher" role="group" aria-label="Which month">
            <button type="button" onClick={() => goto(shift(chosen, -1))} title="Previous month ( [ )">
              <Chevron towards="start" />
              <span className="visually-hidden">Previous month</span>
            </button>
            <button type="button" onClick={() => goto(thisMonth())} disabled={isCurrent(chosen)}>
              This month
            </button>
            <button
              type="button"
              onClick={() => goto(shift(chosen, 1))}
              disabled={isCurrent(chosen)}
              title="Next month ( ] )"
            >
              <Chevron towards="end" />
              <span className="visually-hidden">Next month</span>
            </button>
          </div>

          {rooftops.length > 1 ? (
            <div className="filter">
              <label htmlFor="dash-rooftop">Rooftop</label>
              <select
                id="dash-rooftop"
                value={rooftopId}
                onChange={(event) => chooseRooftop(event.target.value)}
              >
                <option value="">Everywhere I can see</option>
                {rooftops.map((rooftop) => (
                  <option key={rooftop.id} value={rooftop.id}>
                    {rooftop.name}
                  </option>
                ))}
              </select>
            </div>
          ) : null}
        </div>
      </header>

      <div className={settling ? 'dash dash--settling' : 'dash'}>
        <Body load={load} onRetry={fetchMonth} />
      </div>
    </>
  );
}

/**
 * An arrow pointing at the start or the end of the line, whichever way the line
 * runs.
 *
 * Drawn rather than typed. "‹" is in Unicode's mirrored set, so whether it turns
 * around inside a right-to-left paragraph is up to the font and the shaping
 * engine — which means the same markup can point two different ways on two
 * machines. An SVG is not subject to any of that, and the CSS below flips it
 * exactly once.
 */
function Chevron({ towards }: { towards: 'start' | 'end' }) {
  return (
    <svg className={`chev chev--${towards}`} viewBox="0 0 8 12" aria-hidden="true" focusable="false">
      <path
        d="M6.5 1 1.5 6l5 5"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.6"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

function Body({ load, onRetry }: { load: Load; onRetry: () => void }) {
  switch (load.kind) {
    case 'loading':
      return (
        <p className="state" aria-live="polite">
          Adding the month up…
        </p>
      );

    case 'denied':
      return (
        <p className="state" role="alert">
          You do not have access to any of the figures on this dashboard.
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
      return <Review review={load.review} />;
  }
}

function Review({ review }: { review: MonthInReview }) {
  const money = useMemo(
    () =>
      new Intl.NumberFormat(undefined, {
        style: 'currency',
        currency: review.trading?.currency || 'USD',
        maximumFractionDigits: 0,
      }),
    [review.trading?.currency],
  );

  return (
    <>
      <Books review={review} />

      {review.withheld.map((section) => (
        <p key={section} className="notice" role="note">
          {section === 'Trading'
            ? 'The money on this month is not yours to see, so the figures below are only the stock.'
            : 'Stock is not yours to see, so this month shows only what was sold.'}
        </p>
      ))}

      {review.trading === null ? null : (
        <Trading trading={review.trading} prior={review.priorMonth} money={money} />
      )}

      {review.stock === null ? null : <Stock stock={review.stock} />}
    </>
  );
}

/**
 * Whether the month is still open, said as a sentence. The state is what decides
 * whether these figures can still move, and that is the first thing somebody
 * reading a gross number needs to know.
 */
function Books({ review }: { review: MonthInReview }) {
  const closed = review.closedAt === null ? '' : ` on ${new Date(review.closedAt).toLocaleDateString()}`;

  const words: Record<string, string> = {
    Open: 'The books are open, so these figures can still move.',
    Closed: `The books are closed${closed}. These are the figures that were reported.`,
    NotOpened: 'Nobody has opened the books for this month, so nothing can post into it.',
    Unknown: 'Whether the books are open is not yours to see.',
  };

  return (
    <p className={`verdict verdict--${review.books.toLowerCase()}`} role="status">
      {words[review.books]}
    </p>
  );
}

function Trading({
  trading,
  prior,
  money,
}: {
  trading: LedgerPerformance;
  prior: LedgerPerformance | null;
  money: Intl.NumberFormat;
}) {
  const find = (name: string, from: LedgerPerformance | null): DepartmentResult | undefined =>
    from?.departments.find((department) => department.name === name);

  const tiles = [
    { label: 'Total gross', now: trading.totalGross, was: prior?.totalGross, lead: true },
    ...Object.values(departments).map((name) => ({
      label: name === departments.finance ? 'F&I' : name,
      now: find(name, trading)?.gross ?? 0,
      was: find(name, prior)?.gross,
      lead: false,
    })),
  ];

  return (
    <>
      <section aria-label="What the month made">
        <div className="tiles">
          {tiles.map((tile) => (
            <Tile
              key={tile.label}
              label={tile.label}
              value={money.format(tile.now)}
              lead={tile.lead}
              change={change(tile.now, tile.was)}
            />
          ))}
        </div>
      </section>

      <div className="dash__split">
        <section className="panel panel--dash" aria-label="What sold">
          <h2>What sold</h2>

          <dl className="figures">
            <div>
              <dt>Cars delivered</dt>
              <dd>
                {trading.vehiclesDelivered}
                <Was value={prior?.vehiclesDelivered} />
              </dd>
            </div>
            <div>
              <dt>Jobs invoiced</dt>
              <dd>
                {trading.serviceInvoices}
                <Was value={prior?.serviceInvoices} />
              </dd>
            </div>
            <div>
              <dt>Gross per car</dt>
              <dd>
                {trading.vehiclesDelivered === 0 ? (
                  // No cars is no average. "— front and back together" reads as
                  // a qualifier on a figure that is not there.
                  <>—</>
                ) : (
                  <>
                    {money.format(
                      ((find(departments.vehicles, trading)?.gross ?? 0) +
                        (find(departments.finance, trading)?.gross ?? 0)) /
                        trading.vehiclesDelivered,
                    )}
                    <span className="muted"> front and back together</span>
                  </>
                )}
              </dd>
            </div>
          </dl>
        </section>

        <section className="panel panel--dash" aria-label="Where the gross came from">
          <h2>Where the gross came from</h2>

          <div className="scroll">
            <table>
              <thead>
                <tr>
                  <th scope="col">Department</th>
                  <th scope="col" className="num">
                    Revenue
                  </th>
                  <th scope="col" className="num">
                    Cost
                  </th>
                  <th scope="col" className="num">
                    Gross
                  </th>
                  <th scope="col" className="num">
                    Margin
                  </th>
                </tr>
              </thead>
              <tbody>
                {trading.departments.map((department) => (
                  <tr key={department.name}>
                    <td>{department.name}</td>
                    <td className="num mono">{money.format(department.revenue)}</td>
                    <td className="num mono">{money.format(department.cost)}</td>
                    <td className="num mono strong">{money.format(department.gross)}</td>
                    <td className="num mono">
                      {/* Nothing sold is not a margin of zero. */}
                      {department.margin === null
                        ? '—'
                        : `${Math.round(department.margin * 100)}%`}
                    </td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr>
                  <td>Total</td>
                  <td className="num mono">{money.format(trading.totalRevenue)}</td>
                  <td className="num mono">{money.format(trading.totalCost)}</td>
                  <td className="num mono strong">{money.format(trading.totalGross)}</td>
                  <td />
                </tr>
              </tfoot>
            </table>
          </div>
        </section>
      </div>
    </>
  );
}

function Stock({ stock }: { stock: StockAging }) {
  // Bars are drawn relative to the fullest band, not to the total: three bands
  // of four cars each would otherwise all be a third of the width and say
  // nothing.
  const widest = Math.max(1, ...stock.bands.map((band) => band.units));

  return (
    <section className="panel panel--dash" aria-label="How old the stock is">
      <h2>
        How old the stock is
        <span className="muted"> — {stock.units} unsold, as at {stock.asOf}</span>
      </h2>

      {stock.units === 0 ? (
        <p className="muted">Nothing unsold on the lot.</p>
      ) : (
        <>
          <ul className="bands">
            {stock.bands.map((band) => (
              <li key={band.name}>
                {/* The count is text as well as width. A bar alone is unreadable
                    to a screen reader and imprecise to everybody else. */}
                <span className="bands__name">{band.name}</span>
                <span className="bands__bar" aria-hidden="true">
                  <span style={{ inlineSize: `${(band.units / widest) * 100}%` }} />
                </span>
                <span className="bands__count num">{band.units}</span>
              </li>
            ))}
          </ul>

          {stock.oldest.length === 0 ? null : (
            <>
              <h3>Standing longest</h3>
              <div className="scroll">
                <table>
                  <thead>
                    <tr>
                      <th scope="col">Stock</th>
                      <th scope="col">Vehicle</th>
                      <th scope="col">Status</th>
                      <th scope="col" className="num">
                        Days
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {stock.oldest.map((unit) => (
                      <tr key={unit.id}>
                        <td className="mono">
                          {/* Straight to the car, because the point of naming it
                              is that somebody does something about it. */}
                          <Link to={`/inventory?stock=${unit.stockNumber}`}>{unit.stockNumber}</Link>
                        </td>
                        <td>{unit.vehicleDisplayName}</td>
                        <td>
                          <span className={`chip chip--${unit.status.toLowerCase()}`}>
                            {unit.status}
                          </span>
                        </td>
                        <td className="num mono strong">
                          {unit.daysInStock}
                          {unit.ageIsEstimated ? (
                            <abbr title="No acquisition date was recorded, so this counts from when it was entered.">
                              *
                            </abbr>
                          ) : null}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              {stock.oldest.some((unit) => unit.ageIsEstimated) ? (
                <p className="note">
                  * counted from when the car was entered, because no acquisition date was recorded.
                </p>
              ) : null}
            </>
          )}
        </>
      )}
    </section>
  );
}

function Tile({
  label,
  value,
  change,
  lead,
}: {
  label: string;
  value: string;
  change: Change;
  lead: boolean;
}) {
  return (
    <div className={lead ? 'tile tile--lead' : 'tile'}>
      <span className="tile__label">{label}</span>
      <strong className="tile__value">{value}</strong>
      <span className={`tile__change tile__change--${change.direction}`}>
        {/* The arrow is decorative; the word carries the meaning, because
            direction by colour and glyph alone is unreadable to plenty of
            people. */}
        <span aria-hidden="true">{change.arrow} </span>
        {change.words}
      </span>
    </div>
  );
}

type Change = { direction: 'up' | 'down' | 'flat'; arrow: string; words: string };

/**
 * This figure against last month's, in words.
 *
 * A month that made nothing has no percentage to offer — dividing by it gives
 * "∞% up", which is a worse answer than admitting there is nothing to compare.
 */
function change(now: number, was: number | undefined): Change {
  if (was === undefined) {
    return { direction: 'flat', arrow: '', words: 'no comparison' };
  }

  if (was === 0) {
    return now === 0
      ? { direction: 'flat', arrow: '=', words: 'nothing last month either' }
      : { direction: 'up', arrow: '▲', words: 'up from nothing last month' };
  }

  const percent = Math.round(((now - was) / Math.abs(was)) * 100);

  if (percent === 0) {
    return { direction: 'flat', arrow: '=', words: 'level with last month' };
  }

  return {
    direction: percent > 0 ? 'up' : 'down',
    arrow: percent > 0 ? '▲' : '▼',
    words: `${Math.abs(percent)}% ${percent > 0 ? 'up on' : 'down on'} last month`,
  };
}

/** Last month's count, beside this month's. */
function Was({ value }: { value: number | undefined }) {
  if (value === undefined) {
    return null;
  }

  return <span className="muted"> was {value}</span>;
}
