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

import { renderAtRecordRoute, screen, waitFor, within } from '../../test/render';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { CustomersPage } from './CustomersPage';
import { apiCalls, mockApi, mockApiUnreachable, page } from '../../test/setup';
import type { CustomerSummary } from '../../shared/contracts';

/**
 * At the screen's real route. The optional `:id` segment carries the open
 * customer, so a bare mount has no route to navigate within and clicking a row
 * would do nothing.
 */
function renderCustomers(at = '/customers') {
  return renderAtRecordRoute('/customers', <CustomersPage />, at);
}

const ada: CustomerSummary = {
  id: '11111111-1111-1111-1111-111111111111',
  displayName: 'Ada Lovelace',
  kind: 'Person',
  primaryEmail: 'ada@example.test',
  primaryPhone: '5550102030',
  removedAtProviderOn: null,
};

const garage: CustomerSummary = {
  id: '22222222-2222-2222-2222-222222222222',
  displayName: 'Bob’s Garage',
  kind: 'Business',
  primaryEmail: null,
  primaryPhone: null,
  removedAtProviderOn: null,
};

async function fillNewCustomer(lastName: string) {
  await userEvent.click(screen.getByRole('button', { name: 'Add a customer' }));
  await userEvent.type(screen.getByLabelText('Last name'), lastName);
}

describe('finding customers', () => {
  it('reaches Next in three Tab stops with a full page of 100 customers', async () => {
    const user = userEvent.setup();
    const rows = Array.from({ length: 100 }, (_, index) => ({
      ...ada, id: String(index), displayName: `Customer ${index + 1}`,
    }));
    mockApi({ '/customers': { ok: true, body: { rows, total: 200, offset: 0, limit: 100 } } });
    renderCustomers();
    const next = await screen.findByRole('button', { name: 'Next' });

    // Search, Add, Next. Count the route's controls, not the shell's changing
    // navigation; the real browser walk records the whole-page number too.
    let stops = 0;
    while (document.activeElement !== next && stops < 110) {
      await user.tab();
      stops += 1;
    }
    expect(stops).toBe(3);
    await user.tab();
    expect(screen.getByRole('button', { name: 'Customer 1' })).toHaveFocus();
  });

  it('lists who is already here', async () => {
    mockApi({ '/customers': { ok: true, body: page([ada, garage]) } });
    renderCustomers();

    expect(await screen.findByText('Ada Lovelace')).toBeVisible();
    expect(screen.getByText('Bob’s Garage')).toBeVisible();
    expect(screen.getByText('ada@example.test')).toBeVisible();
  });

  it('asks the server to do the matching', async () => {
    mockApi({ '/customers': { ok: true, body: page([ada]) } });
    renderCustomers();
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
    renderCustomers();
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

    renderCustomers();
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
    renderCustomers();

    expect(await screen.findByText('Nobody matches that.')).toBeVisible();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('explains a refusal in words somebody can act on', async () => {
    mockApi({ '/customers': { ok: false, status: 403, code: 'customers.forbidden', detail: 'No.' } });
    renderCustomers();

    expect(await screen.findByRole('alert')).toHaveTextContent(/do not have access to customer records/i);
  });

  it('offers a retry when the server is unreachable', async () => {
    mockApiUnreachable();
    renderCustomers();

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
    renderCustomers();

    // Not "the first 100, there may be more", which was true and useless to
    // somebody trying to find out how many customers they have.
    expect(await screen.findAllByText('Showing 1–100 of 2,140.')).toHaveLength(2);
  });
});

describe('adding a customer', () => {
  it('looks for an existing record before creating one', async () => {
    mockApi({ '/customers': { ok: true, body: page([ada]) } });
    renderCustomers();
    await screen.findByText('Ada Lovelace');

    await fillNewCustomer('Lovelace');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));

    // Found somebody, so nothing was created — the person has to decide.
    expect(await screen.findByText(/Somebody like this is already here/)).toBeVisible();
    expect(apiCalls().some((c) => c.init?.method === 'POST')).toBe(false);
  });

  it('shows who it found, with enough to recognise them', async () => {
    mockApi({ '/customers': { ok: true, body: page([ada]) } });
    renderCustomers();
    await screen.findByText('Ada Lovelace');

    await fillNewCustomer('Lovelace');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));

    const matches = await screen.findByRole('list');
    expect(within(matches).getByText('Ada Lovelace')).toBeVisible();
    expect(within(matches).getByText(/ada@example.test/)).toBeVisible();
  });

  it('creates nobody when the person says it is one of these', async () => {
    mockApi({ '/customers': { ok: true, body: page([ada]) } });
    renderCustomers();
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

    renderCustomers();
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

    renderCustomers();
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
    renderCustomers();
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
    renderCustomers();
    await screen.findByText('Nobody matches that.');

    await userEvent.click(screen.getByRole('button', { name: 'Add a customer' }));

    expect(screen.getByRole('button', { name: 'Add' })).toBeDisabled();
  });

  it('asks for a business name rather than a last name for a business', async () => {
    mockApi({ '/customers': { ok: true, body: page([]) } });
    renderCustomers();
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

    renderCustomers();
    await screen.findByText('Nobody matches that.');

    await fillNewCustomer('Refused');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));

    expect(await screen.findByText('You cannot add customers.')).toBeVisible();
  });
});

/**
 * A customer with an address of their own (2026-09-16).
 *
 * The results stay on screen with the search term intact — that part is
 * unchanged and is the whole reason the band was a band. What is new is that
 * `/customers/:id` says which of them is open, so "I will send you the link"
 * replaces "search for Lovelace, no, the other one" on a telephone call.
 */
describe('a customer reached by their own address', () => {
  const detail = {
    id: ada.id,
    displayName: 'Ada Lovelace',
    kind: 'Person' as const,
    firstName: 'Ada',
    lastName: 'Lovelace',
    homeRooftopId: null,
    address: null,
    contactPoints: [],
    externalReference: null,
  };

  it('arrives open when the address names the customer', async () => {
    mockApi({
      '/customers': { ok: true, body: page([ada]) },
      [`/customers/${ada.id}`]: { ok: true, body: detail },
    });
    renderCustomers(`/customers/${ada.id}`);

    expect(await screen.findByRole('region', { name: 'Ada Lovelace' })).toBeVisible();
  });

  it('puts the customer in the address when a row is opened', async () => {
    mockApi({
      '/customers': { ok: true, body: page([ada]) },
      [`/customers/${ada.id}`]: { ok: true, body: detail },
    });
    const { address } = renderCustomers();

    await userEvent.click(await screen.findByRole('button', { name: 'Ada Lovelace' }));
    await screen.findByRole('region', { name: 'Ada Lovelace' });

    expect(address()).toBe(`/customers/${ada.id}`);
  });

  it('leaves the search term alone, because the box is not worth a history entry', async () => {
    // Typing is not navigation. A keystroke per history entry would turn the
    // back button into an undo for typing, which is not what anybody presses it
    // for. Only the open record goes in the address.
    mockApi({
      '/customers': { ok: true, body: page([ada]) },
      [`/customers/${ada.id}`]: { ok: true, body: detail },
    });
    const { address } = renderCustomers();
    await screen.findByText('Ada Lovelace');

    await userEvent.type(screen.getByLabelText('Find someone'), 'love');

    expect(address()).toBe('/customers');
  });

  it('says so in one sentence when the address names somebody it cannot open', async () => {
    mockApi({
      '/customers': { ok: true, body: page([ada]) },
      '/customers/00000000-0000-0000-0000-000000000000': {
        ok: false, status: 404, code: 'customer.not_found', detail: 'No.',
      },
    });
    renderCustomers('/customers/00000000-0000-0000-0000-000000000000');

    expect(await screen.findByText(/That record cannot be opened/)).toBeVisible();
  });
});

describe('a customer’s address', () => {
  const withoutAddress = {
    id: ada.id,
    displayName: 'Ada Lovelace',
    kind: 'Person' as const,
    firstName: 'Ada',
    lastName: 'Lovelace',
    homeRooftopId: null,
    address: null,
    contactPoints: [],
    externalReference: null,
    creditLimit: null,
  };

  const withAddress = {
    ...withoutAddress,
    address: {
      line1: '18 Kestrel Way',
      line2: null,
      city: 'Springfield',
      administrativeArea: 'IL',
      county: 'Sangamon',
      postalCode: '62704',
      country: 'US',
    },
  };

  it('shows nothing recorded before anybody has typed one in', async () => {
    mockApi({
      '/customers': { ok: true, body: page([ada]) },
      [`/customers/${ada.id}`]: { ok: true, body: withoutAddress },
    });
    renderCustomers(`/customers/${ada.id}`);

    expect(await screen.findByText('No address recorded.')).toBeVisible();
    expect(screen.getByRole('button', { name: 'Add address' })).toBeVisible();
  });

  it('shows the address on file, joined into one line', async () => {
    mockApi({
      '/customers': { ok: true, body: page([ada]) },
      [`/customers/${ada.id}`]: { ok: true, body: withAddress },
    });
    renderCustomers(`/customers/${ada.id}`);

    expect(await screen.findByText(/18 Kestrel Way, Springfield, IL, Sangamon, 62704, US/))
      .toBeVisible();
  });

  it('sends the address as one call, distinct from the customer’s other fields', async () => {
    mockApi({
      '/customers': { ok: true, body: page([ada]) },
      [`/customers/${ada.id}`]: { ok: true, body: withoutAddress },
      [`/customers/${ada.id}/address`]: { ok: true, body: withAddress },
    });
    renderCustomers(`/customers/${ada.id}`);

    await userEvent.click(await screen.findByRole('button', { name: 'Add address' }));
    await userEvent.type(screen.getByLabelText('Address line 1'), '18 Kestrel Way');
    await userEvent.type(screen.getByLabelText('City'), 'Springfield');
    await userEvent.type(screen.getByLabelText('State or region'), 'IL');
    await userEvent.type(screen.getByLabelText('Country'), 'US');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    const sent = apiCalls().find((call) => call.path === `/customers/${ada.id}/address`);
    expect(sent).toBeDefined();

    const body = JSON.parse(sent!.init!.body as string) as {
      address: { line1: string; city: string; country: string };
    };
    expect(body.address.line1).toBe('18 Kestrel Way');
    expect(body.address.country).toBe('US');
  });

  it('clears the address by sending null rather than a set of blank fields', async () => {
    mockApi({
      '/customers': { ok: true, body: page([ada]) },
      [`/customers/${ada.id}`]: { ok: true, body: withAddress },
      [`/customers/${ada.id}/address`]: { ok: true, body: withoutAddress },
    });
    renderCustomers(`/customers/${ada.id}`);

    await userEvent.click(await screen.findByRole('button', { name: 'Edit' }));
    await userEvent.click(screen.getByRole('button', { name: 'Remove address' }));

    const sent = apiCalls().find((call) => call.path === `/customers/${ada.id}/address`);
    const body = JSON.parse(sent!.init!.body as string) as { address: unknown };
    expect(body.address).toBeNull();
  });
});

/**
 * A customer the provider no longer has (2026-09-19, ADR-026).
 *
 * The whole decision is that a tombstone MARKS and never hides. These are what
 * stop a later "tidy up the removed ones" change from quietly filtering the
 * list — which would reintroduce exactly the failure the ADR was written to
 * prevent: a customer disappearing from search while an advisor is on the
 * telephone to them.
 */
describe('a customer the provider no longer has', () => {
  const withdrawn = { ...ada, removedAtProviderOn: '2026-09-19T09:00:00Z' };

  it('is still in the list, and says so', async () => {
    mockApi({ '/customers': { ok: true, body: page([withdrawn]) } });
    renderCustomers();

    expect(await screen.findByText('Ada Lovelace')).toBeVisible();
    expect(screen.getByText('Removed at the provider')).toBeVisible();
  });

  it('explains on the record that it is kept and still works', async () => {
    const detail = {
      id: ada.id,
      displayName: 'Ada Lovelace',
      kind: 'Person' as const,
      firstName: 'Ada',
      lastName: 'Lovelace',
      homeRooftopId: null,
      address: null,
      contactPoints: [],
      externalReference: 'PROV-1',
      creditLimit: null,
      removedAtProviderOn: '2026-09-19T09:00:00Z',
    };

    mockApi({
      '/customers': { ok: true, body: page([withdrawn]) },
      [`/customers/${ada.id}`]: { ok: true, body: detail },
    });
    renderCustomers(`/customers/${ada.id}`);

    const band = await screen.findByRole('region', { name: 'Ada Lovelace' });
    expect(within(band).getByText(/no longer has this customer/)).toBeVisible();
    expect(within(band).getByText(/everything already attached to them still works/)).toBeVisible();
  });

  it('says nothing at all about a customer nobody has withdrawn', async () => {
    mockApi({ '/customers': { ok: true, body: page([ada]) } });
    renderCustomers();

    await screen.findByText('Ada Lovelace');
    expect(screen.queryByText('Removed at the provider')).not.toBeInTheDocument();
  });
});
