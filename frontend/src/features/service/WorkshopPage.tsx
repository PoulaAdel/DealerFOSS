// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   WorkshopPage — the jobs in the workshop, and what each of them is waiting on.
//
// Usage:
//   Reachable at /workshop. Selecting a job opens it.
//
// Coding Instructions:
//   Four things here are deliberate.
//
//   (1) The list leads with **work waiting on a customer**, not with a status
//   filter. Every one of those is a phone call somebody owes and an invoice
//   that cannot go out, and `linesAwaitingAnswer` exists on the summary
//   precisely so it can be drawn without opening anything.
//
//   (2) The moves offered come from the server's `availableMoves`, which is
//   `RepairOrderStatusRules` and nothing else. The deal desk and the leads
//   screen are both written this way. A third copy of a transition table
//   would be the one that drifts, and the browser's copy is always the wrong
//   one to trust.
//
//   (3) Invoice is OFFERED even when a line is still Pending, and the
//   server's refusal is shown. Predicting it here would be a second copy of
//   the rule; worse, the refusal names the specific job you still need to
//   ring about, which is more useful than a greyed-out button.
//
//   (4) Recording what the customer said is its own act with its own
//   permission. A technician can write work up and not answer for it. They
//   are deliberately not forbidden from being the same person — in a small
//   shop the advisor who spots it is usually the one who telephones.
//
//   (5) WHO PAYS IS CHOSEN PER LINE AND SHOWN PER LINE. One job routinely
//   carries all three: the customer's brake pads, a warranty claim for the
//   part that failed, and an internal charge for the courtesy wash. That is
//   why the totals block has four figures rather than one — "Due" is what
//   the CUSTOMER owes and nothing else, and a screen that adds warranty
//   work into it would put a number on an invoice that nobody agreed to.
//
//   Warranty and internal work needs no customer authorization and the
//   server marks it authorized on arrival, so the "Agreed?" column says so
//   rather than claiming somebody was asked.

import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router';
import { ApiError, api, openDocument, post, remove } from '../../shared/api';
import { DiaryPanel } from './DiaryPanel';
import { useI18n, type MessageKey } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';
import { Pager, usePageCaption } from '../../shared/Pager';
import { RecordPicker, type PickerOption } from '../../shared/RecordPicker';
import { TakePayment } from '../receivables/TakePayment';
import {
  servicePayTypes,
  type RepairOrderDetail,
  type RepairOrderStatus,
  type RepairOrderSummary,
  type ServiceLineKind,
  type ServiceLineView,
  type OpCodeView,
  type PartSummary,
  type ServicePayType,
  type StaffMember,
  type Page,
} from '../../shared/contracts';

const PageSize = 50;

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; page: Page<RepairOrderSummary> }
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
  const caption = usePageCaption();

  const [openOnly, setOpenOnly] = useState(true);

  // Which page. Reset when the filter changes.
  const [offset, setOffset] = useState(0);
  const [load, setLoad] = useState<Load>({ kind: 'loading' });
  const [selected, setSelected] = useState<RepairOrderDetail | null>(null);

  // `quiet` keeps the list and the open job on screen while the list is
  // refetched. Without it, every act on a job — answering a line, assigning a
  // technician, writing work up — replaced the WHOLE screen with "Loading the
  // workshop…" for the length of a request, then rebuilt it: the detail band
  // was unmounted and remounted, the write-up form lost what was half typed,
  // and the page jumped. Found by driving it in a browser, not by a test.
  //
  // A first load and a filter change still show the loading state, because
  // then there is genuinely nothing to look at.
  const find = useCallback(async (open: boolean, from: number, quiet = false) => {
    if (!quiet) {
      setLoad({ kind: 'loading' });
    }

    try {
      setLoad({
        kind: 'ready',
        page: await api<Page<RepairOrderSummary>>(
          `/repair-orders?openOnly=${open}&limit=${PageSize}&offset=${from}`,
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
    void find(openOnly, offset);
  }, [find, openOnly, offset]);

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
        <button type="button" onClick={() => void find(openOnly, offset)}>
          {t('common.retry')}
        </button>
      </section>
    );
  }

  // The advisor's real worklist: the calls they owe. Drawn from the summary, so
  // it costs no extra request.
  const waiting = load.page.rows.filter((job) => job.linesAwaitingAnswer > 0);

  /**
   * Whether this page of jobs spans more than one lot.
   *
   * Job numbers restart per rooftop by design — 162 jobs here share 82 numbers,
   * 80 of them used twice — so a list that mixes lots shows two different jobs
   * under one number and nothing separates them. Asking for RO-1082 on
   * 2026-09-15 returned two rows and the only way to tell them apart was to
   * read the DOM.
   *
   * Computed from the rows rather than from the user's rooftops on purpose: it
   * answers "is what I am looking at ambiguous", which is the actual question.
   * A manager who covers three lots but is filtered to one is not looking at
   * anything ambiguous, and a code on every row would just be noise.
   */
  const mixed = new Set(load.page.rows.map((job) => job.rooftopId)).size > 1;

  return (
    <section className="page">
      <header className="page__head">
        <h1>{t('workshop.title')}</h1>
        <label className="check" htmlFor="workshop-open">
          <input
            id="workshop-open"
            type="checkbox"
            checked={openOnly}
            onChange={(event) => {
              setOpenOnly(event.target.checked);
              setOffset(0);
            }}
          />
          {t('workshop.openOnly')}
        </label>
        {/* A route rather than a band: the report is a different QUESTION over a
            different period, not a detail of anything in this list. ADR-020
            keeps routes for areas, and "how did the workshop do" is one. */}
        <Link to="/workshop/labour">{t('workshop.labourReport')}</Link>{' '}
        <Link to="/workshop/setup">{t('workshop.setupLink')}</Link>
      </header>

      {/* What is coming, above what is here. A service manager's day is both
          halves at once, and putting the diary behind a second route would make
          somebody hold half the answer in their head. */}
      <DiaryPanel
        onArrived={(job) => {
          setSelected(job);
          void find(openOnly, offset, true);
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
                  {job.number}
                  {mixed && job.rooftopCode !== '' ? ` ${job.rooftopCode}` : ''} —{' '}
                  {job.customerName}
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
            await find(openOnly, offset, true);
          }}
        />
      )}

      {load.page.total === 0 ? (
        <p className="note">
          {openOnly ? t('workshop.nothingOpen') : t('workshop.empty')}
        </p>
      ) : (
        <div className="scroll">
          <table className="table">
            <caption className="visually-hidden">{caption(load.page)}</caption>
            <thead>
              <tr>
                <th scope="col">{t('workshop.colJob')}</th>
                <th scope="col">{t('workshop.colCustomer')}</th>
                <th scope="col">{t('workshop.colVehicle')}</th>
                <th scope="col">{t('workshop.colCameInFor')}</th>
                <th scope="col">{t('workshop.colWaiting')}</th>
                <th scope="col" className="num">
                  {t('workshop.colDue')}
                </th>
                <th scope="col">{t('workshop.colStage')}</th>
              </tr>
            </thead>
            <tbody>
              {load.page.rows.map((job) => (
                <tr key={job.id}>
                  <td>
                    <button type="button" className="link" onClick={() => void open(job.id)}>
                      {job.number}
                    </button>
                    {/* Only when the list actually mixes lots. At a one-site
                        dealership every row would carry the same code and it
                        would be noise; at a group it is the difference between
                        two rows that otherwise read identically. */}
                    {mixed && job.rooftopCode !== '' ? (
                      <>
                        {' '}
                        <span className="muted">{job.rooftopCode}</span>
                      </>
                    ) : null}
                  </td>
                  <td>{job.customerName}</td>
                  <td>{job.vehicle}</td>
                  <td>{job.complaint}</td>
                  <td>
                    {job.linesAwaitingAnswer === 0 ? (
                      ''
                    ) : (
                      <span className="chip chip--warn">
                        {t('workshop.toAsk', { count: job.linesAwaitingAnswer })}
                      </span>
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

      <Pager page={load.page} onPage={setOffset} />
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
        {/* Always here, unlike in the list. One job open on its own carries no
            context to infer the lot from — and this is the heading somebody
            reads back down a phone to a customer holding a job card. */}
        <h2>
          {job.number}
          {job.rooftopCode === '' ? null : (
            <>
              {' '}
              <span className="muted">{job.rooftopCode}</span>
            </>
          )}{' '}
          <span className="muted">·</span> {job.customerName}
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

      {/* Renders nothing until the job is invoiced, because nothing is owed
          until then. Before this existed a job reached Invoiced and stopped:
          the only thing left to do was print it. */}
      <TakePayment source="RepairOrder" reference={job.id} watch={job.status} />

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
                <th scope="col">{t('workshop.colWhoPays')}</th>
                <th scope="col">{t('workshop.colAgreed')}</th>
                <th scope="col" className="num">
                  {t('workshop.colAmount')}
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
          <span className={`chip chip--pay-${line.payType.toLowerCase()}`}>
            {t(`enum.servicePayType.${line.payType}` as MessageKey)}
          </span>
        </td>
        <td>
          {/*
            Only customer-pay work is the customer's to agree to. Showing
            "Agreed" against a warranty claim would be a record of a
            conversation that never happened — and the server marks those
            authorized on arrival precisely because nobody needs to be asked.
          */}
          {line.payType !== 'CustomerPay' ? (
            <span className="muted">{t('workshop.notCustomersCall')}</span>
          ) : line.authorization === 'Pending' ? (
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
              {t('workshop.removeLine')}
            </button>
          ) : null}
        </td>
      </tr>

      {answering ? (
        <tr>
          <td colSpan={6}>
            <div className="field">
              <label htmlFor={`answer-${line.id}`}>{t('workshop.howObtained')}</label>
              <input
                id={`answer-${line.id}`}
                value={answerNote}
                placeholder={t('workshop.howObtainedPlaceholder')}
                onChange={(event) => setAnswerNote(event.target.value)}
              />
              <p className="hint">{t('workshop.howObtainedHint')}</p>
            </div>
            <div className="actions">
              <button
                type="button"
                className="primary"
                disabled={busy}
                onClick={() => void answer(true)}
              >
                {t('workshop.theySaidYes')}
              </button>
              <button type="button" disabled={busy} onClick={() => void answer(false)}>
                {t('workshop.theySaidNo')}
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
  const [payType, setPayType] = useState<ServicePayType>('CustomerPay');
  const [description, setDescription] = useState('');
  const [hours, setHours] = useState('');
  const [rate, setRate] = useState('');
  const [amount, setAmount] = useState('');

  // The catalogue, and which part of it this line sells. Empty means free text,
  // which stays a legitimate choice: a one-off item bought for a single job
  // never enters the catalogue and still has to be billable.
  const [catalogue, setCatalogue] = useState<PartSummary[]>([]);
  const [partId, setPartId] = useState('');
  const [quantity, setQuantity] = useState('1');

  /**
   * The catalogued job this line sells. Empty means free text, which is as
   * legitimate here as it is for parts — a one-off job nobody will do again
   * still has to be billable.
   */
  const [jobCode, setJobCode] = useState<PickerOption | null>(null);

  const isLabour = kind === 'Labour';
  const isPart = kind === 'Part';

  useEffect(() => {
    if (!isPart || catalogue.length > 0) {
      return;
    }

    void (async () => {
      try {
        setCatalogue((await api<Page<PartSummary>>('/parts?inStockOnly=true&limit=200')).rows);
      } catch {
        // A catalogue that will not load must not stop the job being written up.
        // The line falls back to free text, which is what it did before the
        // picker existed at all.
        setCatalogue([]);
      }
    })();
  }, [isPart, catalogue.length]);

  const chosen = catalogue.find((part) => part.id === partId) ?? null;

  function add() {
    void onAct(async () => {
      const created = await post<RepairOrderDetail>(`/repair-orders/${job.id}/lines`, {
        kind,
        description: description.trim(),
        hours: isLabour && hours !== '' ? Number(hours) : null,
        rate: isLabour && rate !== '' ? Number(rate) : null,
        unitAmount: isLabour ? 0 : Number(amount || 0),
        payType,
        // Sending these is what makes the part come off the shelf at cost when
        // the job is invoiced. Until 2026-09-11 the screen never sent them, so
        // every part billed in a browser was free text: 5300 and 1400 never
        // posted and the dashboard showed a 100% margin on service.
        partId: isPart && partId !== '' ? partId : null,
        partQuantity: isPart && partId !== '' ? Number(quantity || 1) : null,
        // Sending this is what lets the server fill in the standard time and
        // this lot's rate for whoever is paying. Anything typed above still
        // wins — the catalogue fills blanks, it does not overrule a person.
        opCodeId: isLabour && jobCode !== null ? jobCode.id : null,
      });

      setDescription('');
      setHours('');
      setRate('');
      setAmount('');
      setPartId('');
      setQuantity('1');
      setJobCode(null);
      return created;
    });
  }

  const findJobs = useCallback(
    async (term: string, signal: AbortSignal) =>
      (await api<Page<OpCodeView>>(
        `/service/op-codes?search=${encodeURIComponent(term)}&limit=15`,
        { signal },
      )).rows.map((code) => ({
        id: code.id,
        label: code.description,
        // The code and the standard time, because those are what make two
        // similar-sounding jobs different: "Interim service 0.8h" is not
        // "Full service 1.5h".
        hint: `${code.code} · ${code.standardHours}h`,
      })),
    [],
  );

  /** Fills the description and the price from the catalogue, both still editable. */
  function choosePart(id: string) {
    setPartId(id);

    const part = catalogue.find((p) => p.id === id);
    if (part === undefined) {
      return;
    }

    if (description.trim() === '') {
      setDescription(part.description);
    }
  }

  return (
    <>
      <h4>{t('workshop.writeUpMore')}</h4>
      {/* True only of customer-pay work, which is why the sentence changes with
          the picker below rather than standing as one permanent claim. */}
      <p className="note">
        {payType === 'CustomerPay' ? t('workshop.writeUpNote') : t('workshop.writeUpNoteOther')}
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

        {/*
          Defaulted to CustomerPay, which is the overwhelming majority and what
          the server assumes when told nothing. Offered here rather than derived
          from anything: a warranty claim and a customer repair can be the same
          words, the same hours and the same part, and only a person knows which
          one they are looking at.
        */}
        <div className="field">
          <label htmlFor="line-pay">{t('workshop.linePayType')}</label>
          <select
            id="line-pay"
            value={payType}
            onChange={(event) => setPayType(event.target.value as ServicePayType)}
          >
            {servicePayTypes.map((option) => (
              <option key={option} value={option}>
                {t(`enum.servicePayType.${option}` as MessageKey)}
              </option>
            ))}
          </select>
        </div>

        {/* Only on labour: an op code IS a job, and citing one on a part or a
            sublet line would mean nothing. The server refuses it too. */}
        {isLabour ? (
          <div className="field field--grow">
            <RecordPicker
              id="line-op-code"
              label={t('workshop.lineJob')}
              chosen={jobCode}
              onChoose={setJobCode}
              search={findJobs}
            />
            <p className="note">{t('workshop.lineJobNote')}</p>
          </div>
        ) : null}

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

      {!isPart ? null : (
        <div className="row">
          <div className="field">
            <label htmlFor="line-part">{t('workshop.fromTheShelf')}</label>
            <select
              id="line-part"
              value={partId}
              onChange={(event) => choosePart(event.target.value)}
            >
              {/* Free text first, and named as a choice rather than an absence.
                  A one-off item bought for one job never enters the catalogue. */}
              <option value="">{t('workshop.notFromStock')}</option>
              {catalogue.map((part) => (
                <option key={part.id} value={part.id}>
                  {part.partNumber} — {part.description} ({part.quantityOnHand})
                </option>
              ))}
            </select>
          </div>

          {partId === '' ? null : (
            <div className="field">
              <label htmlFor="line-quantity">{t('workshop.howMany')}</label>
              <input
                id="line-quantity"
                inputMode="decimal"
                value={quantity}
                onChange={(event) => setQuantity(event.target.value)}
              />
            </div>
          )}
        </div>
      )}

      {chosen === null ? null : (
        <p className="note">
          {t('workshop.onTheShelf', {
            count: chosen.quantityOnHand,
            number: chosen.partNumber,
          })}
        </p>
      )}

      <div className="actions">
        {/* A line needs SOMETHING to call itself, and a catalogued job is that
            something — its description is what the server writes on the line.
            Requiring the description box as well would mean typing out the name
            of the job you just picked, which is the retyping the catalogue
            exists to end. Caught by a test, not by reading this. */}
        <button
          type="button"
          disabled={busy || (description.trim() === '' && jobCode === null)}
          onClick={add}
        >
          {t('workshop.addLine')}
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

        {/*
          Who settles it, under what it is. These two rows appear only when there
          is something in them — a permanent "Warranty 0.00" on every ordinary
          customer job is noise, and this block is read at the moment somebody
          decides what to charge.

          `totalDue` stays LAST and stays the emphasised one, because it is the
          figure that goes on the customer's invoice. Warranty and internal work
          is money the workshop earns and the customer never sees.
        */}
        {job.warrantyTotal === 0 ? null : (
          <tr>
            <th scope="row">{t('workshop.totalWarranty')}</th>
            <td className="num">{money(job.warrantyTotal, job.currency)}</td>
          </tr>
        )}
        {job.internalTotal === 0 ? null : (
          <tr>
            <th scope="row">{t('workshop.totalInternal')}</th>
            <td className="num">{money(job.internalTotal, job.currency)}</td>
          </tr>
        )}
        {job.warrantyTotal === 0 && job.internalTotal === 0 ? null : (
          <tr>
            <th scope="row">{t('workshop.totalWork')}</th>
            <td className="num">{money(job.workTotal, job.currency)}</td>
          </tr>
        )}

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
        <p className="hint">{t('workshop.assignedElsewhere')}</p>
      ) : null}

      <Clock job={job} busy={busy} colleagues={options} onAct={onAct} />
    </div>
  );
}

/**
 * Time on this job.
 *
 * ASSIGNED AND CLOCKED ON ARE DIFFERENT THINGS, and the screen keeps them
 * apart. One technician is named on the job; several can be on the clock
 * against it at once, and a gearbox out is exactly that. Clocking on here also
 * takes somebody off whatever they were on elsewhere — the server does the
 * switch and records why, because a shop that had to clock off first would stop
 * clocking at all.
 */
function Clock({
  job,
  busy,
  colleagues,
  onAct,
}: {
  job: RepairOrderDetail;
  busy: boolean;
  colleagues: StaffMember[];
  onAct: (work: () => Promise<RepairOrderDetail>) => Promise<void>;
}) {
  const { t, format } = useI18n();

  const [who, setWho] = useState('');
  const open = job.clockings.filter((c) => c.isOpen);

  function nameOf(id: string) {
    return colleagues.find((p) => p.id === id)?.displayName ?? t('workshop.someone');
  }

  return (
    <div className="panel-inset">
      <h4>{t('workshop.clockTitle')}</h4>

      {/* Zero until somebody clocks off. See TechnicianClocking.Hours: a figure
          that changed every time you looked at it could not be reconciled
          against the hours sold. */}
      <p className="note">
        {t('workshop.clockedSoFar', {
          hours: format.number(job.clockedHours, { maximumFractionDigits: 2 }),
        })}
      </p>

      {open.length === 0 ? null : (
        <ul className="history">
          {open.map((entry) => (
            <li key={entry.id}>
              <span className="strong">{nameOf(entry.technicianUserId)}</span>{' '}
              <span className="muted">
                {t('workshop.onSince', { since: format.dateTime(entry.startedAt) })}
              </span>{' '}
              <button
                type="button"
                disabled={busy}
                onClick={() =>
                  void onAct(() =>
                    post<RepairOrderDetail>(`/repair-orders/${job.id}/clock-off`, {
                      technicianUserId: entry.technicianUserId,
                    }),
                  )
                }
              >
                {t('workshop.clockOff')}
              </button>
            </li>
          ))}
        </ul>
      )}

      {job.linesAreOpen ? (
        <div className="actions">
          <label htmlFor="clock-who" className="visually-hidden">
            {t('workshop.clockWho')}
          </label>
          <select
            id="clock-who"
            value={who}
            disabled={busy}
            onChange={(event) => setWho(event.target.value)}
          >
            <option value="">{t('workshop.clockWho')}</option>
            {colleagues.map((person) => (
              <option key={person.id} value={person.id}>
                {person.displayName}
              </option>
            ))}
          </select>

          <button
            type="button"
            disabled={busy || who === ''}
            onClick={() =>
              void onAct(() =>
                post<RepairOrderDetail>(`/repair-orders/${job.id}/clock-on`, {
                  technicianUserId: who,
                }),
              ).then(() => setWho(''))
            }
          >
            {t('workshop.clockOn')}
          </button>
        </div>
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
        <label htmlFor="move-note">{t('workshop.moveNote')}</label>
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
