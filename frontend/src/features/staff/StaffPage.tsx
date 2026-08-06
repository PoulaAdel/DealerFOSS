// StaffPage — who works here, and what each of them may reach.
//
// Use:  reachable at /staff. Selecting somebody opens what they hold.
// Edit: three things here are deliberate.
//
//       (1) The enrolment code is shown ONCE, in a panel that says so. There is
//       no "show it again" — the server keeps only a hash, so there is nothing to
//       show. A screen that implied otherwise would send somebody looking for a
//       button that cannot exist.
//
//       (2) Adding a starter does not ask for a password, because nobody should
//       ever type a password on somebody else's behalf. The account arrives
//       unable to sign in, and the code is what fixes that.
//
//       (3) Whether a grant is allowed is the server's answer, not a prediction
//       made here. A rooftop-scoped manager sees the organization-wide option and
//       is refused it on the way in — showing the refusal is honest, and hiding
//       the option would make the rule invisible.

import { useCallback, useEffect, useState } from 'react';
import { ApiError, api, post, remove } from '../../shared/api';
import type { RooftopSummary, StaffEnrolmentCode, StaffMember, StaffRole } from '../../shared/contracts';

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; people: StaffMember[] }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

export function StaffPage() {
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
        message: failure instanceof ApiError ? failure.message : 'The staff list could not be read.',
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
    return <p>Loading the people who work here…</p>;
  }

  if (load.kind === 'denied') {
    return (
      <section className="page">
        <h1>People</h1>
        <p className="note">
          You do not have access to the staff list. Ask a manager if you need it.
        </p>
      </section>
    );
  }

  if (load.kind === 'failed') {
    return (
      <section className="page">
        <h1>People</h1>
        <p className="error">{load.message}</p>
        <button type="button" onClick={() => void find()}>
          Try again
        </button>
      </section>
    );
  }

  return (
    <section className="page">
      <header className="page__head">
        <h1>People</h1>
        <button type="button" className="primary" onClick={() => setAdding(true)}>
          Add somebody
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
        <p className="note">Nobody here yet.</p>
      ) : (
        // Five columns do not fit a phone. `div.scroll` makes the table scroll
        // inside its own box instead of pushing the whole page sideways — the
        // same wrapper every other wide table here uses. Its absence is
        // invisible to jsdom, which has no layout engine, so the test asserts
        // the wrapper is present rather than measuring anything.
        <div className="scroll">
          <table className="table">
            <caption className="visually-hidden">
              Everybody whose access reaches a location you work at.
            </caption>
            <thead>
              <tr>
                <th scope="col">Name</th>
                <th scope="col">Email</th>
                <th scope="col">Holds</th>
                <th scope="col">Second factor</th>
                <th scope="col">State</th>
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
                      ? 'Nothing yet'
                      : person.assignments.map((a) => a.roleName).join(', ')}
                  </td>
                  <td>{person.hasSecondFactor ? 'Yes' : 'No'}</td>
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
  if (!person.isActive) {
    return <span className="chip chip--lost">Stopped</span>;
  }

  if (person.awaitingEnrolment) {
    return <span className="chip chip--new">Awaiting first password</span>;
  }

  return <span className="chip chip--won">Working</span>;
}

function EnrolmentCode({
  issued,
  onDismiss,
}: {
  issued: { person: string; code: StaffEnrolmentCode };
  onDismiss: () => void;
}) {
  return (
    <section className="panel panel--code" aria-live="polite">
      <h2>Code for {issued.person}</h2>
      <p className="code">{issued.code.code}</p>
      <p>
        Read this out to them. They set their own password with it at the sign-in
        screen — nobody else ever types it, including you.
      </p>
      <p className="note">
        <strong>This is the only time it can be shown.</strong> Only a hash of it is
        stored, so it cannot be looked up again — if it goes astray, issue a new
        one, which stops this one working. It expires{' '}
        {new Date(issued.code.expiresAt).toLocaleString()}.
      </p>
      <button type="button" onClick={onDismiss}>
        I have passed it on
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
      setError(failure instanceof ApiError ? failure.message : 'That did not work.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="panel">
      <h2>Add somebody</h2>

      <p className="note">
        They will not be able to sign in until they set a password with the code
        this produces. You never see or choose their password.
      </p>

      <div className="field">
        <label htmlFor="starter-name">Name</label>
        <input
          id="starter-name"
          value={displayName}
          onChange={(event) => setDisplayName(event.target.value)}
        />
      </div>

      <div className="field">
        <label htmlFor="starter-email">Email</label>
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
          Add and make a code
        </button>
        <button type="button" onClick={onCancel}>
          Cancel
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
      setError(failure instanceof ApiError ? failure.message : 'That did not work.');
    } finally {
      setBusy(false);
    }
  }

  const grant = roles.find((r) => r.id === roleId);

  return (
    <section className="panel panel--deal">
      <header className="page__head">
        <h2>{person.displayName}</h2>
        <button type="button" onClick={onClose}>
          Close
        </button>
      </header>

      <p className="muted">
        {person.email} · <State person={person} /> ·{' '}
        {person.hasSecondFactor ? 'has a second factor' : 'no second factor'}
      </p>

      <h3>What they hold</h3>
      {person.assignments.length === 0 ? (
        <p className="note">Nothing yet, so they can sign in and see nothing.</p>
      ) : (
        <ul className="grants">
          {person.assignments.map((assignment) => (
            <li key={assignment.id}>
              <span>
                <strong>{assignment.roleName}</strong>{' '}
                {assignment.isOrganizationWide ? (
                  <span className="chip chip--warn">everywhere</span>
                ) : (
                  (rooftops.find((r) => r.id === assignment.rooftopId)?.code ?? 'one location')
                )}
              </span>
              <button
                type="button"
                disabled={busy}
                onClick={() => void act(() => remove(`/staff/${person.id}/assignments/${assignment.id}`))}
              >
                Take it away
              </button>
            </li>
          ))}
        </ul>
      )}

      <h3>Give them a role</h3>
      <div className="field">
        <label htmlFor="grant-role">Role</label>
        <select id="grant-role" value={roleId} onChange={(event) => setRoleId(event.target.value)}>
          <option value="">Choose a role</option>
          {roles.map((role) => (
            <option key={role.id} value={role.id}>
              {role.name}
            </option>
          ))}
        </select>
      </div>

      {grant === undefined ? null : (
        <p className="note">
          Holding it grants: {grant.permissions.join(', ')}
          {grant.requiresSecondFactor ? ' — and obliges them to set up a second factor.' : ''}
        </p>
      )}

      <div className="field">
        <label htmlFor="grant-rooftop">Where</label>
        <select
          id="grant-rooftop"
          value={rooftopId}
          onChange={(event) => setRooftopId(event.target.value)}
        >
          <option value="">Everywhere in the organization</option>
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
          Give them this
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
            Make a new code
          </button>
        ) : null}

        <button
          type="button"
          disabled={busy}
          onClick={() =>
            void act(() => post(`/staff/${person.id}/active`, { active: !person.isActive }))
          }
        >
          {person.isActive ? 'Stop this account' : 'Let them back in'}
        </button>
      </div>

      {person.isActive ? (
        <p className="note">
          Stopping an account ends their sessions on the very next request, and
          deletes nothing — their name still has to appear against the work they
          did.
        </p>
      ) : null}
    </section>
  );
}
