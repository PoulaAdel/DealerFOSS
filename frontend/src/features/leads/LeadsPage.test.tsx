// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   LeadsPage.test — the enquiry list, and the two things about it that are easy
//   to break by accident.
//
// Usage:
//   npm test
//
// Coding Instructions:
//   The trap this file guards is the screen growing its own opinions. The
//   moves offered must come from the server's `availableMoves` — if a test
//   here ever asserts that "Working leads offer an appointment", the
//   transition table has been copied into the browser and the copies will
//   drift. And there must be no rooftop picker on the list: the server
//   already filters to the caller's lots, so a picker would promise access
//   the server refuses.

import { renderAtRecordRoute, screen, waitFor, within } from '../../test/render';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { LeadsPage } from './LeadsPage';
import { SessionProvider } from '../../app/session';
import { apiCalls, mockApi, mockApiUnreachable, page } from '../../test/setup';
import { allPermissions } from '../../test/session';
import { setCurrentTenant } from '../../shared/api';
import type { LeadDetail, LeadSummary } from '../../shared/contracts';

const me = 'u1';

const summary: LeadSummary = {
  id: 'l1',
  rooftopId: 'r1',
  status: 'New',
  source: 'WalkIn',
  customerId: 'c1',
  customerName: 'Priya Raman',
  vehicleOfInterestId: 'v1',
  vehicleOfInterest: '2021 Toyota RAV4 XLE',
  assignedToUserId: null,
  assignedTo: null,
  capturedAt: '2026-08-01T09:00:00Z',
  daysOpen: 4,
};

const detail = (over: Partial<LeadDetail> = {}): LeadDetail => ({
  id: 'l1',
  rooftopId: 'r1',
  status: 'New',
  source: 'WalkIn',
  customerId: 'c1',
  customerName: 'Priya Raman',
  vehicleOfInterestId: 'v1',
  vehicleOfInterest: '2021 Toyota RAV4 XLE',
  assignedToUserId: null,
  assignedTo: null,
  enquiry: 'Wants something around 25k.',
  capturedAt: '2026-08-01T09:00:00Z',
  closedAt: null,
  isOpen: true,
  availableMoves: ['Working', 'Lost'],
  history: [
    { fromStatus: null, toStatus: 'New', occurredAt: '2026-08-01T09:00:00Z', note: null },
  ],
  ...over,
});

/** The session is real, because the screen asks it who "mine" is. */
function renderLeads(at = '/leads') {
  setCurrentTenant('northgroup');

  // At the screen's real route: the optional `:id` segment carries the open
  // enquiry, and a bare mount would have nowhere to navigate to.
  return renderAtRecordRoute(
    '/leads',
    <SessionProvider>
      <LeadsPage />
    </SessionProvider>,
    at,
  );
}

const signedIn = { ok: true as const, body: { userId: me, mustEnrolSecondFactor: false, permissions: allPermissions } };

/**
 * The row's own control, not the one in the signal band above it.
 *
 * An unassigned enquiry appears twice on purpose: once in "Nobody is chasing
 * these" and once in the list. Both open the same record, so an unscoped
 * `getByRole('button', { name })` matches two elements and throws. Scoping to
 * the table is also the more precise assertion — these tests are about the
 * list.
 */
function rowButton(name: string) {
  return within(screen.getByRole('table')).getByRole('button', { name });
}

async function openLead() {
  await screen.findByRole('table');
  await userEvent.click(rowButton('Priya Raman'));
}

const colleague = (over: Record<string, unknown> = {}) => ({
  id: 'u2',
  email: 'sales@dev.local',
  displayName: 'Rooftop Salesperson',
  isActive: true,
  hasSecondFactor: false,
  canSignIn: true,
  awaitingEnrolment: false,
  assignments: [
    { id: 'a1', roleId: 'r', roleName: 'Salesperson', isOrganizationWide: false, rooftopId: 'r1' },
  ],
  ...over,
});

describe('handing an enquiry to a named colleague', () => {
  it('offers colleagues by name', async () => {
    mockApi({
      '/auth/me': signedIn,
      '/leads/l1': { ok: true, body: detail() },
      '/leads': { ok: true, body: page([summary]) },
      '/staff': { ok: true, body: [colleague()] },
    });
    renderLeads();
    await openLead();

    expect(await screen.findByRole('option', { name: 'Rooftop Salesperson' })).toBeInTheDocument();
  });

  it('does not offer somebody who holds no role, because the enquiry would vanish', async () => {
    mockApi({
      '/auth/me': signedIn,
      '/leads/l1': { ok: true, body: detail() },
      '/leads': { ok: true, body: page([summary]) },
      '/staff': { ok: true, body: [colleague({ assignments: [] })] },
    });
    renderLeads();
    await openLead();

    expect(screen.queryByLabelText('Hand to')).toBeNull();
  });

  it('draws no picker at all when the caller may not read the staff list', async () => {
    // A salesperson without Staff.Read still has claim and release. An empty
    // picker would read as a broken screen rather than as a permission they do
    // not hold — the same reasoning as the missing rooftop picker.
    mockApi({
      '/auth/me': signedIn,
      '/leads/l1': { ok: true, body: detail() },
      '/leads': { ok: true, body: page([summary]) },
      '/staff': { ok: false, status: 403, code: 'staff.read_forbidden', detail: 'No.' },
    });
    renderLeads();
    await openLead();

    expect(await screen.findByRole('button', { name: 'I will chase this' })).toBeVisible();
    expect(screen.queryByLabelText('Hand to')).toBeNull();
  });

  it('names who is chasing it rather than saying "somebody else"', async () => {
    mockApi({
      '/auth/me': signedIn,
      '/leads': { ok: true, body: page([{ ...summary, assignedToUserId: 'u9', assignedTo: 'Ada Nwosu' }]) },
      '/staff': { ok: true, body: [] },
    });
    renderLeads();

    expect(await screen.findByText('Ada Nwosu')).toBeVisible();
  });
});

describe('the enquiry list', () => {
  it('lists what is being chased, and what each one is about', async () => {
    mockApi({ '/auth/me': signedIn, '/leads': { ok: true, body: page([summary]) } });
    renderLeads();

    await screen.findByRole('table');
    const list = within(screen.getByRole('table'));

    expect(rowButton('Priya Raman')).toBeVisible();
    expect(list.getByText('2021 Toyota RAV4 XLE')).toBeVisible();
    expect(list.getByText('Walk-in')).toBeVisible();
    expect(list.getByText('Nobody yet')).toBeVisible();
  });

  it('puts an enquiry nobody owns in the signal band, and takes it out once claimed', async () => {
    mockApi({ '/auth/me': signedIn, '/leads': { ok: true, body: page([summary]) } });
    renderLeads();

    // The band exists because the enquiry has nobody's name on it. That is the
    // one that rots — everything else on this screen has an owner who will be
    // asked about it.
    const band = await screen.findByRole('heading', { name: 'Nobody is chasing these' });
    expect(band).toBeVisible();
    expect(screen.getByText('1 enquiry has no name against it.')).toBeVisible();
    expect(screen.getByText(/waiting 4 days/)).toBeVisible();
  });

  it('says nothing at all when every enquiry has somebody chasing it', async () => {
    // A permanently present "0 need attention" panel trains people to stop
    // reading the one spot they must not stop reading, so it disappears.
    mockApi({
      '/auth/me': signedIn,
      '/leads': { ok: true, body: page([{ ...summary, assignedToUserId: 'u9', assignedTo: 'Ada Nwosu' }]) },
    });
    renderLeads();

    await screen.findByRole('table');
    expect(
      screen.queryByRole('heading', { name: 'Nobody is chasing these' }),
    ).not.toBeInTheDocument();
  });

  it('defaults to the ones still being chased', async () => {
    mockApi({ '/auth/me': signedIn, '/leads': { ok: true, body: page([summary]) } });
    renderLeads();
    await screen.findByRole('table');

    expect(apiCalls().some((c) => c.path.includes('openOnly=true'))).toBe(true);
  });

  it('narrows to mine without offering a rooftop to choose', async () => {
    mockApi({ '/auth/me': signedIn, '/leads': { ok: true, body: page([summary]) } });
    renderLeads();
    await screen.findByRole('table');

    await userEvent.click(screen.getByLabelText('Only mine'));

    await waitFor(() =>
      expect(apiCalls().some((c) => c.path.includes(`assignedTo=${me}`))).toBe(true),
    );

    // A one-lot user shown an empty rooftop picker reads it as a broken screen,
    // and a group user shown a full one is being promised access the server will
    // refuse. The list is already scoped; there is nothing to pick.
    expect(screen.queryByLabelText(/location/i)).not.toBeInTheDocument();
  });

  it('says the list is empty rather than showing an empty table', async () => {
    mockApi({ '/auth/me': signedIn, '/leads': { ok: true, body: page([]) } });
    renderLeads();

    expect(await screen.findByText(/No enquiries here/)).toBeVisible();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('explains a refusal, and offers a retry when the server is unreachable', async () => {
    mockApi({
      '/auth/me': signedIn,
      '/leads': { ok: false, status: 403, code: 'leads.forbidden', detail: 'No.' },
    });
    const { unmount } = renderLeads();
    expect(await screen.findByRole('alert')).toHaveTextContent(/do not have access to enquiries/i);
    unmount();

    mockApiUnreachable();
    renderLeads();
    expect(await screen.findByRole('alert')).toHaveTextContent(/Could not reach the server/);
    expect(screen.getByRole('button', { name: 'Try again' })).toBeVisible();
  });
});

describe('one enquiry', () => {
  it('shows what they said and where it has got to', async () => {
    mockApi({
      '/auth/me': signedIn,
      '/leads/l1': { ok: true, body: detail() },
      '/leads': { ok: true, body: page([summary]) },
    });

    renderLeads();
    await openLead();

    expect(await screen.findByText('Wants something around 25k.')).toBeVisible();
    expect(screen.getByText('Nobody has picked this up yet.')).toBeVisible();
  });

  it('offers exactly the moves the server allows, and nothing else', async () => {
    mockApi({
      '/auth/me': signedIn,
      // Deliberately a status whose real transition table this file does not
      // know. The screen must render what it was sent, not what it believes.
      '/leads/l1': { ok: true, body: detail({ status: 'Working', availableMoves: ['Appointment'] }) },
      '/leads': { ok: true, body: page([{ ...summary, status: 'Working' }]) },
    });

    renderLeads();
    await openLead();

    expect(await screen.findByRole('button', { name: 'They are coming in' })).toBeVisible();
    expect(screen.queryByRole('button', { name: 'Mark it lost' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'They are buying' })).not.toBeInTheDocument();
  });

  it('offers nothing on a finished enquiry', async () => {
    mockApi({
      '/auth/me': signedIn,
      '/leads/l1': { ok: true, body: detail({ status: 'Won', isOpen: false, availableMoves: [] }) },
      '/leads': { ok: true, body: page([{ ...summary, status: 'Won' }]) },
    });

    renderLeads();
    await openLead();

    expect(await screen.findByText(/This enquiry is finished/)).toBeVisible();
    expect(screen.queryByLabelText(/Note/)).not.toBeInTheDocument();
  });

  it('sends the note along with the move, so the record says why', async () => {
    mockApi({
      '/auth/me': signedIn,
      '/leads/l1': { ok: true, body: detail() },
      '/leads/l1/status': {
        ok: true,
        body: detail({ status: 'Working', availableMoves: ['Appointment', 'Won', 'Lost'] }),
      },
      '/leads': { ok: true, body: page([summary]) },
    });

    renderLeads();
    await openLead();

    await userEvent.type(await screen.findByLabelText(/Note/), 'Left a voicemail.');
    await userEvent.click(screen.getByRole('button', { name: 'Start chasing' }));

    await waitFor(() => {
      const call = apiCalls().find((c) => c.path === '/leads/l1/status');
      expect(call).toBeDefined();
      expect(JSON.parse(String(call!.init!.body))).toEqual({
        status: 'Working',
        note: 'Left a voicemail.',
      });
    });
  });

  it('shows the server’s refusal rather than predicting it', async () => {
    mockApi({
      '/auth/me': signedIn,
      '/leads/l1': { ok: true, body: detail() },
      '/leads/l1/status': {
        ok: false, status: 409, code: 'leads.status_not_allowed',
        detail: 'A New lead can only move to Working or Lost.',
      },
      '/leads': { ok: true, body: page([summary]) },
    });

    renderLeads();
    await openLead();
    await userEvent.click(await screen.findByRole('button', { name: 'Start chasing' }));

    expect(
      await screen.findByText('A New lead can only move to Working or Lost.'),
    ).toBeVisible();
  });

  it('claims an unassigned enquiry, and hands it back', async () => {
    mockApi({
      '/auth/me': signedIn,
      '/leads/l1': { ok: true, body: detail() },
      '/leads/l1/assign': { ok: true, body: detail({ assignedToUserId: me }) },
      '/leads': { ok: true, body: page([summary]) },
    });

    renderLeads();
    await openLead();
    await userEvent.click(await screen.findByRole('button', { name: 'I will chase this' }));

    expect(await screen.findByText('You are chasing this one.')).toBeVisible();
    expect(
      await screen.findByRole('button', { name: 'Put it back in the pool' }),
    ).toBeVisible();

    const call = apiCalls().find((c) => c.path === '/leads/l1/assign');
    expect(JSON.parse(String(call!.init!.body))).toEqual({ assignedToUserId: me });
  });

  it('offers the deal only once the enquiry is won', async () => {
    mockApi({
      '/auth/me': signedIn,
      '/leads/l1': { ok: true, body: detail() },
      '/leads': { ok: true, body: page([summary]) },
    });

    const { unmount } = renderLeads();
    await openLead();
    await screen.findByText('Wants something around 25k.');
    expect(screen.queryByRole('button', { name: 'Build the deal' })).not.toBeInTheDocument();
    unmount();

    mockApi({
      '/auth/me': signedIn,
      '/leads/l1': { ok: true, body: detail({ status: 'Won', availableMoves: [] }) },
      '/leads': { ok: true, body: page([{ ...summary, status: 'Won' }]) },
    });

    renderLeads();
    await openLead();
    expect(await screen.findByRole('button', { name: 'Build the deal' })).toBeVisible();
  });

  it('shows what happened, newest first', async () => {
    mockApi({
      '/auth/me': signedIn,
      '/leads/l1': {
        ok: true,
        body: detail({
          status: 'Working',
          availableMoves: ['Appointment', 'Won', 'Lost'],
          history: [
            { fromStatus: null, toStatus: 'New', occurredAt: '2026-08-01T09:00:00Z', note: null },
            { fromStatus: 'New', toStatus: 'Working', occurredAt: '2026-08-02T10:00:00Z', note: 'Left a voicemail.' },
          ],
        }),
      },
      '/leads': { ok: true, body: page([{ ...summary, status: 'Working' }]) },
    });

    renderLeads();
    await openLead();

    const history = await screen.findByRole('list', { name: 'What happened' });
    const entries = within(history).getAllByRole('listitem');
    expect(entries[0]).toHaveTextContent('Working');
    expect(entries[0]).toHaveTextContent('Left a voicemail.');
    expect(entries[1]).toHaveTextContent('New');
  });
});

describe('taking an enquiry', () => {
  const organization = {
    id: 'o1',
    name: 'North Auto Group',
    slug: 'northgroup',
    legalEntities: [
      {
        id: 'e1',
        name: 'North Auto Group LLC',
        rooftops: [
          { id: 'r1', name: 'Northgate', code: 'NAG-01', timeZone: 'UTC', legalEntityId: 'e1' },
        ],
      },
    ],
  };

  it('names the only location instead of offering a choice of one', async () => {
    mockApi({
      '/auth/me': signedIn,
      '/organization': { ok: true, body: organization },
      '/inventory': { ok: true, body: page([]) },
      '/customers': { ok: true, body: page([]) },
      '/leads': { ok: true, body: page([]) },
    });

    renderLeads();
    await userEvent.click(await screen.findByRole('button', { name: 'Take an enquiry' }));

    expect(await screen.findByText(/the only location you work at/)).toBeVisible();
    expect(screen.queryByLabelText('Which location')).not.toBeInTheDocument();
  });

  it('asks which location when the person works at more than one', async () => {
    mockApi({
      '/auth/me': signedIn,
      '/organization': {
        ok: true,
        body: {
          ...organization,
          legalEntities: [
            {
              ...organization.legalEntities[0],
              rooftops: [
                ...organization.legalEntities[0]!.rooftops,
                { id: 'r2', name: 'Southgate', code: 'NAG-02', timeZone: 'UTC', legalEntityId: 'e1' },
              ],
            },
          ],
        },
      },
      '/inventory': { ok: true, body: page([]) },
      '/customers': { ok: true, body: page([]) },
      '/leads': { ok: true, body: page([]) },
    });

    renderLeads();
    await userEvent.click(await screen.findByRole('button', { name: 'Take an enquiry' }));

    expect(await screen.findByLabelText('Which location')).toBeVisible();
  });

  it('saves the enquiry against the customer, the lot, and the car', async () => {
    mockApi({
      '/auth/me': signedIn,
      '/organization': { ok: true, body: organization },
      '/inventory': {
        ok: true,
        body: page([{
          id: 'u1', stockNumber: 'NAG-1042', rooftopId: 'r1', status: 'Available',
          vehicleId: 'v1', vin: '1HGCM82633A004352', vehicleDisplayName: '2021 Toyota RAV4 XLE',
        }]),
      },
      '/customers': {
        ok: true,
        body: page([{ id: 'c1', displayName: 'Priya Raman', kind: 'Person', primaryEmail: null, primaryPhone: null }]),
      },
      '/leads': [
        { ok: true, body: page([]) },
        { ok: true, body: detail() },
        { ok: true, body: page([summary]) },
      ],
    });

    renderLeads();
    await userEvent.click(await screen.findByRole('button', { name: 'Take an enquiry' }));

    await userEvent.selectOptions(await screen.findByLabelText('Who is asking'), 'c1');
    await userEvent.selectOptions(screen.getByLabelText('How they reached us'), 'Phone');

    // Off the picker's shortlist, by stock number. The old dropdown offered the
    // first 200 cars with no status filter at all — including sold ones.
    await userEvent.click(await screen.findByRole('button', { name: /NAG-1042/ }));

    await userEvent.click(screen.getByRole('button', { name: 'Save the enquiry' }));

    await waitFor(() => {
      const call = apiCalls().find((c) => c.path === '/leads' && c.init?.method === 'POST');
      expect(call).toBeDefined();

      // The vehicle, not the unit on the lot: the enquiry outlives that
      // particular car being sold to somebody else.
      expect(JSON.parse(String(call!.init!.body))).toMatchObject({
        rooftopId: 'r1',
        customerId: 'c1',
        source: 'Phone',
        vehicleOfInterestId: 'v1',
      });
    });
  });
});

/**
 * Reaching the enquiries the first page does not hold, and recording somebody
 * the dealership has never met.
 *
 * The walk on 2026-09-10 measured all three of these against the running
 * application: the chase panel took the fifty NEWEST enquiries and dropped the
 * two longest-waiting; the footer said "the first 50, there may be more" and
 * offered no way through; and a walk-in who was not already a customer could not
 * have an enquiry taken at all.
 */
describe('reaching every enquiry', () => {
  it('asks the server for the longest waiting, rather than sorting a page of the newest', async () => {
    // The heart of the defect. Sorting after the page arrives cannot fix a page
    // that contains the wrong rows.
    mockApi({ '/auth/me': signedIn, '/leads': { ok: true, body: page([summary]) } });
    renderLeads();

    await screen.findByRole('table');

    expect(apiCalls().some((c) => c.path.includes('order=longestWaiting'))).toBe(true);
  });

  it('says how many there are, not that there may be more', async () => {
    mockApi({
      '/auth/me': signedIn,
      '/leads': { ok: true, body: page([summary], { total: 213, offset: 0, limit: 50 }) },
    });
    renderLeads();

    // Said twice on purpose, to two audiences: the table's caption is what a
    // screen reader announces, the footer note is what a sighted user reads.
    // The capped note this replaces was written the same way.
    expect(await screen.findAllByText(/Showing 1–1 of 213\./)).toHaveLength(2);
  });

  it('reaches the next page, and cannot go back from the first', async () => {
    mockApi({
      '/auth/me': signedIn,
      '/leads': { ok: true, body: page([summary], { total: 213, offset: 0, limit: 50 }) },
    });
    renderLeads();

    await screen.findByRole('table');
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled();

    await userEvent.click(screen.getByRole('button', { name: 'Next' }));

    expect(apiCalls().some((c) => c.path.includes('offset=50'))).toBe(true);
  });

  it('offers no next page on the last one', async () => {
    mockApi({
      '/auth/me': signedIn,
      '/leads': { ok: true, body: page([summary], { total: 1, offset: 0, limit: 50 }) },
    });
    renderLeads();

    await screen.findByRole('table');
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled();
  });
});

describe('a walk-in nobody has met', () => {
  const organizationOnly = {
    id: 'o1',
    name: 'North Auto Group',
    legalEntities: [{ id: 'e1', name: 'North Auto Group LLC', rooftops: [
      { id: 'r1', code: 'NAG-01', name: 'North Auto Downtown' },
    ] }],
  };

  function mockCapture() {
    mockApi({
      '/auth/me': signedIn,
      '/organization': { ok: true, body: organizationOnly },
      '/inventory': { ok: true, body: page([]) },
      '/customers': { ok: true, body: page([]) },
      '/leads': { ok: true, body: page([]) },
    });
  }

  it('has a search button, rather than only answering to Enter', async () => {
    // Typing a name and tabbing onward left the same first 25 customers showing,
    // so the person concluded the customer was not on file.
    mockCapture();
    renderLeads();

    await userEvent.click(await screen.findByRole('button', { name: 'Take an enquiry' }));

    expect(await screen.findByRole('button', { name: 'Search' })).toBeVisible();
  });

  it('records somebody new without leaving the enquiry', async () => {
    mockCapture();
    renderLeads();

    await userEvent.click(await screen.findByRole('button', { name: 'Take an enquiry' }));
    await userEvent.click(await screen.findByRole('button', { name: /Not on file/ }));

    await userEvent.type(screen.getByLabelText('Last name'), 'Okonkwo');
    await userEvent.type(screen.getByLabelText('Phone'), '5551234567');

    mockApi({
      '/auth/me': signedIn,
      '/organization': { ok: true, body: organizationOnly },
      '/inventory': { ok: true, body: page([]) },
      '/customers': { ok: true, body: { id: 'c9', displayName: 'Okonkwo', kind: 'Person' } },
      '/leads': { ok: true, body: page([]) },
    });

    await userEvent.click(screen.getByRole('button', { name: 'Add and use them' }));

    // Added AND selected, so the enquiry can be saved without going anywhere.
    expect(await screen.findByRole('combobox', { name: 'Who is asking' })).toHaveValue('c9');
  });

  it('needs a surname before it will add anybody', async () => {
    mockCapture();
    renderLeads();

    await userEvent.click(await screen.findByRole('button', { name: 'Take an enquiry' }));
    await userEvent.click(await screen.findByRole('button', { name: /Not on file/ }));

    expect(screen.getByRole('button', { name: 'Add and use them' })).toBeDisabled();
  });
});

/**
 * An enquiry with an address of its own (2026-09-16).
 *
 * A sales manager chasing an unclaimed enquiry now has something to send. The
 * list stays underneath, signal band and all.
 */
describe('an enquiry reached by its own address', () => {
  it('arrives open when the address names the enquiry', async () => {
    mockApi({
      '/auth/me': signedIn,
      '/leads/l1': { ok: true, body: detail() },
      '/leads': { ok: true, body: page([summary]) },
      '/staff': { ok: true, body: [] },
    });
    renderLeads('/leads/l1');

    expect(
      await screen.findByRole('heading', { name: /Priya Raman · 2021 Toyota RAV4 XLE/ }),
    ).toBeVisible();
  });

  it('puts the enquiry in the address when a row is opened', async () => {
    mockApi({
      '/auth/me': signedIn,
      '/leads/l1': { ok: true, body: detail() },
      '/leads': { ok: true, body: page([summary]) },
      '/staff': { ok: true, body: [] },
    });
    const { address } = renderLeads();

    await openLead();
    await screen.findByRole('heading', { name: /Priya Raman · 2021 Toyota RAV4 XLE/ });

    expect(address()).toBe('/leads/l1');
  });

  it('says so in one sentence when the address names an enquiry it cannot open', async () => {
    // An enquiry IS rooftop-scoped, so this is the case that matters most:
    // "belongs to another branch" and "never existed" must read identically,
    // or a guessed id becomes a way to learn what other lots are working on.
    mockApi({
      '/auth/me': signedIn,
      '/leads': { ok: true, body: page([summary]) },
      '/leads/l9': { ok: false, status: 403, code: 'lead.forbidden', detail: 'No.' },
    });
    renderLeads('/leads/l9');

    expect(await screen.findByText(/That record cannot be opened/)).toBeVisible();
  });
});
