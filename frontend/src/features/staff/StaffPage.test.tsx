// StaffPage.test — the staff screen, and the properties that make it safe.
//
// Use:  npm test.
// Edit: two tests are safeguards rather than coverage.
//
//       "shows the code once and says it cannot be shown again" — if somebody
//       later adds a convenient "show it again" button, that test should be what
//       stops them, because the server keeps only a hash and there is nothing to
//       show.
//
//       "shows the server's refusal rather than predicting it" — the screen must
//       not grow its own copy of the scoping rule. Ask, then show the answer.

import { render, screen } from '../../test/render';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router';
import { StaffPage } from './StaffPage';
import { apiCalls, mockApi } from '../../test/setup';
import { setCurrentTenant } from '../../shared/api';
import type { StaffMember, StaffRole } from '../../shared/contracts';

const working: StaffMember = {
  id: 'u1',
  email: 'gm@dev.local',
  displayName: 'Dana Whitfield',
  isActive: true,
  hasSecondFactor: true,
  canSignIn: true,
  awaitingEnrolment: false,
  assignments: [
    {
      id: 'a1',
      roleId: 'role-manager',
      roleName: 'Manager',
      isOrganizationWide: true,
      rooftopId: null,
    },
  ],
};

const starter: StaffMember = {
  id: 'u2',
  email: 'new@dev.local',
  displayName: 'Sam Okonkwo',
  isActive: true,
  hasSecondFactor: false,
  canSignIn: false,
  awaitingEnrolment: true,
  assignments: [],
};

const leaver: StaffMember = {
  ...starter,
  id: 'u3',
  email: 'gone@dev.local',
  displayName: 'Chris Blake',
  isActive: false,
  awaitingEnrolment: false,
};

const roles: StaffRole[] = [
  {
    id: 'role-sales',
    name: 'Salesperson',
    requiresSecondFactor: false,
    permissions: ['Deals.Write', 'Leads.Manage'],
  },
];

const organization = {
  ok: true as const,
  body: { legalEntities: [{ rooftops: [{ id: 'r1', name: 'Northgate', code: 'NAG-01' }] }] },
};

function renderStaff() {
  setCurrentTenant('northgroup');

  return render(
    <MemoryRouter initialEntries={['/staff']}>
      <StaffPage />
    </MemoryRouter>,
  );
}

function withStaff(people: StaffMember[]) {
  mockApi({
    '/staff/roles': { ok: true, body: roles },
    '/organization': organization,
    '/staff': { ok: true, body: people },
  });
}

describe('the staff list', () => {
  it('shows who works here and what they hold', async () => {
    withStaff([working]);
    renderStaff();

    expect(await screen.findByRole('button', { name: 'Dana Whitfield' })).toBeVisible();
    expect(screen.getByText('gm@dev.local')).toBeVisible();
    expect(screen.getByText('Manager')).toBeVisible();
  });

  it('keeps the wide table scrolling inside its own box', async () => {
    // Found in a browser at 375px, not by this test: without `div.scroll` the
    // five columns push the whole page sideways. jsdom has no layout engine, so
    // this asserts the wrapper exists — which is what stops it being deleted as
    // a stray div. Whether it actually scrolls still needs a real browser.
    withStaff([working]);
    const { container } = renderStaff();
    await screen.findByRole('button', { name: 'Dana Whitfield' });

    expect(container.querySelector('.scroll > table')).not.toBeNull();
  });

  it('tells a starter apart from a leaver, because they need opposite actions', async () => {
    withStaff([starter, leaver]);
    renderStaff();

    expect(await screen.findByText('Awaiting first password')).toBeVisible();
    expect(screen.getByText('Stopped')).toBeVisible();
  });

  it('says plainly when the caller may not read it', async () => {
    mockApi({
      '/staff/roles': { ok: true, body: [] },
      '/organization': organization,
      '/staff': { ok: false, status: 403, code: 'staff.read_forbidden', detail: 'No.' },
    });
    renderStaff();

    expect(await screen.findByText(/do not have access to the staff list/i)).toBeVisible();
  });

  it('offers a way back from a failure', async () => {
    mockApi({
      '/staff/roles': { ok: true, body: [] },
      '/organization': organization,
      '/staff': { ok: false, status: 500, code: 'server', detail: 'The server fell over.' },
    });
    renderStaff();

    expect(await screen.findByRole('button', { name: 'Try again' })).toBeVisible();
  });
});

describe('adding somebody', () => {
  it('never asks for a password, and shows the code once', async () => {
    mockApi({
      '/staff/roles': { ok: true, body: roles },
      '/organization': organization,
      // In call order: the list, the created starter, then the refreshed list.
      // Keys are paths, so the POST and the GET share one queue.
      '/staff': [{ ok: true, body: [working] }, { ok: true, body: starter }, { ok: true, body: [working, starter] }],
      '/staff/u2/enrolment': {
        ok: true,
        body: { code: 'ABCD-EFGH-JKLM', expiresAt: '2026-08-06T10:00:00Z' },
      },
    });
    renderStaff();

    await userEvent.click(await screen.findByRole('button', { name: 'Add somebody' }));

    // Nobody should ever type a password on somebody else's behalf.
    expect(screen.queryByLabelText(/password/i)).toBeNull();

    await userEvent.type(screen.getByLabelText('Name'), 'Sam Okonkwo');
    await userEvent.type(screen.getByLabelText('Email'), 'new@dev.local');
    await userEvent.click(screen.getByRole('button', { name: 'Add and make a code' }));

    expect(await screen.findByText('ABCD-EFGH-JKLM')).toBeVisible();
  });

  it('says the code cannot be shown again, because it genuinely cannot', async () => {
    mockApi({
      '/staff/roles': { ok: true, body: roles },
      '/organization': organization,
      '/staff': [{ ok: true, body: [working] }, { ok: true, body: starter }, { ok: true, body: [working, starter] }],
      '/staff/u2/enrolment': {
        ok: true,
        body: { code: 'ABCD-EFGH-JKLM', expiresAt: '2026-08-06T10:00:00Z' },
      },
    });
    renderStaff();

    await userEvent.click(await screen.findByRole('button', { name: 'Add somebody' }));
    await userEvent.type(screen.getByLabelText('Name'), 'Sam Okonkwo');
    await userEvent.type(screen.getByLabelText('Email'), 'new@dev.local');
    await userEvent.click(screen.getByRole('button', { name: 'Add and make a code' }));

    expect(await screen.findByText(/only time it can be shown/i)).toBeVisible();
  });
});

describe('changing what somebody may reach', () => {
  it('says what a role grants before it is handed over', async () => {
    withStaff([working]);
    renderStaff();

    await userEvent.click(await screen.findByRole('button', { name: 'Dana Whitfield' }));
    await userEvent.selectOptions(await screen.findByLabelText('Role'), 'role-sales');

    expect(screen.getByText(/Deals\.Write/)).toBeVisible();
  });

  it('shows the server refusal rather than predicting it', async () => {
    mockApi({
      '/staff/roles': { ok: true, body: roles },
      '/organization': organization,
      '/staff': { ok: true, body: [working] },
      '/staff/u1': { ok: true, body: working },
      '/staff/u1/assignments': {
        ok: false,
        status: 403,
        code: 'staff.organization_scope_required',
        detail: 'This needs organization-wide permission.',
      },
    });
    renderStaff();

    await userEvent.click(await screen.findByRole('button', { name: 'Dana Whitfield' }));
    await userEvent.selectOptions(await screen.findByLabelText('Role'), 'role-sales');
    await userEvent.click(screen.getByRole('button', { name: 'Give them this' }));

    expect(await screen.findByText(/organization-wide permission/i)).toBeVisible();
  });

  it('sends a rooftop grant as a rooftop, not as everywhere', async () => {
    mockApi({
      '/staff/roles': { ok: true, body: roles },
      '/organization': organization,
      '/staff': { ok: true, body: [working] },
      '/staff/u1': { ok: true, body: working },
      '/staff/u1/assignments': { ok: true, status: 204 },
    });
    renderStaff();

    await userEvent.click(await screen.findByRole('button', { name: 'Dana Whitfield' }));
    await userEvent.selectOptions(await screen.findByLabelText('Role'), 'role-sales');
    await userEvent.selectOptions(screen.getByLabelText('Where'), 'r1');
    await userEvent.click(screen.getByRole('button', { name: 'Give them this' }));

    const grant = apiCalls().find((c) => c.path === '/staff/u1/assignments');
    expect(JSON.parse(String(grant?.init?.body))).toEqual({
      roleId: 'role-sales',
      rooftopId: 'r1',
    });
  });

  it('warns that stopping an account ends their sessions at once', async () => {
    withStaff([working]);
    renderStaff();

    await userEvent.click(await screen.findByRole('button', { name: 'Dana Whitfield' }));

    expect(await screen.findByText(/ends their sessions on the very next request/i)).toBeVisible();
  });
});
