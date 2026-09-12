// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   CustomersPage.test — searching, and the duplicate check that stands in front
//   of every new customer.
//
// Usage:
//   npm test
//
// Coding Instructions:
//   The one worth guarding hardest is that **adding somebody always looks
//   for them first**. Two records for the same person splits their service
//   history and their deals, and nobody notices until it matters. If that
//   check is ever removed as friction, these tests are what says so.

import { render, screen, waitFor, within } from '../../test/render';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { CustomersPage } from './CustomersPage';
import { apiCalls, mockApi, mockApiUnreachable, page } from '../../test/setup';
import type { CustomerSummary } from '../../shared/contracts';

const ada: CustomerSummary = {
  id: '11111111-1111-1111-1111-111111111111',
  displayName: 'Ada Lovelace',
  kind: 'Person',
  primaryEmail: 'ada@example.test',
  primaryPhone: '5550102030',
};

const garage: CustomerSummary = {
  id: '22222222-2222-2222-2222-222222222222',
  displayName: 'Bob’s Garage',
  kind: 'Business',
  primaryEmail: null,
  primaryPhone: null,
};

async function fillNewCustomer(lastName: string) {
  await userEvent.click(screen.getByRole('button', { name: 'Add a customer' }));
  await userEvent.type(screen.getByLabelText('Last name'), lastName);
}

describe('finding customers', () => {
  it('lists who is already here', async () => {
    mockApi({ '/customers': { ok: true, body: page([ada, garage]) } });
    render(<CustomersPage />);

    expect(await screen.findByText('Ada Lovelace')).toBeVisible();
    expect(screen.getByText('Bob’s Garage')).toBeVisible();
    expect(screen.getByText('ada@example.test')).toBeVisible();
  });

  it('asks the server to do the matching', async () => {
    mockApi({ '/customers': { ok: true, body: page([ada]) } });
    render(<CustomersPage />);
    await screen.findByText('Ada Lovelace');

    // No Enter. The list follows the box.
    await userEvent.type(screen.getByLabelText('Find someone'), 'lovelace');

    // The server is the only party that knows what this caller may see, so the
    // filtering cannot happen in the browser.
    await waitFor(() =>
      expect(apiCalls().some((c) => c.path.includes('search=lovelace'))).toBe(true),
    );
  });

  it('collapses a burst of typing into one search', async () => {
    mockApi({ '/customers': { ok: true, body: page([ada]) } });
    render(<CustomersPage />);
    await screen.findByText('Ada Lovelace');

    await userEvent.type(screen.getByLabelText('Find someone'), 'lovelace');

    await waitFor(() =>
      expect(apiCalls().some((c) => c.path.includes('search=lovelace'))).toBe(true),
    );

    // Eight characters must not be eight searches. The first call is the
    // unfiltered list on mount; anything beyond one more means the debounce is
    // not doing its job.
    const searches = apiCalls().filter((c) => c.path.includes('search='));
    expect(searches.length).toBeLessThanOrEqual(2);
  });

  it('never shows results for a query the box no longer holds', async () => {
    // The race instant search exists to avoid: a slow answer for an early
    // keystroke landing AFTER the right answer and overwriting it. Debouncing
    // alone only makes this rarer, which is worse than leaving it obvious —
    // so the superseded request is aborted, and an aborted request cannot win.
    const grace = { id: 'c9', displayName: 'Grace Hopper', kind: 'Person' as const,
      primaryEmail: null, primaryPhone: null };

    mockApi({
      // Slow first, fast second. Without cancellation the stale reply lands last.
      '/customers?search=': [
        { ok: true, body: page([ada]), delayMs: 400 },
        { ok: true, body: page([grace]) },
      ],
      '/customers': { ok: true, body: page([ada]) },
    });

    render(<CustomersPage />);
    await screen.findByText('Ada Lovelace');

    const box = screen.getByLabelText('Find someone');

    // The pause matters: without it the debounce collapses both bursts into one
    // request and there is no race to lose. This waits just past the debounce
    // so the slow search for "a" is genuinely in flight before the next
    // keystroke supersedes it.
    await userEvent.type(box, 'a');
    await new Promise((resolve) => setTimeout(resolve, 300));

    await userEvent.type(box, 'grace');

    await waitFor(() => expect(screen.getByText('Grace Hopper')).toBeVisible());

    // Long enough for the abandoned reply to have arrived if it were going to.
    await new Promise((resolve) => setTimeout(resolve, 600));

    expect(screen.getByText('Grace Hopper')).toBeVisible();
    expect(screen.queryByText('Ada Lovelace')).not.toBeInTheDocument();
  });

  it('says nobody matches rather than showing an empty table', async () => {
    mockApi({ '/customers': { ok: true, body: page([]) } });
    render(<CustomersPage />);

    expect(await screen.findByText('Nobody matches that.')).toBeVisible();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('explains a refusal in words somebody can act on', async () => {
    mockApi({ '/customers': { ok: false, status: 403, code: 'customers.forbidden', detail: 'No.' } });
    render(<CustomersPage />);

    expect(await screen.findByRole('alert')).toHaveTextContent(/do not have access to customer records/i);
  });

  it('offers a retry when the server is unreachable', async () => {
    mockApiUnreachable();
    render(<CustomersPage />);

    expect(await screen.findByRole('alert')).toHaveTextContent(/Could not reach the server/);
    expect(screen.getByRole('button', { name: 'Try again' })).toBeVisible();
  });

  it('says which people these are out of how many', async () => {
    const many = Array.from({ length: 100 }, (_, i) => ({
      ...ada,
      id: `${i}`.padStart(8, '0') + '-1111-1111-1111-111111111111',
      displayName: `Person ${i}`,
    }));

    mockApi({ '/customers': { ok: true, body: page(many, { total: 2140, limit: 100 }) } });
    render(<CustomersPage />);

    // Not "the first 100, there may be more", which was true and useless to
    // somebody trying to find out how many customers they have.
    expect(await screen.findAllByText('Showing 1–100 of 2,140.')).toHaveLength(2);
  });
});

describe('adding a customer', () => {
  it('looks for an existing record before creating one', async () => {
    mockApi({ '/customers': { ok: true, body: page([ada]) } });
    render(<CustomersPage />);
    await screen.findByText('Ada Lovelace');

    await fillNewCustomer('Lovelace');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));

    // Found somebody, so nothing was created — the person has to decide.
    expect(await screen.findByText(/Somebody like this is already here/)).toBeVisible();
    expect(apiCalls().some((c) => c.init?.method === 'POST')).toBe(false);
  });

  it('shows who it found, with enough to recognise them', async () => {
    mockApi({ '/customers': { ok: true, body: page([ada]) } });
    render(<CustomersPage />);
    await screen.findByText('Ada Lovelace');

    await fillNewCustomer('Lovelace');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));

    const matches = await screen.findByRole('list');
    expect(within(matches).getByText('Ada Lovelace')).toBeVisible();
    expect(within(matches).getByText(/ada@example.test/)).toBeVisible();
  });

  it('creates nobody when the person says it is one of these', async () => {
    mockApi({ '/customers': { ok: true, body: page([ada]) } });
    render(<CustomersPage />);
    await screen.findByText('Ada Lovelace');

    await fillNewCustomer('Lovelace');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));
    await screen.findByText(/Somebody like this is already here/);

    await userEvent.click(screen.getByRole('button', { name: 'One of these is them' }));

    expect(apiCalls().some((c) => c.init?.method === 'POST')).toBe(false);
    expect(screen.getByRole('button', { name: 'Add a customer' })).toBeVisible();
  });

  it('lets somebody add anyway, because two people do share a name', async () => {
    mockApi({
      '/customers': [
        { ok: true, body: page([ada]) },
        { ok: true, body: page([ada]) },
        { ok: true, body: { id: 'new', displayName: 'Grace Lovelace' } },
        { ok: true, body: page([ada]) },
      ],
    });

    render(<CustomersPage />);
    await screen.findByText('Ada Lovelace');

    await fillNewCustomer('Lovelace');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));
    await screen.findByText(/Somebody like this is already here/);

    await userEvent.click(screen.getByRole('button', { name: /None of these/ }));

    await waitFor(() =>
      expect(apiCalls().some((c) => c.init?.method === 'POST')).toBe(true),
    );
  });

  it('creates directly when nobody matches', async () => {
    mockApi({
      '/customers': [
        { ok: true, body: page([]) },
        { ok: true, body: page([]) },
        { ok: true, body: { id: 'new', displayName: 'Nobody Likethis' } },
        { ok: true, body: page([]) },
      ],
    });

    render(<CustomersPage />);
    await screen.findByText('Nobody matches that.');

    await fillNewCustomer('Likethis');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));

    // No duplicates, so no interruption — the check must not become a nuisance
    // for the ordinary case.
    await waitFor(() =>
      expect(apiCalls().some((c) => c.init?.method === 'POST')).toBe(true),
    );
    expect(screen.queryByText(/Somebody like this is already here/)).not.toBeInTheDocument();
  });

  it('checks the phone and the email too, not only the name', async () => {
    mockApi({ '/customers': { ok: true, body: page([]) } });
    render(<CustomersPage />);
    await screen.findByText('Nobody matches that.');

    await userEvent.click(screen.getByRole('button', { name: 'Add a customer' }));
    await userEvent.type(screen.getByLabelText('Last name'), 'Smith');
    await userEvent.type(screen.getByLabelText('Email'), 'ada@example.test');
    await userEvent.type(screen.getByLabelText('Phone'), '5550102030');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));

    // A duplicate usually differs in one field: the phone matches but the name
    // is spelled differently, or the surname matches but the email is a work one.
    await waitFor(() => {
      const searched = apiCalls().map((c) => c.path);
      expect(searched.some((p) => p.includes('Smith'))).toBe(true);
      expect(searched.some((p) => p.includes('ada%40example.test'))).toBe(true);
      expect(searched.some((p) => p.includes('5550102030'))).toBe(true);
    });
  });

  it('will not submit without a name', async () => {
    mockApi({ '/customers': { ok: true, body: page([]) } });
    render(<CustomersPage />);
    await screen.findByText('Nobody matches that.');

    await userEvent.click(screen.getByRole('button', { name: 'Add a customer' }));

    expect(screen.getByRole('button', { name: 'Add' })).toBeDisabled();
  });

  it('asks for a business name rather than a last name for a business', async () => {
    mockApi({ '/customers': { ok: true, body: page([]) } });
    render(<CustomersPage />);
    await screen.findByText('Nobody matches that.');

    await userEvent.click(screen.getByRole('button', { name: 'Add a customer' }));
    await userEvent.selectOptions(screen.getByLabelText('Person or business'), 'Business');

    expect(screen.getByLabelText('Business name')).toBeVisible();
    // A business has no first name, and asking for one invites junk.
    expect(screen.queryByLabelText('First name')).not.toBeInTheDocument();
  });

  it('surfaces the server’s own refusal', async () => {
    mockApi({
      '/customers': [
        { ok: true, body: page([]) },
        { ok: true, body: page([]) },
        { ok: false, status: 403, code: 'customers.forbidden', detail: 'You cannot add customers.' },
      ],
    });

    render(<CustomersPage />);
    await screen.findByText('Nobody matches that.');

    await fillNewCustomer('Refused');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));

    expect(await screen.findByText('You cannot add customers.')).toBeVisible();
  });
});
