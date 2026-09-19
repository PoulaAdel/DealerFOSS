// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   LeadsPage — the enquiries being chased, before any of them is a deal.
//
// Usage:
//   Reachable at /leads. Selecting one opens it.
//
// Coding Instructions:
//   Two things here are deliberate and easy to undo by accident.
//
//   (1) The moves offered come from the server's `availableMoves`, which is
//   `LeadStatusRules` and nothing else. Do not add a transition table here —
//   the deal desk was written the same way for the same reason, and a second
//   copy of a rule drifts with the browser's copy being the wrong one.
//
//   (2) There is no rooftop picker on the list. A lead is rooftop-scoped and
//   LeadService already filters to the caller's authorized lots, so the list
//   is *already* the right list. Offering a picker would imply a person can
//   look at another location's enquiries, and the server would refuse.
//
//   (3) The open enquiry lives at `/leads/:id`, so a manager can send one to
//   the person who should be chasing it. The list stays on screen underneath —
//   see `useRecordRoute` and the 2026-09-16 amendment to ADR-020.

import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router';
import { ApiError, api, post } from '../../shared/api';
import { CloseButton } from '../../shared/CloseButton';
import { RecordBandStatus } from '../../shared/RecordBand';
import { useRecordRoute } from '../../shared/useRecordRoute';
import { useSession } from '../../app/session';
import { CaptureLead } from './CaptureLead';
import type {
  LeadDetail,
  LeadStatus,
  LeadSummary,
  Page,
  StaffMember,
} from '../../shared/contracts';
import { useI18n, type MessageKey } from '../../shared/i18n';
import { useEnumLabel } from '../../shared/i18n/enums';
import { ListTable } from '../../shared/ListScreen';
import { useApiMessage } from '../../shared/i18n/apiMessage';

const PageSize = 50;

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; page: Page<LeadSummary> }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

export function LeadsPage() {
  const { user } = useSession();
  const { t } = useI18n();
  const describe = useApiMessage();

  const [openOnly, setOpenOnly] = useState(true);
  const [mineOnly, setMineOnly] = useState(false);
  const [load, setLoad] = useState<Load>({ kind: 'loading' });
  const [capturing, setCapturing] = useState(false);

  const record = useRecordRoute<LeadDetail>({
    area: '/leads',
    load: (leadId, signal) => api<LeadDetail>(`/leads/${leadId}`, { signal }),
  });

  // Which page of the list. Reset whenever a filter changes, because page 3 of
  // one filter is not page 3 of another and landing on an empty page reads as
  // "there is nothing here".
  const [offset, setOffset] = useState(0);

  const me = user?.userId ?? null;

  // Built here rather than inside the effect so that who "mine" is only matters
  // when the filter is on. Depending on the user id unconditionally made the list
  // load twice on every visit — once before /auth/me answered and once after.
  // ORDERED BY LONGEST WAITING, and that is the fix rather than a preference.
  // The band below used to sort the loaded page oldest-first while the server
  // sent the NEWEST fifty, so the panel whose purpose is to surface neglect
  // dropped exactly the rows it was for. An enquiry list is a chase list.
  const query =
    `openOnly=${openOnly}&limit=${PageSize}&offset=${offset}&order=longestWaiting` +
    (mineOnly && me !== null ? `&assignedTo=${me}` : '');

  const find = useCallback(async (filters: string) => {
    setLoad({ kind: 'loading' });

    try {
      setLoad({
        kind: 'ready',
        page: await api<Page<LeadSummary>>(`/leads?${filters}`),
      });
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 403) {
        setLoad({ kind: 'denied' });
        return;
      }

      setLoad({ kind: 'failed', message: describe(failure) });
    }
  }, [describe]);

  useEffect(() => {
    void find(query);
  }, [find, query]);

  return (
    <>
      <header className="page__head">
        <h1>{t('leads.title')}</h1>

        <div className="filter">
          <label htmlFor="open-leads">{t('leads.show')}</label>
          <select
            id="open-leads"
            value={openOnly ? 'open' : 'all'}
            onChange={(e) => { setOpenOnly(e.target.value === 'open'); setOffset(0); }}
          >
            <option value="open">{t('leads.stillChasing')}</option>
            <option value="all">{t('leads.everything')}</option>
          </select>

          <label htmlFor="mine-only" className="check">
            <input
              id="mine-only"
              type="checkbox"
              checked={mineOnly}
              onChange={(e) => { setMineOnly(e.target.checked); setOffset(0); }}
            />
            {t('leads.onlyMine')}
          </label>
        </div>
      </header>

      {capturing ? (
        <CaptureLead
          onCaptured={async (lead) => {
            setCapturing(false);
            // The server has just handed back the whole enquiry, so it is shown
            // without a second request for what is already here.
            record.openWith(lead.id, lead);
            await find(query);
          }}
          onCancel={() => setCapturing(false)}
        />
      ) : (
        <div className="actions actions--lead">
          <button type="button" className="primary" onClick={() => setCapturing(true)}>
            {t('leads.take')}
          </button>
        </div>
      )}

      <Untouched load={load} onOpen={record.open} />

      <RecordBandStatus route={record} />

      {record.state.kind !== 'open' ? null : (
        <LeadPanel
          lead={record.state.record}
          me={me}
          onChanged={async (updated) => {
            record.refresh(updated);
            await find(query);
          }}
          onClose={record.close}
        />
      )}

      <Body
        load={load}
        me={me}
        onRetry={() => void find(query)}
        onOpen={record.open}
        onPage={setOffset}
      />
    </>
  );
}

/**
 * ADR-020's signal band: enquiries with nobody's name on them.
 *
 * An unassigned enquiry is the one that rots. Everything else on this screen has
 * an owner who will be asked about it; this has nobody, so it goes stale in
 * silence and the dealership finds out when the customer buys elsewhere.
 *
 * Drawn entirely from the list already loaded — no extra request, per ADR-020 —
 * and **rendered only when there is something in it**. A permanent "0 need
 * attention" panel trains people to stop reading the one spot they must not stop
 * reading.
 *
 * It reflects the filters above it, which is deliberate: with "only mine" ticked
 * the server never sends unassigned enquiries, so the band is empty and correct.
 * Showing work excluded by the reader's own filter would be a different bug.
 */
function Untouched({
  load, onOpen,
}: {
  load: Load;
  onOpen: (leadId: string) => void;
}) {
  const { t } = useI18n();

  if (load.kind !== 'ready') {
    return null;
  }

  // Oldest first: the one that has been ignored longest is the one to call.
  const waiting = load.page.rows
    .filter((lead) => lead.assignedTo === null && lead.status !== 'Won' && lead.status !== 'Lost')
    .sort((a, b) => b.daysOpen - a.daysOpen);

  if (waiting.length === 0) {
    return null;
  }

  return (
    <section className="panel panel--signal">
      <h2>{t('leads.untouchedTitle')}</h2>
      <p className="note">{t('leads.untouchedNote', { count: waiting.length })}</p>

      <ul className="calls" aria-label={t('leads.untouchedTitle')}>
        {waiting.map((lead) => (
          <li key={lead.id}>
            <button type="button" className="link" onClick={() => onOpen(lead.id)}>
              {lead.customerName}
            </button>{' '}
            <span className="muted">
              {lead.vehicleOfInterest ?? t('leads.noParticularCar')}
              {' · '}
              {t('leads.waitingDays', { count: lead.daysOpen })}
            </span>
          </li>
        ))}
      </ul>
    </section>
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
  const { t, format } = useI18n();
  const label = useEnumLabel();
  const describe = useApiMessage();

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
      setError(describe(failure));
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
        <CloseButton onClick={onClose} />
      </header>

      <p className="muted">
        <span className={`chip chip--${lead.status.toLowerCase()}`}>
          {label('leadStatus', lead.status)}
        </span>{' '}
        · {label('leadSource', lead.source)} ·{' '}
        {t('leads.cameIn', { date: format.date(lead.capturedAt) })}
      </p>

      {lead.enquiry === null ? null : <blockquote className="enquiry">{lead.enquiry}</blockquote>}

      <p className="note">
        {lead.assignedToUserId === null
          ? t('leads.unclaimed')
          : mine
            ? t('leads.yoursToChase')
            : t('leads.theirsToChase', { name: lead.assignedTo ?? t('leads.somebodyElse') })}
      </p>

      <div className="actions">
        {mine ? (
          <button type="button" disabled={busy} onClick={() => void assign(null)}>
            {t('leads.putBack')}
          </button>
        ) : (
          <button type="button" disabled={busy || me === null} onClick={() => void assign(me)}>
            {lead.assignedToUserId === null ? t('leads.iWillChase') : t('leads.takeItOver')}
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
            {t('leads.buildTheDeal')}
          </button>
        </div>
      ) : null}

      <h3>{t('leads.whatHappened')}</h3>
      <ol className="history" aria-label={t('leads.whatHappened')}>
        {[...lead.history].reverse().map((entry, index) => (
          <li key={`${entry.toStatus}-${entry.occurredAt}-${index}`}>
            <span className="strong">{label('leadStatus', entry.toStatus)}</span>{' '}
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
  const { t } = useI18n();
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
      <label htmlFor="hand-to">{t('leads.handTo')}</label>
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
        <option value="">{t('leads.chooseColleague')}</option>
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
  const { t } = useI18n();

  if (lead.availableMoves.length === 0) {
    return <p className="note">{t('leads.finished')}</p>;
  }

  return (
    <>
      <label htmlFor="lead-note">{t('leads.note')}</label>
      <input
        id="lead-note"
        value={note}
        onChange={(e) => onNote(e.target.value)}
        placeholder={t('leads.notePlaceholder')}
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
            {t(moveKey(lead.status, status))}
          </button>
        ))}
      </div>

      {lead.status === 'Lost' ? <p className="note">{t('leads.reopenedHere')}</p> : null}
    </>
  );
}

/**
 * The move as a person would say it, given where the lead is now.
 *
 * A key rather than a sentence, and still keyed off BOTH ends of the transition:
 * moving to Working reads "start chasing" from New and "reopen it" from Lost,
 * which is the one place the wording depends on where you came from. The set of
 * moves offered still comes from the server; this only names them.
 */
function moveKey(from: LeadStatus, to: LeadStatus): MessageKey {
  switch (to) {
    case 'Working':
      return from === 'Lost' ? 'leads.moveReopen' : 'leads.moveStartChasing';
    case 'Appointment':
      return 'leads.moveAppointment';
    case 'Won':
      return 'leads.moveWon';
    default:
      return 'leads.moveLost';
  }
}

function Body({
  load, me, onRetry, onOpen, onPage,
}: {
  load: Load;
  me: string | null;
  onRetry: () => void;
  onOpen: (id: string) => void;
  onPage: (offset: number) => void;
}) {
  const { t } = useI18n();

  switch (load.kind) {
    case 'loading':
      return (
        <p className="state" aria-live="polite">
          {t('leads.loading')}
        </p>
      );

    case 'denied':
      return (
        <p className="state" role="alert">
          {t('leads.denied')}
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
      return load.page.total === 0 ? (
        <p className="state">{t('leads.empty')}</p>
      ) : (
        <LeadTable page={load.page} me={me} onOpen={onOpen} onPage={onPage} />
      );
  }
}

function LeadTable({
  page, me, onOpen, onPage,
}: {
  page: Page<LeadSummary>;
  me: string | null;
  onOpen: (id: string) => void;
  onPage: (offset: number) => void;
}) {
  const { t, format } = useI18n();
  const label = useEnumLabel();

  return (
    <ListTable
      page={page}
      onPage={onPage}
      columns={
        <>
          <th scope="col">{t('leads.colCustomer')}</th>
          <th scope="col">{t('leads.colAskedAbout')}</th>
          <th scope="col">{t('leads.colCameFrom')}</th>
          <th scope="col" className="num">
            {t('leads.colDays')}
          </th>
          <th scope="col">{t('leads.colChasedBy')}</th>
          <th scope="col">{t('leads.colStage')}</th>
        </>
      }
      row={(lead) => (
        <tr key={lead.id}>
          <td>
            <button type="button" className="link" onClick={() => onOpen(lead.id)}>
              {lead.customerName}
            </button>
          </td>
          <td>{lead.vehicleOfInterest ?? t('leads.nothingSpecific')}</td>
          <td>{label('leadSource', lead.source)}</td>
          <td className="num">{format.number(lead.daysOpen)}</td>
          <td>
            {lead.assignedToUserId === null
              ? t('leads.nobodyYet')
              : me !== null && lead.assignedToUserId === me
                ? t('leads.you')
                : (lead.assignedTo ?? t('leads.somebodyElse'))}
          </td>
          <td>
            <span className={`chip chip--${lead.status.toLowerCase()}`}>
              {label('leadStatus', lead.status)}
            </span>
          </td>
        </tr>
      )}
    />
  );
}
