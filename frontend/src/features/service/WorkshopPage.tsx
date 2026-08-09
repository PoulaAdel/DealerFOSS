// WorkshopPage — the jobs in the workshop, and what each of them is waiting on.
//
// Use:  reachable at /workshop. Selecting a job opens it.
// Edit: four things here are deliberate.
//
//       (1) The list leads with **work waiting on a customer**, not with a status
//       filter. Every one of those is a phone call somebody owes and an invoice
//       that cannot go out, and `linesAwaitingAnswer` exists on the summary
//       precisely so it can be drawn without opening anything.
//
//       (2) The moves offered come from the server's `availableMoves`, which is
//       `RepairOrderStatusRules` and nothing else. The deal desk and the leads
//       screen are both written this way. A third copy of a transition table
//       would be the one that drifts, and the browser's copy is always the wrong
//       one to trust.
//
//       (3) Invoice is OFFERED even when a line is still Pending, and the
//       server's refusal is shown. Predicting it here would be a second copy of
//       the rule; worse, the refusal names the specific job you still need to
//       ring about, which is more useful than a greyed-out button.
//
//       (4) Recording what the customer said is its own act with its own
//       permission. A technician can write work up and not answer for it. They
//       are deliberately not forbidden from being the same person — in a small
//       shop the advisor who spots it is usually the one who telephones.

import { useCallback, useEffect, useState } from 'react';
import { ApiError, api, openDocument, post, remove } from '../../shared/api';
import { DiaryPanel } from './DiaryPanel';
import { useI18n, type MessageKey } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';
import type {
  RepairOrderDetail,
  RepairOrderStatus,
  RepairOrderSummary,
  ServiceLineKind,
  ServiceLineView,
  StaffMember,
} from '../../shared/contracts';

const PageSize = 50;

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; jobs: RepairOrderSummary[] }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

/**
 * Money in the reader's language. Was a module-level `Intl.NumberFormat` with
 * an `undefined` locale, which followed the operating system rather than the
 * application — so a French screen could show `$1,234.50`.
 */
function useMoney() {
  const { format } = useI18n();
  return (amount: number, currency: string) => format.money(amount, currency);
}

/**
 * A job's stage, and the button that moves it there — two different sets of
 * words for the same five values, deliberately. "Work finished" describes where
 * the job IS; "Work is finished" is somebody telling the system so. Both are
 * keys rather than sentences, so both survive translation.
 *
 * The workshop's own wording, not `enum.repairOrderStatus.*`: that family is
 * the neutral label a table cell uses, and this screen says it in the trade's
 * words instead.
 */
export function statusKey(status: RepairOrderStatus): MessageKey {
  switch (status) {
    case 'Booked':
      return 'workshop.stageBooked';
    case 'InProgress':
      return 'workshop.stageInProgress';
    case 'Completed':
      return 'workshop.stageCompleted';
    case 'Invoiced':
      return 'workshop.stageInvoiced';
    default:
      return 'workshop.stageCancelled';
  }
}

function moveKey(status: RepairOrderStatus): MessageKey {
  switch (status) {
    case 'InProgress':
      return 'workshop.moveInProgress';
    case 'Completed':
      return 'workshop.moveCompleted';
    case 'Invoiced':
      return 'workshop.moveInvoiced';
    case 'Cancelled':
      return 'workshop.moveCancelled';
    default:
      return 'workshop.moveBooked';
  }
}

export function WorkshopPage() {
  const { t } = useI18n();
  const money = useMoney();
  const describe = useApiMessage();

  const [openOnly, setOpenOnly] = useState(true);
  const [load, setLoad] = useState<Load>({ kind: 'loading' });
  const [selected, setSelected] = useState<RepairOrderDetail | null>(null);

  const find = useCallback(async (open: boolean) => {
    setLoad({ kind: 'loading' });

    try {
      setLoad({
        kind: 'ready',
        jobs: await api<RepairOrderSummary[]>(
          `/repair-orders?openOnly=${open}&limit=${PageSize}`,
        ),
      });
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 403) {
        setLoad({ kind: 'denied' });
        return;
      }

      setLoad({
        kind: 'failed',
        message: describe(failure),
      });
    }
  }, []);

  useEffect(() => {
    void find(openOnly);
  }, [find, openOnly]);

  async function open(jobId: string) {
    setSelected(await api<RepairOrderDetail>(`/repair-orders/${jobId}`));
  }

  if (load.kind === 'loading') {
    return <p>{t('workshop.loading')}</p>;
  }

  if (load.kind === 'denied') {
    return (
      <section className="page">
        <h1>{t('workshop.title')}</h1>
        <p className="note">{t('workshop.denied')}</p>
      </section>
    );
  }

  if (load.kind === 'failed') {
    return (
      <section className="page">
        <h1>{t('workshop.title')}</h1>
        <p className="error">{load.message}</p>
        <button type="button" onClick={() => void find(openOnly)}>
          {t('common.retry')}
        </button>
      </section>
    );
  }

  // The advisor's real worklist: the calls they owe. Drawn from the summary, so
  // it costs no extra request.
  const waiting = load.jobs.filter((job) => job.linesAwaitingAnswer > 0);

  return (
    <section className="page">
      <header className="page__head">
        <h1>{t('workshop.title')}</h1>
        <label className="check" htmlFor="workshop-open">
          <input
            id="workshop-open"
            type="checkbox"
            checked={openOnly}
            onChange={(event) => setOpenOnly(event.target.checked)}
          />
          {t('workshop.openOnly')}
        </label>
      </header>

      {/* What is coming, above what is here. A service manager's day is both
          halves at once, and putting the diary behind a second route would make
          somebody hold half the answer in their head. */}
      <DiaryPanel
        onArrived={(job) => {
          setSelected(job);
          void find(openOnly);
        }}
      />

      {waiting.length === 0 ? null : (
        <section className="panel panel--signal">
          <h2>{t('workshop.waitingTitle')}</h2>
          {/* One plural entry rather than a hand-written singular/plural pair:
              the second half of the sentence changes with the count too ("it
              cannot" against "none of them can"), and Russian and Arabic need
              four and six versions of the whole thing. */}
          <p className="note">{t('workshop.waitingNote', { count: waiting.length })}</p>
          <ul className="calls">
            {waiting.map((job) => (
              <li key={job.id}>
                <button type="button" className="link" onClick={() => void open(job.id)}>
                  {job.number} — {job.customerName}
                </button>{' '}
                <span className="muted">
                  {job.vehicle} ·{' '}
                  {t('workshop.toAskAbout', { count: job.linesAwaitingAnswer })}
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}

      {selected === null ? null : (
        <Job
          job={selected}
          onClose={() => setSelected(null)}
          onChanged={async (updated) => {
            setSelected(updated);
            await find(openOnly);
          }}
        />
      )}

      {load.jobs.length === 0 ? (
        <p className="note">
          {openOnly ? t('workshop.nothingOpen') : t('workshop.empty')}
        </p>
      ) : (
        <div className="scroll">
          <table className="table">
            <caption className="visually-hidden">
              {t('workshop.caption2', { limit: PageSize })}
            </caption>
            <thead>
              <tr>
                <th scope="col">{t('workshop.colJob')}</th>
                <th scope="col">{t('workshop.colCustomer')}</th>
                <th scope="col">{t('workshop.colVehicle')}</th>
                <th scope="col">{t('workshop.colCameInFor')}</th>
                <th scope="col">{t('workshop.colWaiting')}</th>
                <th scope="col" className="num">
                  Due
                </th>
                <th scope="col">{t('workshop.colStage')}</th>
              </tr>
            </thead>
            <tbody>
              {load.jobs.map((job) => (
                <tr key={job.id}>
                  <td>
                    <button type="button" className="link" onClick={() => void open(job.id)}>
                      {job.number}
                    </button>
                  </td>
                  <td>{job.customerName}</td>
                  <td>{job.vehicle}</td>
                  <td>{job.complaint}</td>
                  <td>
                    {job.linesAwaitingAnswer === 0 ? (
                      ''
                    ) : (
                      <span className="chip chip--warn">{job.linesAwaitingAnswer} to ask</span>
                    )}
                  </td>
                  <td className="num">{money(job.amountDue, job.currency)}</td>
                  <td>
                    <span className={`chip chip--${job.status.toLowerCase()}`}>
                      {t(statusKey(job.status))}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {load.jobs.length >= PageSize ? (
        <p className="note">
          The first {PageSize}, newest first — there may be more.
        </p>
      ) : null}
    </section>
  );
}

function Job({
  job,
  onClose,
  onChanged,
}: {
  job: RepairOrderDetail;
  onClose: () => void;
  onChanged: (updated: RepairOrderDetail) => Promise<void>;
}) {
  const { t, format } = useI18n();
  const [note, setNote] = useState('');
  const describe = useApiMessage();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function act(work: () => Promise<RepairOrderDetail>) {
    setError(null);
    setBusy(true);

    try {
      await onChanged(await work());
      setNote('');
    } catch (failure) {
      // The server knows which line is unanswered and says so. Anything invented
      // here would be less useful and eventually wrong.
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  const pending = job.lines.filter((line) => line.authorization === 'Pending');

  return (
    <section className="panel panel--deal">
      <header className="page__head">
        <h2>
          {job.number} <span className="muted">·</span> {job.customerName}
        </h2>
        <button type="button" onClick={onClose}>
          {t('common.close')}
        </button>
      </header>

      <p className="muted">
        <span className={`chip chip--${job.status.toLowerCase()}`}>{t(statusKey(job.status))}</span> ·{' '}
        {job.vehicle}
        {job.odometerReading === null ? '' : ` · ${t('workshop.miles', { count: job.odometerReading })}`} ·
        {t('workshop.bookedIn', { date: format.date(job.openedAt) })}
      </p>

      <blockquote className="enquiry">{job.complaint}</blockquote>

      {pending.length === 0 ? null : (
        <p className="note note--warn">
          {t('workshop.pendingNote', { count: pending.length })}
        </p>
      )}

      <Lines job={job} busy={busy} onAct={act} />

      <Totals job={job} />

      <Technician job={job} busy={busy} onAct={act} />

      <p className="error" aria-live="polite">
        {error ?? ''}
      </p>

      <div className="actions">
        {/* Available before invoicing too: an advisor hands over a job sheet to
            explain what is being done. The document says which it is. */}
        <button
          type="button"
          disabled={busy}
          onClick={() => {
            setError(null);
            void openDocument(`/documents/repair-orders/${job.id}`).catch((failure: unknown) =>
              setError(
                describe(failure),
              ),
            );
          }}
        >
          {job.invoicedAt === null ? t('workshop.printJobSheet') : t('workshop.printInvoice')}
        </button>
      </div>

      <Moves job={job} busy={busy} note={note} onNote={setNote} onAct={act} />

      <h3>{t('workshop.whatHappened')}</h3>
      <ol className="history">
        {[...job.history].reverse().map((entry, index) => (
          <li key={`${entry.toStatus}-${entry.occurredAt}-${index}`}>
            <span className="strong">{t(statusKey(entry.toStatus))}</span>{' '}
            <span className="muted">
              {format.dateTime(entry.occurredAt)}
              {entry.note === null ? '' : ` — ${entry.note}`}
            </span>
          </li>
        ))}
      </ol>
    </section>
  );
}

function Lines({
  job,
  busy,
  onAct,
}: {
  job: RepairOrderDetail;
  busy: boolean;
  onAct: (work: () => Promise<RepairOrderDetail>) => Promise<void>;
}) {
  const { t } = useI18n();
  return (
    <>
      <h3>{t('workshop.theWork')}</h3>
      {job.lines.length === 0 ? (
        <p className="note">{t('workshop.nothingWrittenUp')}</p>
      ) : (
        <div className="scroll">
          <table className="table terms">
            <thead>
              <tr>
                <th scope="col">{t('workshop.colWhat')}</th>
                <th scope="col">{t('workshop.colDetail')}</th>
                <th scope="col">{t('workshop.colAgreed')}</th>
                <th scope="col" className="num">
                  Amount
                </th>
                <th scope="col">&nbsp;</th>
              </tr>
            </thead>
            <tbody>
              {job.lines.map((line) => (
                <Line
                  key={line.id}
                  job={job}
                  line={line}
                  busy={busy}
                  onAct={onAct}
                />
              ))}
            </tbody>
          </table>
        </div>
      )}

      {job.linesAreOpen ? <AddLine job={job} busy={busy} onAct={onAct} /> : null}
    </>
  );
}

function Line({
  job,
  line,
  busy,
  onAct,
}: {
  job: RepairOrderDetail;
  line: ServiceLineView;
  busy: boolean;
  onAct: (work: () => Promise<RepairOrderDetail>) => Promise<void>;
}) {
  const { t } = useI18n();
  const money = useMoney();
  const [answering, setAnswering] = useState(false);
  const [answerNote, setAnswerNote] = useState('');

  const answer = (approved: boolean) =>
    onAct(() =>
      post<RepairOrderDetail>(`/repair-orders/${job.id}/lines/${line.id}/answer`, {
        approved,
        note: answerNote.trim() === '' ? null : answerNote.trim(),
      }),
    );

  return (
    <>
      <tr>
        <td>{t(`enum.serviceLineKind.${line.kind}` as MessageKey)}</td>
        <td>
          {line.description}
          {line.hours === null || line.rate === null ? null : (
            <>
              {' '}
              <span className="muted">
({t('workshop.hoursAtRate', { hours: line.hours, rate: money(line.rate, job.currency) })})
              </span>
            </>
          )}
        </td>
        <td>
          {line.authorization === 'Pending' ? (
            <span className="chip chip--warn">{t('workshop.nobodyAsked')}</span>
          ) : line.authorization === 'Declined' ? (
            <span className="chip chip--lost">{t('workshop.saidNo')}</span>
          ) : (
            <span className="chip chip--won">{t('workshop.agreed')}</span>
          )}
          {line.authorizationNote === null ? null : (
            <div className="muted">{line.authorizationNote}</div>
          )}
        </td>
        <td className="num">
          {/*
            Declined work stays on the record at nothing. Showing the amount it
            would have been would read as a charge.
          */}
          {line.authorization === 'Declined' ? '—' : money(line.amount, job.currency)}
        </td>
        <td>
          {/*
            Recording the answer is NOT tied to `linesAreOpen`. That flag governs
            editing the WORK; the server's AnswerLine deliberately has no such
            check, because the real sequence is: finish the job, try to invoice,
            be told to ring, ring, record, invoice. Gating this on `linesAreOpen`
            produced a dead end — the refusal said "record what they said" and
            the screen had hidden the only way to do it. Found by walking it.
          */}
          {line.authorization === 'Pending' ? (
            <button type="button" disabled={busy} onClick={() => setAnswering(!answering)}>
              {answering ? t('workshop.notNow') : t('workshop.iRangThem')}
            </button>
          ) : job.linesAreOpen ? (
            <button
              type="button"
              disabled={busy}
              onClick={() =>
                void onAct(() =>
                  remove<RepairOrderDetail>(`/repair-orders/${job.id}/lines/${line.id}`),
                )
              }
            >
              Remove
            </button>
          ) : null}
        </td>
      </tr>

      {answering ? (
        <tr>
          <td colSpan={5}>
            <div className="field">
              <label htmlFor={`answer-${line.id}`}>{t('workshop.howObtained')}</label>
              <input
                id={`answer-${line.id}`}
                value={answerNote}
                placeholder={t('workshop.howObtainedPlaceholder')}
                onChange={(event) => setAnswerNote(event.target.value)}
              />
              <p className="hint">
                This is the part that matters if the bill is ever questioned. Say who
                you spoke to and when.
              </p>
            </div>
            <div className="actions">
              <button
                type="button"
                className="primary"
                disabled={busy}
                onClick={() => void answer(true)}
              >
                They agreed
              </button>
              <button type="button" disabled={busy} onClick={() => void answer(false)}>
                They said no
              </button>
            </div>
          </td>
        </tr>
      ) : null}
    </>
  );
}

function AddLine({
  job,
  busy,
  onAct,
}: {
  job: RepairOrderDetail;
  busy: boolean;
  onAct: (work: () => Promise<RepairOrderDetail>) => Promise<void>;
}) {
  const { t } = useI18n();
  const [kind, setKind] = useState<ServiceLineKind>('Labour');
  const [description, setDescription] = useState('');
  const [hours, setHours] = useState('');
  const [rate, setRate] = useState('');
  const [amount, setAmount] = useState('');

  const isLabour = kind === 'Labour';

  function add() {
    void onAct(async () => {
      const created = await post<RepairOrderDetail>(`/repair-orders/${job.id}/lines`, {
        kind,
        description: description.trim(),
        hours: isLabour && hours !== '' ? Number(hours) : null,
        rate: isLabour && rate !== '' ? Number(rate) : null,
        unitAmount: isLabour ? 0 : Number(amount || 0),
      });

      setDescription('');
      setHours('');
      setRate('');
      setAmount('');
      return created;
    });
  }

  return (
    <>
      <h4>{t('workshop.writeUpMore')}</h4>
      <p className="note">
        Anything added now needs the customer’s answer before it can be billed —
        which is the point. Write it down while you are looking at it.
      </p>

      <div className="row">
        <div className="field">
          <label htmlFor="line-kind">{t('workshop.lineKind')}</label>
          <select
            id="line-kind"
            value={kind}
            onChange={(event) => setKind(event.target.value as ServiceLineKind)}
          >
            <option value="Labour">{t('enum.serviceLineKind.Labour')}</option>
            <option value="Part">{t('enum.serviceLineKind.Part')}</option>
            <option value="Sublet">{t('enum.serviceLineKind.Sublet')}</option>
          </select>
        </div>

        <div className="field field--grow">
          <label htmlFor="line-description">{t('workshop.lineDescription')}</label>
          <input
            id="line-description"
            value={description}
            onChange={(event) => setDescription(event.target.value)}
          />
        </div>

        {isLabour ? (
          <>
            <div className="field">
              <label htmlFor="line-hours">{t('workshop.hours')}</label>
              <input
                id="line-hours"
                inputMode="decimal"
                value={hours}
                onChange={(event) => setHours(event.target.value)}
              />
            </div>
            <div className="field">
              <label htmlFor="line-rate">{t('workshop.rate')}</label>
              <input
                id="line-rate"
                inputMode="decimal"
                value={rate}
                onChange={(event) => setRate(event.target.value)}
              />
            </div>
          </>
        ) : (
          <div className="field">
            <label htmlFor="line-amount">{t('workshop.amount')}</label>
            <input
              id="line-amount"
              inputMode="decimal"
              value={amount}
              onChange={(event) => setAmount(event.target.value)}
            />
          </div>
        )}
      </div>

      <div className="actions">
        <button
          type="button"
          disabled={busy || description.trim() === ''}
          onClick={add}
        >
          Write it up
        </button>
      </div>
    </>
  );
}

function Totals({ job }: { job: RepairOrderDetail }) {
  const { t } = useI18n();
  const money = useMoney();
  return (
    <table className="table totals">
      <caption className="visually-hidden">{t('workshop.totalsCaption')}</caption>
      <tbody>
        {/*
          Split rather than a single figure: "we sold 464 of service" tells a
          manager nothing, and "180 labour, 284 parts" is what they run the
          department on.
        */}
        <tr>
          <th scope="row">{t('workshop.totalLabour')}</th>
          <td className="num">{money(job.labourTotal, job.currency)}</td>
        </tr>
        <tr>
          <th scope="row">{t('workshop.totalParts')}</th>
          <td className="num">{money(job.partsTotal, job.currency)}</td>
        </tr>
        <tr>
          <th scope="row">{t('workshop.totalSublet')}</th>
          <td className="num">{money(job.subletTotal, job.currency)}</td>
        </tr>
        <tr className="strong">
          <th scope="row">{t('workshop.totalDue')}</th>
          <td className="num">{money(job.amountDue, job.currency)}</td>
        </tr>
      </tbody>
    </table>
  );
}

/**
 * Who is doing the work. Loads the staff list and renders nothing if the caller
 * cannot read it — an empty picker would read as a broken screen rather than as
 * a permission they do not hold.
 */
function Technician({
  job,
  busy,
  onAct,
}: {
  job: RepairOrderDetail;
  busy: boolean;
  onAct: (work: () => Promise<RepairOrderDetail>) => Promise<void>;
}) {
  const { t } = useI18n();
  const [colleagues, setColleagues] = useState<StaffMember[]>([]);

  useEffect(() => {
    let current = true;

    void (async () => {
      try {
        const people = await api<StaffMember[]>('/staff');
        if (current) {
          setColleagues(people);
        }
      } catch {
        if (current) {
          setColleagues([]);
        }
      }
    })();

    return () => {
      current = false;
    };
  }, []);

  // Same two dead ends as the enquiry handover: somebody who cannot sign in, and
  // somebody holding no role at all, cannot do the work.
  const options = colleagues.filter((p) => p.canSignIn && p.assignments.length > 0);

  if (options.length === 0) {
    return null;
  }

  const current = options.find((p) => p.id === job.technicianUserId);

  return (
    <div className="field">
      <label htmlFor="job-technician">{t('workshop.whoIsOnIt')}</label>
      <select
        id="job-technician"
        disabled={busy}
        value={job.technicianUserId ?? ''}
        onChange={(event) =>
          void onAct(() =>
            post<RepairOrderDetail>(`/repair-orders/${job.id}/technician`, {
              technicianUserId: event.target.value === '' ? null : event.target.value,
            }),
          )
        }
      >
        <option value="">{t('workshop.nobodyYet')}</option>
        {options.map((person) => (
          <option key={person.id} value={person.id}>
            {person.displayName}
          </option>
        ))}
      </select>
      {job.technicianUserId !== null && current === undefined ? (
        <p className="hint">
          Assigned to somebody who is not on your staff list — they may work at
          another location.
        </p>
      ) : null}
    </div>
  );
}

function Moves({
  job,
  busy,
  note,
  onNote,
  onAct,
}: {
  job: RepairOrderDetail;
  busy: boolean;
  note: string;
  onNote: (value: string) => void;
  onAct: (work: () => Promise<RepairOrderDetail>) => Promise<void>;
}) {
  const { t, format } = useI18n();
  if (job.availableMoves.length === 0) {
    return (
      <p className="note">
        {job.status === 'Invoiced'
          ? t('workshop.invoicedNothingMore', {
              date: job.invoicedAt === null ? '' : format.date(job.invoicedAt),
            })
          : t('workshop.jobFinished')}
      </p>
    );
  }

  return (
    <>
      <div className="field">
        <label htmlFor="move-note">Note (goes on the record)</label>
        <input id="move-note" value={note} onChange={(event) => onNote(event.target.value)} />
      </div>

      <div className="actions">
        {/*
          Every move the server says is legal, including Invoice while a line is
          unanswered. The refusal that comes back names the job you still need to
          ring about, which is more use than a disabled button.
        */}
        {job.availableMoves.map((move) => (
          <button
            key={move}
            type="button"
            className={move === 'Invoiced' ? 'primary' : undefined}
            disabled={busy}
            onClick={() =>
              void onAct(() =>
                post<RepairOrderDetail>(`/repair-orders/${job.id}/status`, {
                  status: move,
                  note: note.trim() === '' ? null : note.trim(),
                }),
              )
            }
          >
            {t(moveKey(move))}
          </button>
        ))}
      </div>
    </>
  );
}
