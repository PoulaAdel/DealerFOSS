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

const money = (amount: number, currency: string) =>
  new Intl.NumberFormat(undefined, { style: 'currency', currency }).format(amount);

export function statusLabel(status: RepairOrderStatus): string {
  switch (status) {
    case 'Booked':
      return 'Booked in';
    case 'InProgress':
      return 'Being worked on';
    case 'Completed':
      return 'Work finished';
    case 'Invoiced':
      return 'Invoiced';
    case 'Cancelled':
      return 'Cancelled';
  }
}

/** What each move means to the person clicking it, rather than the enum name. */
function moveLabel(status: RepairOrderStatus): string {
  switch (status) {
    case 'InProgress':
      return 'Start work';
    case 'Completed':
      return 'Work is finished';
    case 'Invoiced':
      return 'Invoice it';
    case 'Cancelled':
      return 'Cancel the job';
    case 'Booked':
      return 'Back to booked';
  }
}

export function WorkshopPage() {
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
        message: failure instanceof ApiError ? failure.message : 'The workshop list could not be read.',
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
    return <p>Loading the workshop…</p>;
  }

  if (load.kind === 'denied') {
    return (
      <section className="page">
        <h1>Workshop</h1>
        <p className="note">
          You do not have access to this location’s workshop. Ask a manager if you
          think that is wrong.
        </p>
      </section>
    );
  }

  if (load.kind === 'failed') {
    return (
      <section className="page">
        <h1>Workshop</h1>
        <p className="error">{load.message}</p>
        <button type="button" onClick={() => void find(openOnly)}>
          Try again
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
        <h1>Workshop</h1>
        <label className="check" htmlFor="workshop-open">
          <input
            id="workshop-open"
            type="checkbox"
            checked={openOnly}
            onChange={(event) => setOpenOnly(event.target.checked)}
          />
          Only jobs still open
        </label>
      </header>

      {waiting.length === 0 ? null : (
        <section className="panel panel--waiting">
          <h2>Waiting on a customer</h2>
          <p className="note">
            {waiting.length === 1
              ? 'One job has work nobody has agreed to pay for yet. It cannot be invoiced until somebody rings.'
              : `${waiting.length} jobs have work nobody has agreed to pay for yet. None of them can be invoiced until somebody rings.`}
          </p>
          <ul className="calls">
            {waiting.map((job) => (
              <li key={job.id}>
                <button type="button" className="link" onClick={() => void open(job.id)}>
                  {job.number} — {job.customerName}
                </button>{' '}
                <span className="muted">
                  {job.vehicle} ·{' '}
                  {job.linesAwaitingAnswer === 1
                    ? '1 job to ask about'
                    : `${job.linesAwaitingAnswer} jobs to ask about`}
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
          {openOnly ? 'Nothing is in the workshop right now.' : 'No jobs here yet.'}
        </p>
      ) : (
        <div className="scroll">
          <table className="table">
            <caption className="visually-hidden">
              The work at the locations you cover, newest first, up to {PageSize}.
            </caption>
            <thead>
              <tr>
                <th scope="col">Job</th>
                <th scope="col">Customer</th>
                <th scope="col">Vehicle</th>
                <th scope="col">Came in for</th>
                <th scope="col">Waiting</th>
                <th scope="col" className="num">
                  Due
                </th>
                <th scope="col">Stage</th>
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
                      {statusLabel(job.status)}
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
  const [note, setNote] = useState('');
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
      setError(failure instanceof ApiError ? failure.message : 'That did not work.');
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
          Close
        </button>
      </header>

      <p className="muted">
        <span className={`chip chip--${job.status.toLowerCase()}`}>{statusLabel(job.status)}</span> ·{' '}
        {job.vehicle}
        {job.odometerReading === null ? '' : ` · ${job.odometerReading.toLocaleString()} miles`} ·
        booked in {new Date(job.openedAt).toLocaleDateString()}
      </p>

      <blockquote className="enquiry">{job.complaint}</blockquote>

      {pending.length === 0 ? null : (
        <p className="note note--warn">
          {pending.length === 1
            ? 'One piece of work is waiting on the customer. It cannot be billed until they answer.'
            : `${pending.length} pieces of work are waiting on the customer. None of them can be billed until they answer.`}
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
                failure instanceof ApiError ? failure.message : 'The document could not be opened.',
              ),
            );
          }}
        >
          {job.invoicedAt === null ? 'Print the job sheet' : 'Print the invoice'}
        </button>
      </div>

      <Moves job={job} busy={busy} note={note} onNote={setNote} onAct={act} />

      <h3>What happened</h3>
      <ol className="history">
        {[...job.history].reverse().map((entry, index) => (
          <li key={`${entry.toStatus}-${entry.occurredAt}-${index}`}>
            <span className="strong">{statusLabel(entry.toStatus)}</span>{' '}
            <span className="muted">
              {new Date(entry.occurredAt).toLocaleString()}
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
  return (
    <>
      <h3>The work</h3>
      {job.lines.length === 0 ? (
        <p className="note">Nothing written up yet.</p>
      ) : (
        <div className="scroll">
          <table className="table terms">
            <thead>
              <tr>
                <th scope="col">What</th>
                <th scope="col">Detail</th>
                <th scope="col">Agreed?</th>
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
        <td>{line.kind}</td>
        <td>
          {line.description}
          {line.hours === null || line.rate === null ? null : (
            <>
              {' '}
              <span className="muted">
                ({line.hours} h at {money(line.rate, job.currency)})
              </span>
            </>
          )}
        </td>
        <td>
          {line.authorization === 'Pending' ? (
            <span className="chip chip--warn">Nobody has asked</span>
          ) : line.authorization === 'Declined' ? (
            <span className="chip chip--lost">Said no</span>
          ) : (
            <span className="chip chip--won">Agreed</span>
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
              {answering ? 'Not now' : 'I rang them'}
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
              <label htmlFor={`answer-${line.id}`}>How it was obtained</label>
              <input
                id={`answer-${line.id}`}
                value={answerNote}
                placeholder="Phoned 10:40, spoke to Mrs Okafor"
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
      <h4>Write up more work</h4>
      <p className="note">
        Anything added now needs the customer’s answer before it can be billed —
        which is the point. Write it down while you are looking at it.
      </p>

      <div className="row">
        <div className="field">
          <label htmlFor="line-kind">What</label>
          <select
            id="line-kind"
            value={kind}
            onChange={(event) => setKind(event.target.value as ServiceLineKind)}
          >
            <option value="Labour">Labour</option>
            <option value="Part">Part</option>
            <option value="Sublet">Sent out</option>
          </select>
        </div>

        <div className="field field--grow">
          <label htmlFor="line-description">Description</label>
          <input
            id="line-description"
            value={description}
            onChange={(event) => setDescription(event.target.value)}
          />
        </div>

        {isLabour ? (
          <>
            <div className="field">
              <label htmlFor="line-hours">Hours</label>
              <input
                id="line-hours"
                inputMode="decimal"
                value={hours}
                onChange={(event) => setHours(event.target.value)}
              />
            </div>
            <div className="field">
              <label htmlFor="line-rate">Rate</label>
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
            <label htmlFor="line-amount">Amount</label>
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
  return (
    <table className="table totals">
      <caption className="visually-hidden">What the job comes to.</caption>
      <tbody>
        {/*
          Split rather than a single figure: "we sold 464 of service" tells a
          manager nothing, and "180 labour, 284 parts" is what they run the
          department on.
        */}
        <tr>
          <th scope="row">Labour</th>
          <td className="num">{money(job.labourTotal, job.currency)}</td>
        </tr>
        <tr>
          <th scope="row">Parts</th>
          <td className="num">{money(job.partsTotal, job.currency)}</td>
        </tr>
        <tr>
          <th scope="row">Sent out</th>
          <td className="num">{money(job.subletTotal, job.currency)}</td>
        </tr>
        <tr className="strong">
          <th scope="row">Due</th>
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
      <label htmlFor="job-technician">Who is on it</label>
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
        <option value="">Nobody yet</option>
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
  if (job.availableMoves.length === 0) {
    return (
      <p className="note">
        {job.status === 'Invoiced'
          ? `Invoiced ${job.invoicedAt === null ? '' : new Date(job.invoicedAt).toLocaleDateString()}. Nothing more to do.`
          : 'This job is finished.'}
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
            {moveLabel(move)}
          </button>
        ))}
      </div>
    </>
  );
}
