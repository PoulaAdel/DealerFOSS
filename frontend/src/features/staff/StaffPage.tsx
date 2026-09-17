// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   StaffPage — who works here, and what each of them may reach.
//
// Usage:
//   Reachable at /staff. Selecting somebody opens what they hold.
//
// Coding Instructions:
//   Three things here are deliberate.
//
//   (1) The enrolment code is shown ONCE, in a panel that says so. There is
//   no "show it again" — the server keeps only a hash, so there is nothing to
//   show. A screen that implied otherwise would send somebody looking for a
//   button that cannot exist.
//
//   (2) Adding a starter does not ask for a password, because nobody should
//   ever type a password on somebody else's behalf. The account arrives
//   unable to sign in, and the code is what fixes that.
//
//   (3) Whether a grant is allowed is the server's answer, not a prediction
//   made here. A rooftop-scoped manager sees the organization-wide option and
//   is refused it on the way in — showing the refusal is honest, and hiding
//   the option would make the rule invisible.

import { useCallback, useEffect, useState } from 'react';
import { ApiError, api, post, remove } from '../../shared/api';
import { CloseButton } from '../../shared/CloseButton';
import { useI18n } from '../../shared/i18n';
import { useApiMessage } from '../../shared/i18n/apiMessage';
import type { RooftopSummary, StaffEnrolmentCode, StaffMember, StaffRole } from '../../shared/contracts';

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; people: StaffMember[] }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

export function StaffPage() {
  const { t } = useI18n();
  const describe = useApiMessage();

  const [load, setLoad] = useState<Load>({ kind: 'loading' });
  const [roles, setRoles] = useState<StaffRole[]>([]);
  const [rooftops, setRooftops] = useState<RooftopSummary[]>([]);
  const [selected, setSelected] = useState<StaffMember | null>(null);
  const [adding, setAdding] = useState(false);
  const [issued, setIssued] = useState<{ person: string; code: StaffEnrolmentCode } | null>(null);

  const find = useCallback(async () => {
    setLoad({ kind: 'loading' });

    try {
      setLoad({ kind: 'ready', people: await api<StaffMember[]>('/staff') });
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
    void find();
  }, [find]);

  // Roles and rooftops are reference data for the grant form. A failure here is
  // not a failure of the page — the list still reads.
  useEffect(() => {
    void (async () => {
      try {
        setRoles(await api<StaffRole[]>('/staff/roles'));
      } catch {
        setRoles([]);
      }

      try {
        const organization = await api<{ legalEntities: { rooftops: RooftopSummary[] }[] }>('/organization');
        setRooftops(organization.legalEntities.flatMap((entity) => entity.rooftops));
      } catch {
        setRooftops([]);
      }
    })();
  }, []);

  async function refresh(userId: string) {
    await find();
    setSelected(await api<StaffMember>(`/staff/${userId}`));
  }

  if (load.kind === 'loading') {
    return <p>{t('staff.loading')}</p>;
  }

  if (load.kind === 'denied') {
    return (
      <section className="page">
        <h1>{t('staff.title')}</h1>
        <p className="note">{t('staff.denied')}</p>
      </section>
    );
  }

  if (load.kind === 'failed') {
    return (
      <section className="page">
        <h1>{t('staff.title')}</h1>
        <p className="error">{load.message}</p>
        <button type="button" onClick={() => void find()}>
          {t('common.retry')}
        </button>
      </section>
    );
  }

  return (
    <section className="page">
      <header className="page__head">
        <h1>{t('staff.title')}</h1>
        <button type="button" className="primary" onClick={() => setAdding(true)}>
          {t('staff.add')}
        </button>
      </header>

      {issued === null ? null : (
        <EnrolmentCode issued={issued} onDismiss={() => setIssued(null)} />
      )}

      {adding ? (
        <AddStarter
          onCancel={() => setAdding(false)}
          onAdded={async (person, code) => {
            setAdding(false);
            setIssued({ person: person.displayName, code });
            await find();
          }}
        />
      ) : null}

      {selected === null ? null : (
        <Person
          person={selected}
          roles={roles}
          rooftops={rooftops}
          onClose={() => setSelected(null)}
          onChanged={() => void refresh(selected.id)}
          onIssued={(code) => setIssued({ person: selected.displayName, code })}
        />
      )}

      {load.people.length === 0 ? (
        <p className="note">{t('staff.empty')}</p>
      ) : (
        // Five columns do not fit a phone. `div.scroll` makes the table scroll
        // inside its own box instead of pushing the whole page sideways — the
        // same wrapper every other wide table here uses. Its absence is
        // invisible to jsdom, which has no layout engine, so the test asserts
        // the wrapper is present rather than measuring anything.
        <div className="scroll">
          <table className="table">
            <caption className="visually-hidden">{t('staff.caption')}</caption>
            <thead>
              <tr>
                <th scope="col">{t('staff.colName')}</th>
                <th scope="col">{t('staff.colEmail')}</th>
                <th scope="col">{t('staff.colHolds')}</th>
                <th scope="col">{t('staff.colSecondFactor')}</th>
                <th scope="col">{t('staff.colState')}</th>
              </tr>
            </thead>
            <tbody>
              {load.people.map((person) => (
                <tr key={person.id}>
                  <td>
                    <button type="button" className="link" onClick={() => setSelected(person)}>
                      {person.displayName}
                    </button>
                  </td>
                  <td>{person.email}</td>
                  <td>
                    {person.assignments.length === 0
                      ? t('staff.holdsNothing')
                      : person.assignments.map((a) => a.roleName).join(', ')}
                  </td>
                  <td>{person.hasSecondFactor ? t('common.yes') : t('common.no')}</td>
                  <td>
                    <State person={person} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

/**
 * A starter and a leaver both read "cannot sign in", and they need opposite
 * actions — so they are never shown the same way.
 */
function State({ person }: { person: StaffMember }) {
  const { t } = useI18n();

  if (!person.isActive) {
    return <span className="chip chip--lost">{t('staff.stateStopped')}</span>;
  }

  if (person.awaitingEnrolment) {
    return <span className="chip chip--new">{t('staff.stateAwaiting')}</span>;
  }

  return <span className="chip chip--won">{t('staff.stateWorking')}</span>;
}

function EnrolmentCode({
  issued,
  onDismiss,
}: {
  issued: { person: string; code: StaffEnrolmentCode };
  onDismiss: () => void;
}) {
  const { t, format } = useI18n();

  return (
    <section className="panel panel--code" aria-live="polite">
      <h2>{t('staff.codeFor', { name: issued.person })}</h2>
      {/* Read out character by character and typed on another screen, so it
          runs left to right whatever the page does. */}
      <p className="code" dir="ltr">
        {issued.code.code}
      </p>
      <p>{t('staff.readItOut')}</p>
      <p className="note">
        <strong>{t('staff.onlyTimeShown')}</strong>{' '}
        {t('staff.onlyTimeShownRest', { expires: format.dateTime(issued.code.expiresAt) })}
      </p>
      <button type="button" onClick={onDismiss}>
        {t('staff.passedItOn')}
      </button>
    </section>
  );
}

function AddStarter({
  onCancel,
  onAdded,
}: {
  onCancel: () => void;
  onAdded: (person: StaffMember, code: StaffEnrolmentCode) => Promise<void>;
}) {
  const { t } = useI18n();
  const describe = useApiMessage();

  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    setBusy(true);
    setError(null);

    try {
      const person = await post<StaffMember>('/staff', { email, displayName });

      // Adding somebody and giving them a way in are one act from where the
      // manager is standing, so the code is minted immediately rather than
      // leaving an account nobody can use and a second button to find.
      const code = await post<StaffEnrolmentCode>(`/staff/${person.id}/enrolment`, {});
      await onAdded(person, code);
    } catch (failure) {
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="panel">
      <h2>{t('staff.addTitle')}</h2>

      <p className="note">{t('staff.addLede')}</p>

      <div className="field">
        <label htmlFor="starter-name">{t('staff.name')}</label>
        <input
          id="starter-name"
          value={displayName}
          onChange={(event) => setDisplayName(event.target.value)}
        />
      </div>

      <div className="field">
        <label htmlFor="starter-email">{t('staff.email')}</label>
        <input
          id="starter-email"
          type="email"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
        />
      </div>

      <p className="error" aria-live="polite">
        {error ?? ''}
      </p>

      <div className="actions">
        <button
          type="button"
          className="primary"
          disabled={busy || displayName.trim() === '' || email.trim() === ''}
          onClick={() => void submit()}
        >
          {t('staff.addAndMakeCode')}
        </button>
        <button type="button" onClick={onCancel}>
          {t('common.cancel')}
        </button>
      </div>
    </section>
  );
}

function Person({
  person,
  roles,
  rooftops,
  onClose,
  onChanged,
  onIssued,
}: {
  person: StaffMember;
  roles: StaffRole[];
  rooftops: RooftopSummary[];
  onClose: () => void;
  onChanged: () => void;
  onIssued: (code: StaffEnrolmentCode) => void;
}) {
  const { t, format } = useI18n();
  const describe = useApiMessage();

  const [roleId, setRoleId] = useState('');
  const [rooftopId, setRooftopId] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function act(work: () => Promise<unknown>) {
    setBusy(true);
    setError(null);

    try {
      await work();
      onChanged();
    } catch (failure) {
      // The server knows who is asking and which rule they hit. Anything guessed
      // here would be a second, worse copy of the rule.
      setError(describe(failure));
    } finally {
      setBusy(false);
    }
  }

  const grant = roles.find((r) => r.id === roleId);

  return (
    <section className="panel panel--deal">
      <header className="page__head">
        <h2>{person.displayName}</h2>
        <CloseButton onClick={onClose} />
      </header>

      <p className="muted">
        <span dir="ltr">{person.email}</span> · <State person={person} /> ·{' '}
        {person.hasSecondFactor ? t('staff.hasSecondFactor') : t('staff.noSecondFactor')}
      </p>

      <h3>{t('staff.whatTheyHold')}</h3>
      {person.assignments.length === 0 ? (
        <p className="note">{t('staff.holdsNothingYet')}</p>
      ) : (
        <ul className="grants">
          {person.assignments.map((assignment) => (
            <li key={assignment.id}>
              <span>
                <strong>{assignment.roleName}</strong>{' '}
                {assignment.isOrganizationWide ? (
                  <span className="chip chip--warn">{t('staff.everywhere')}</span>
                ) : (
                  // The rooftop CODE is the dealership's own label for that
                  // location and is printed as they set it.
                  (rooftops.find((r) => r.id === assignment.rooftopId)?.code ??
                    t('staff.oneLocation'))
                )}
              </span>
              <button
                type="button"
                disabled={busy}
                onClick={() => void act(() => remove(`/staff/${person.id}/assignments/${assignment.id}`))}
              >
                {t('staff.takeItAway')}
              </button>
            </li>
          ))}
        </ul>
      )}

      <h3>{t('staff.giveARole')}</h3>
      <div className="field">
        <label htmlFor="grant-role">{t('staff.role')}</label>
        <select id="grant-role" value={roleId} onChange={(event) => setRoleId(event.target.value)}>
          <option value="">{t('staff.chooseRole')}</option>
          {roles.map((role) => (
            <option key={role.id} value={role.id}>
              {role.name}
            </option>
          ))}
        </select>
      </div>

      {grant === undefined ? null : (
        <p className="note">
          {/* The permission NAMES are the API's catalogue (`Deals.Approve`) and
              are shown verbatim — they are identifiers a manager matches
              against the documentation, not prose. `format.list` joins them the
              way the reader's language joins a list. */}
          {t('staff.holdingGrants', { permissions: format.list(grant.permissions) })}
          {grant.requiresSecondFactor ? t('staff.andObligesSecondFactor') : ''}
        </p>
      )}

      <div className="field">
        <label htmlFor="grant-rooftop">{t('staff.where')}</label>
        <select
          id="grant-rooftop"
          value={rooftopId}
          onChange={(event) => setRooftopId(event.target.value)}
        >
          <option value="">{t('staff.everywhereInOrg')}</option>
          {rooftops.map((rooftop) => (
            <option key={rooftop.id} value={rooftop.id}>
              {rooftop.code} — {rooftop.name}
            </option>
          ))}
        </select>
      </div>

      <p className="error" aria-live="polite">
        {error ?? ''}
      </p>

      <div className="actions">
        <button
          type="button"
          className="primary"
          disabled={busy || roleId === ''}
          onClick={() =>
            void act(() =>
              post(`/staff/${person.id}/assignments`, {
                roleId,
                rooftopId: rooftopId === '' ? null : rooftopId,
              }),
            )
          }
        >
          {t('staff.giveThem')}
        </button>

        {person.awaitingEnrolment && person.isActive ? (
          <button
            type="button"
            disabled={busy}
            onClick={() =>
              void act(async () =>
                onIssued(await post<StaffEnrolmentCode>(`/staff/${person.id}/enrolment`, {})),
              )
            }
          >
            {t('staff.makeNewCode')}
          </button>
        ) : null}

        {/* The backstop of ADR-018, and only for somebody who HAS a password —
            a starter needs the enrolment code above, which is a different act
            with different preconditions. Offered here rather than hidden behind
            a permission check in the browser: the server holds
            Staff.ResetPassword and its refusal names the permission, which is
            more useful than a button that is silently absent. */}
        {!person.awaitingEnrolment && person.isActive ? (
          <button
            type="button"
            disabled={busy}
            onClick={() =>
              void act(async () =>
                onIssued(await post<StaffEnrolmentCode>(`/staff/${person.id}/recovery`, {})),
              )
            }
          >
            {t('staff.reset')}
          </button>
        ) : null}

        <button
          type="button"
          disabled={busy}
          onClick={() =>
            void act(() => post(`/staff/${person.id}/active`, { active: !person.isActive }))
          }
        >
          {person.isActive ? t('staff.stopAccount') : t('staff.letThemBackIn')}
        </button>
      </div>

      {/* Visible on the record, not only in the audit trail: a dealership must
          be able to see that somebody handed out access without going looking
          for it (ADR-018). It disappears when the code is used or expires,
          because what matters is what is live right now. */}
      {person.recoveryIssuedAt === null ? null : (
        <p className="note note--warn" role="status">
          {t('staff.resetIssued', { when: format.dateTime(person.recoveryIssuedAt) })}
        </p>
      )}

      {person.isActive ? <p className="note">{t('staff.stoppingNote')}</p> : null}
    </section>
  );
}
