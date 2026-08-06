// LeadsPage — the enquiries being chased, before any of them is a deal.
//
// Use:  reachable at /leads. Selecting one opens it.
// Edit: two things here are deliberate and easy to undo by accident.
//
//       (1) The moves offered come from the server's `availableMoves`, which is
//       `LeadStatusRules` and nothing else. Do not add a transition table here —
//       the deal desk was written the same way for the same reason, and a second
//       copy of a rule drifts with the browser's copy being the wrong one.
//
//       (2) There is no rooftop picker on the list. A lead is rooftop-scoped and
//       LeadService already filters to the caller's authorized lots, so the list
//       is *already* the right list. Offering a picker would imply a person can
//       look at another location's enquiries, and the server would refuse.

import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router';
import { ApiError, api, post } from '../../shared/api';
import { useSession } from '../../app/session';
import { CaptureLead, sourceLabel } from './CaptureLead';
import type { LeadDetail, LeadStatus, LeadSummary, StaffMember } from '../../shared/contracts';

const PageSize = 50;

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; leads: LeadSummary[] }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

export function LeadsPage() {
  const { user } = useSession();
  const [openOnly, setOpenOnly] = useState(true);
  const [mineOnly, setMineOnly] = useState(false);
  const [load, setLoad] = useState<Load>({ kind: 'loading' });
  const [selected, setSelected] = useState<LeadDetail | null>(null);
  const [capturing, setCapturing] = useState(false);

  const me = user?.userId ?? null;

  // Built here rather than inside the effect so that who "mine" is only matters
  // when the filter is on. Depending on the user id unconditionally made the list
  // load twice on every visit — once before /auth/me answered and once after.
  const query =
    `openOnly=${openOnly}&limit=${PageSize}` +
    (mineOnly && me !== null ? `&assignedTo=${me}` : '');

  const find = useCallback(async (filters: string) => {
    setLoad({ kind: 'loading' });

    try {
      setLoad({
        kind: 'ready',
        leads: await api<LeadSummary[]>(`/leads?${filters}`),
      });
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 403) {
        setLoad({ kind: 'denied' });
        return;
      }

      setLoad({
        kind: 'failed',
        message: failure instanceof ApiError ? failure.message : 'Could not load the enquiries.',
      });
    }
  }, []);

  useEffect(() => {
    void find(query);
  }, [find, query]);

  async function open(leadId: string) {
    try {
      setSelected(await api<LeadDetail>(`/leads/${leadId}`));
    } catch (failure) {
      setLoad({
        kind: 'failed',
        message: failure instanceof ApiError ? failure.message : 'Could not open that enquiry.',
      });
    }
  }

  return (
    <>
      <header className="page__head">
        <h1>Enquiries</h1>

        <div className="filter">
          <label htmlFor="open-leads">Show</label>
          <select
            id="open-leads"
            value={openOnly ? 'open' : 'all'}
            onChange={(e) => setOpenOnly(e.target.value === 'open')}
          >
            <option value="open">Still being chased</option>
            <option value="all">Everything</option>
          </select>

          <label htmlFor="mine-only" className="check">
            <input
              id="mine-only"
              type="checkbox"
              checked={mineOnly}
              onChange={(e) => setMineOnly(e.target.checked)}
            />
            Only mine
          </label>
        </div>
      </header>

      {capturing ? (
        <CaptureLead
          onCaptured={async (lead) => {
            setCapturing(false);
            setSelected(lead);
            await find(query);
          }}
          onCancel={() => setCapturing(false)}
        />
      ) : (
        <div className="actions actions--lead">
          <button type="button" className="primary" onClick={() => setCapturing(true)}>
            Take an enquiry
          </button>
        </div>
      )}

      {selected === null ? null : (
        <LeadPanel
          lead={selected}
          me={me}
          onChanged={async (updated) => {
            setSelected(updated);
            await find(query);
          }}
          onClose={() => setSelected(null)}
        />
      )}

      <Body
        load={load}
        me={me}
        onRetry={() => void find(query)}
        onOpen={(id) => void open(id)}
      />
    </>
  );
}

function LeadPanel({
  lead, me, onChanged, onClose,
}: {
  lead: LeadDetail;
  me: string | null;
  onChanged: (updated: LeadDetail) => Promise<void>;
  onClose: () => void;
}) {
  const navigate = useNavigate();
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function act(work: () => Promise<LeadDetail>) {
    setError(null);
    setBusy(true);

    try {
      await onChanged(await work());
      setNote('');
    } catch (failure) {
      // The server's refusal is the honest one: it knows who is asking and which
      // rule they hit. Anything invented here would be a guess.
      setError(failure instanceof ApiError ? failure.message : 'That did not work.');
    } finally {
      setBusy(false);
    }
  }

  const move = (status: LeadStatus) =>
    act(() =>
      post<LeadDetail>(`/leads/${lead.id}/status`, {
        status,
        note: note.trim() === '' ? null : note.trim(),
      }),
    );

  const assign = (userId: string | null) =>
    act(() => post<LeadDetail>(`/leads/${lead.id}/assign`, { assignedToUserId: userId }));

  const mine = me !== null && lead.assignedToUserId === me;

  return (
    <section className="panel panel--deal">
      <header className="page__head">
        <h2>
          {lead.customerName}
          {lead.vehicleOfInterest === null ? null : (
            <>
              {' '}
              <span className="muted">·</span> {lead.vehicleOfInterest}
            </>
          )}
        </h2>
        <button type="button" onClick={onClose}>
          Close
        </button>
      </header>

      <p className="muted">
        <span className={`chip chip--${lead.status.toLowerCase()}`}>{lead.status}</span>{' '}
        · {sourceLabel(lead.source)} · came in{' '}
        {new Date(lead.capturedAt).toLocaleDateString()}
      </p>

      {lead.enquiry === null ? null : <blockquote className="enquiry">{lead.enquiry}</blockquote>}

      <p className="note">
        {lead.assignedToUserId === null
          ? 'Nobody has picked this up yet.'
          : mine
            ? 'You are chasing this one.'
            : `${lead.assignedTo ?? 'Somebody else'} is chasing this one.`}
      </p>

      <div className="actions">
        {mine ? (
          <button type="button" disabled={busy} onClick={() => void assign(null)}>
            Put it back in the pool
          </button>
        ) : (
          <button type="button" disabled={busy || me === null} onClick={() => void assign(me)}>
            {lead.assignedToUserId === null ? 'I will chase this' : 'Take it over'}
          </button>
        )}

        <HandOver
          busy={busy}
          exclude={lead.assignedToUserId}
          onHandOver={(userId) => void assign(userId)}
        />
      </div>

      <p className="error" aria-live="polite">
        {error ?? ''}
      </p>

      <Moves lead={lead} busy={busy} note={note} onNote={setNote} onMove={(s) => void move(s)} />

      {lead.status === 'Won' ? (
        <div className="actions">
          <button
            type="button"
            className="primary"
            onClick={() =>
              // The deal records which enquiry produced it, so the desk is opened
              // with this lead attached rather than the salesperson retyping the
              // buyer and losing the link between the two.
              navigate(`/deals?leadId=${lead.id}&customerId=${lead.customerId}`)
            }
          >
            Build the deal
          </button>
        </div>
      ) : null}

      <h3>What happened</h3>
      <ol className="history">
        {[...lead.history].reverse().map((entry, index) => (
          <li key={`${entry.toStatus}-${entry.occurredAt}-${index}`}>
            <span className="strong">{entry.toStatus}</span>{' '}
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

/**
 * Where this enquiry can go next.
 *
 * The list comes from the server, which reads it off `LeadStatusRules`. This file
 * holds no opinion about which move follows which — that is exactly the knowledge
 * that must not exist in two places.
 */
/**
 * Hand an enquiry to a named colleague.
 *
 * Loads the staff list on mount and renders NOTHING if the caller cannot read it
 * — a salesperson without `Staff.Read` still has claim and release, and an empty
 * picker sitting there would read as a broken screen rather than as a permission
 * they do not hold. Same reasoning as the missing rooftop picker on the list.
 */
function HandOver({
  busy,
  exclude,
  onHandOver,
}: {
  busy: boolean;
  exclude: string | null;
  onHandOver: (userId: string) => void;
}) {
  const [colleagues, setColleagues] = useState<StaffMember[] | null>(null);

  useEffect(() => {
    let current = true;

    void (async () => {
      try {
        const people = await api<StaffMember[]>('/staff');
        if (current) {
          setColleagues(people);
        }
      } catch {
        // Refused, or unreachable. Either way there is no picker to draw, and
        // the buttons beside it still work.
        if (current) {
          setColleagues([]);
        }
      }
    })();

    return () => {
      current = false;
    };
  }, []);

  // Two dead ends are excluded, and only two. Somebody who cannot sign in
  // obviously cannot chase an enquiry; somebody holding no role at all can sign
  // in and reach nothing, so an enquiry handed to them disappears. Anything
  // beyond that would mean guessing at permissions from here, and the server is
  // the only party that actually knows.
  const options = (colleagues ?? []).filter(
    (p) => p.canSignIn && p.assignments.length > 0 && p.id !== exclude,
  );

  if (options.length === 0) {
    return null;
  }

  return (
    // The label is associated by id rather than by wrapping the select. A
    // wrapping label takes its accessible name from its whole textContent, which
    // here would be "Hand to" plus every option — so the control had no usable
    // name, and a test querying for one silently matched nothing.
    <div className="handover">
      <label htmlFor="hand-to">Hand to</label>
      <select
        id="hand-to"
        disabled={busy}
        value=""
        onChange={(event) => {
          if (event.target.value !== '') {
            onHandOver(event.target.value);
          }
        }}
      >
        <option value="">Choose a colleague</option>
        {options.map((person) => (
          <option key={person.id} value={person.id}>
            {person.displayName}
          </option>
        ))}
      </select>
    </div>
  );
}

function Moves({
  lead, busy, note, onNote, onMove,
}: {
  lead: LeadDetail;
  busy: boolean;
  note: string;
  onNote: (value: string) => void;
  onMove: (status: LeadStatus) => void;
}) {
  if (lead.availableMoves.length === 0) {
    return (
      <p className="note">
        This enquiry is finished. A customer who comes back later starts a new one.
      </p>
    );
  }

  return (
    <>
      <label htmlFor="lead-note">Note (goes on the record)</label>
      <input
        id="lead-note"
        value={note}
        onChange={(e) => onNote(e.target.value)}
        placeholder="Left a voicemail · coming in Saturday · bought elsewhere"
        autoComplete="off"
      />

      <div className="actions">
        {lead.availableMoves.map((status) => (
          <button
            key={status}
            type="button"
            className={status === 'Lost' ? undefined : 'primary'}
            disabled={busy}
            onClick={() => onMove(status)}
          >
            {moveLabel(lead.status, status)}
          </button>
        ))}
      </div>

      {lead.status === 'Lost' ? (
        <p className="note">
          A lost enquiry that comes back is reopened here rather than retyped, so
          the first attempt stays part of the story.
        </p>
      ) : null}
    </>
  );
}

/** The move as a person would say it, given where the lead is now. */
function moveLabel(from: LeadStatus, to: LeadStatus): string {
  switch (to) {
    case 'Working':
      return from === 'Lost' ? 'Reopen it' : 'Start chasing';
    case 'Appointment':
      return 'They are coming in';
    case 'Won':
      return 'They are buying';
    case 'Lost':
      return 'Mark it lost';
    default:
      return to;
  }
}

function Body({
  load, me, onRetry, onOpen,
}: {
  load: Load;
  me: string | null;
  onRetry: () => void;
  onOpen: (id: string) => void;
}) {
  switch (load.kind) {
    case 'loading':
      return (
        <p className="state" aria-live="polite">
          Loading the enquiries…
        </p>
      );

    case 'denied':
      return (
        <p className="state" role="alert">
          You do not have access to enquiries at this location. Ask a manager if
          you think that is wrong.
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
      return load.leads.length === 0 ? (
        <p className="state">
          No enquiries here. One starts the moment somebody rings up or walks onto
          the lot.
        </p>
      ) : (
        <LeadTable leads={load.leads} me={me} onOpen={onOpen} />
      );
  }
}

function LeadTable({
  leads, me, onOpen,
}: {
  leads: LeadSummary[];
  me: string | null;
  onOpen: (id: string) => void;
}) {
  const capped = leads.length >= PageSize;

  return (
    <div className="scroll">
      <table>
        <caption className="visually-hidden">
          {capped
            ? `The first ${leads.length} enquiries. There may be more.`
            : `${leads.length} enquiries`}
        </caption>
        <thead>
          <tr>
            <th scope="col">Customer</th>
            <th scope="col">Asked about</th>
            <th scope="col">Came from</th>
            <th scope="col" className="num">Days</th>
            <th scope="col">Chased by</th>
            <th scope="col">Stage</th>
          </tr>
        </thead>
        <tbody>
          {leads.map((lead) => (
            <tr key={lead.id}>
              <td>
                <button type="button" className="link" onClick={() => onOpen(lead.id)}>
                  {lead.customerName}
                </button>
              </td>
              <td>{lead.vehicleOfInterest ?? 'Nothing specific'}</td>
              <td>{sourceLabel(lead.source)}</td>
              <td className="num">{lead.daysOpen}</td>
              <td>
                {lead.assignedToUserId === null
                  ? 'Nobody yet'
                  : me !== null && lead.assignedToUserId === me
                    ? 'You'
                    : (lead.assignedTo ?? 'Somebody else')}
              </td>
              <td>
                <span className={`chip chip--${lead.status.toLowerCase()}`}>{lead.status}</span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {capped ? (
        <p className="note note--footer">
          Showing the first {leads.length}. There may be more — narrow it with the
          filters until paging exists.
        </p>
      ) : null}
    </div>
  );
}
