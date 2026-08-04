// CustomersPage — finding a customer, and adding one without making a second
// copy of somebody who is already here.
//
// Use:  reachable at /customers. The page a receptionist opens fifty times a day.
// Edit: the rule worth protecting is that **adding somebody goes through a
//       duplicate check first**. Two records for the same person is the failure
//       that quietly makes a dealer management system untrustworthy: their
//       service history splits, their deals sit under the wrong name, and
//       nobody notices until it matters. The check is not a warning that can be
//       clicked past without reading — it shows who it found and makes somebody
//       choose.
//
//       It is deliberately not a hard refusal. Two people genuinely do share a
//       name, and a shop that cannot record the second one will get a fake name
//       typed in instead. The screen's job is to make the duplicate obvious, not
//       to decide.

import { useCallback, useEffect, useState } from 'react';
import { ApiError, api, post } from '../../shared/api';
import type { CustomerSummary, NewCustomer } from '../../shared/contracts';

/**
 * What the server will return at most, however many are asked for. The screen
 * has to know: a full page is indistinguishable from "that is everybody", and
 * saying the wrong one puts a false number in front of somebody.
 */
const PageSize = 100;

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; customers: CustomerSummary[] }
  | { kind: 'denied' }
  | { kind: 'failed'; message: string };

/** The add form's state machine. `checking` is the duplicate look-up. */
type Adding =
  | { step: 'closed' }
  | { step: 'form' }
  | { step: 'checking' }
  | { step: 'confirm'; matches: CustomerSummary[] }
  | { step: 'saving' };

const empty: NewCustomer = { kind: 'Person', firstName: '', lastName: '', email: '', phone: '' };

export function CustomersPage() {
  const [search, setSearch] = useState('');
  const [load, setLoad] = useState<Load>({ kind: 'loading' });

  const [adding, setAdding] = useState<Adding>({ step: 'closed' });
  const [draft, setDraft] = useState<NewCustomer>(empty);
  const [error, setError] = useState<string | null>(null);

  const find = useCallback(async (term: string) => {
    setLoad({ kind: 'loading' });

    try {
      const query = term.trim() === ''
        ? `?limit=${PageSize}`
        : `?search=${encodeURIComponent(term.trim())}&limit=${PageSize}`;

      setLoad({ kind: 'ready', customers: await api<CustomerSummary[]>(`/customers${query}`) });
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 403) {
        setLoad({ kind: 'denied' });
        return;
      }

      setLoad({
        kind: 'failed',
        message: failure instanceof ApiError ? failure.message : 'Could not load customers.',
      });
    }
  }, []);

  useEffect(() => {
    void find('');
  }, [find]);

  /**
   * Looks for anybody who might already be this person before creating them.
   * Searches on each of the things that identify somebody separately, because a
   * duplicate usually differs in one of them — the phone matches but the name is
   * spelled differently, or the surname matches but they used a work email.
   */
  async function checkForDuplicates() {
    setError(null);
    setAdding({ step: 'checking' });

    const terms = [draft.lastName, draft.email, draft.phone]
      .map((t) => (t ?? '').trim())
      .filter((t) => t.length > 0);

    try {
      const found = new Map<string, CustomerSummary>();

      for (const term of terms) {
        const matches = await api<CustomerSummary[]>(
          `/customers?search=${encodeURIComponent(term)}&limit=10`,
        );

        for (const match of matches) {
          found.set(match.id, match);
        }
      }

      if (found.size === 0) {
        await save();
        return;
      }

      setAdding({ step: 'confirm', matches: [...found.values()] });
    } catch (failure) {
      setError(messageFor(failure));
      setAdding({ step: 'form' });
    }
  }

  async function save() {
    setError(null);
    setAdding({ step: 'saving' });

    try {
      await post('/customers', {
        kind: draft.kind,
        firstName: draft.kind === 'Business' ? null : draft.firstName,
        lastName: draft.lastName,
        email: draft.email === '' ? null : draft.email,
        phone: draft.phone === '' ? null : draft.phone,
      });

      setDraft(empty);
      setAdding({ step: 'closed' });
      await find(search);
    } catch (failure) {
      setError(messageFor(failure));
      setAdding({ step: 'form' });
    }
  }

  const busy = adding.step === 'checking' || adding.step === 'saving';

  return (
    <>
      <header className="page__head">
        <h1>Customers</h1>

        <form
          className="filter"
          onSubmit={(e) => {
            e.preventDefault();
            void find(search);
          }}
        >
          <label htmlFor="search">Find someone</label>
          <input
            id="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Name, phone, or email"
            // Not type="search": the browser's clear button does not fire an
            // input event in every engine, so the list could disagree with the box.
            autoComplete="off"
          />
        </form>
      </header>

      {adding.step === 'closed' ? (
        <div className="actions actions--lead">
          <button type="button" className="primary" onClick={() => setAdding({ step: 'form' })}>
            Add a customer
          </button>
        </div>
      ) : (
        <AddPanel
          adding={adding}
          draft={draft}
          error={error}
          busy={busy}
          onDraft={setDraft}
          onCheck={() => void checkForDuplicates()}
          onSaveAnyway={() => void save()}
          onCancel={() => {
            setAdding({ step: 'closed' });
            setDraft(empty);
            setError(null);
          }}
        />
      )}

      <Results load={load} onRetry={() => void find(search)} />
    </>
  );
}

function AddPanel({
  adding, draft, error, busy, onDraft, onCheck, onSaveAnyway, onCancel,
}: {
  adding: Adding;
  draft: NewCustomer;
  error: string | null;
  busy: boolean;
  onDraft: (next: NewCustomer) => void;
  onCheck: () => void;
  onSaveAnyway: () => void;
  onCancel: () => void;
}) {
  if (adding.step === 'confirm') {
    return (
      <section className="panel">
        <h2>Somebody like this is already here</h2>
        <p role="alert">
          Adding a second record for the same person splits their history — their
          service, their deals, and their contact details stop agreeing. Check
          whether one of these is them.
        </p>

        <ul className="matches">
          {adding.matches.map((match) => (
            <li key={match.id}>
              <span className="strong">{match.displayName}</span>{' '}
              <span className="muted">
                {[match.primaryEmail, match.primaryPhone].filter(Boolean).join(' · ') || 'no contact details'}
              </span>
            </li>
          ))}
        </ul>

        <div className="actions">
          <button type="button" className="primary" onClick={onCancel}>
            One of these is them
          </button>
          <button type="button" onClick={onSaveAnyway} disabled={busy}>
            {busy ? 'Adding…' : 'None of these — add anyway'}
          </button>
        </div>
      </section>
    );
  }

  return (
    <section className="panel">
      <h2>Add a customer</h2>

      <form
        onSubmit={(e) => {
          e.preventDefault();
          onCheck();
        }}
        noValidate
      >
        <label htmlFor="customer-kind">Person or business</label>
        <select
          id="customer-kind"
          value={draft.kind}
          onChange={(e) => onDraft({ ...draft, kind: e.target.value as NewCustomer['kind'] })}
        >
          <option value="Person">Person</option>
          <option value="Business">Business</option>
        </select>

        {draft.kind === 'Person' ? (
          <>
            <label htmlFor="first-name">First name</label>
            <input
              id="first-name"
              value={draft.firstName ?? ''}
              onChange={(e) => onDraft({ ...draft, firstName: e.target.value })}
            />
          </>
        ) : null}

        <label htmlFor="last-name">
          {draft.kind === 'Business' ? 'Business name' : 'Last name'}
        </label>
        <input
          id="last-name"
          value={draft.lastName}
          onChange={(e) => onDraft({ ...draft, lastName: e.target.value })}
          required
        />

        <label htmlFor="email">Email</label>
        <input
          id="email"
          type="email"
          value={draft.email ?? ''}
          onChange={(e) => onDraft({ ...draft, email: e.target.value })}
        />

        <label htmlFor="phone">Phone</label>
        <input
          id="phone"
          value={draft.phone ?? ''}
          onChange={(e) => onDraft({ ...draft, phone: e.target.value })}
        />

        <p className="error" aria-live="polite">
          {error ?? ''}
        </p>

        <div className="actions">
          <button
            type="submit"
            className="primary"
            disabled={busy || draft.lastName.trim() === ''}
          >
            {adding.step === 'checking'
              ? 'Checking for duplicates…'
              : adding.step === 'saving'
                ? 'Adding…'
                : 'Add'}
          </button>
          <button type="button" onClick={onCancel} disabled={busy}>
            Cancel
          </button>
        </div>
      </form>
    </section>
  );
}

function Results({ load, onRetry }: { load: Load; onRetry: () => void }) {
  switch (load.kind) {
    case 'loading':
      return (
        <p className="state" aria-live="polite">
          Looking…
        </p>
      );

    case 'denied':
      return (
        <p className="state" role="alert">
          You do not have access to customer records. Ask a manager if you think
          that is wrong.
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
      return load.customers.length === 0 ? (
        <p className="state">Nobody matches that.</p>
      ) : (
        <CustomerTable customers={load.customers} />
      );
  }
}

function CustomerTable({ customers }: { customers: CustomerSummary[] }) {
  // A full page probably means there are more, and we cannot know how many.
  const capped = customers.length >= PageSize;

  return (
    <div className="scroll">
      <table>
        <caption className="visually-hidden">
          {capped
            ? `The first ${customers.length} customers. There may be more.`
            : `${customers.length} customers`}
        </caption>
        <thead>
          <tr>
            <th scope="col">Name</th>
            <th scope="col">Kind</th>
            <th scope="col">Email</th>
            <th scope="col">Phone</th>
          </tr>
        </thead>
        <tbody>
          {customers.map((customer) => (
            <tr key={customer.id}>
              <td>{customer.displayName}</td>
              <td>
                <span className={`chip chip--${customer.kind.toLowerCase()}`}>{customer.kind}</span>
              </td>
              <td>{customer.primaryEmail ?? <span className="muted">—</span>}</td>
              <td className="mono">{customer.primaryPhone ?? <span className="muted">—</span>}</td>
            </tr>
          ))}
        </tbody>
      </table>

      {capped ? (
        <p className="note note--footer">
          Showing the first {customers.length}. There may be more — narrow the
          search until paging exists.
        </p>
      ) : null}
    </div>
  );
}

function messageFor(failure: unknown): string {
  return failure instanceof ApiError ? failure.message : 'Something went wrong. Try again.';
}
