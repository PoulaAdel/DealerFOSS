// LabourPage.test — the workshop's numbers, and the two it refuses to invent.
//
// Use:  npm test.
// Edit: "says which figures it cannot produce" is the test that matters here.
//       Efficiency and productivity are what a service manager comes to a
//       report like this for, and this system has neither a roster nor a time
//       clock to produce them from. A future tidy-up that drops that band would
//       leave a report which quietly implies the two missing numbers were fine,
//       and both are used to judge individual people.

import { render, screen, within } from '../../test/render';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router';
import { LabourPage } from './LabourPage';
import { apiCalls, mockApi } from '../../test/setup';
import { setCurrentTenant } from '../../shared/api';
import type { LabourPerformance } from '../../shared/contracts';

const labour = (over: Partial<LabourPerformance> = {}): LabourPerformance => ({
  from: '2026-08-01',
  to: '2026-08-15',
  hoursSold: 126.5,
  labourRevenue: 11894,
  effectiveLabourRate: 94,
  byTechnician: [
    { technicianUserId: 'u5', hoursSold: 80, revenue: 7840, effectiveLabourRate: 98 },
    { technicianUserId: null, hoursSold: 46.5, revenue: 4054, effectiveLabourRate: 87.18 },
  ],
  byPayer: [
    { payType: 'CustomerPay', hoursSold: 92, revenue: 9016 },
    { payType: 'Warranty', hoursSold: 34.5, revenue: 2878 },
  ],
  notMeasured: ['Efficiency', 'Productivity'],
  ...over,
});

const technician = {
  id: 'u5',
  email: 'tech@dev.local',
  displayName: 'Workshop Technician',
  isActive: true,
  hasSecondFactor: false,
  canSignIn: true,
  awaitingEnrolment: false,
  recoveryIssuedAt: null,
  assignments: [
    { id: 'a1', roleId: 'r', roleName: 'Technician', isOrganizationWide: false, rooftopId: 'r1' },
  ],
};

function renderLabour() {
  setCurrentTenant('northgroup');

  return render(
    <MemoryRouter initialEntries={['/workshop/labour']}>
      <LabourPage />
    </MemoryRouter>,
  );
}

describe('the labour report', () => {
  it('leads with hours, revenue, and what an hour actually realised', async () => {
    mockApi({
      '/repair-orders/labour': { ok: true, body: labour() },
      '/staff': { ok: true, body: [technician] },
    });
    renderLabour();

    const headline = await screen.findByRole('table', { name: /hours sold, labour revenue/i });
    expect(within(headline).getByText('126.5')).toBeVisible();
    expect(within(headline).getByText('$11,894.00')).toBeVisible();
    // The one figure the screen exists for: 94 against a posted rate of 120 is
    // the gap a service manager is looking for.
    expect(within(headline).getByText('$94.00')).toBeVisible();
  });

  it('asks for a period rather than leaving the server to guess one', async () => {
    mockApi({
      '/repair-orders/labour': { ok: true, body: labour() },
      '/staff': { ok: true, body: [technician] },
    });
    renderLabour();
    await screen.findByRole('table', { name: /hours sold, labour revenue/i });

    const call = apiCalls().find((c) => c.path.startsWith('/repair-orders/labour'));
    expect(call?.path).toMatch(/from=\d{4}-\d{2}-\d{2}&to=\d{4}-\d{2}-\d{2}/);
  });

  it('asks again when the period changes', async () => {
    mockApi({
      '/repair-orders/labour': { ok: true, body: labour() },
      '/staff': { ok: true, body: [technician] },
    });
    renderLabour();
    await screen.findByRole('table', { name: /hours sold, labour revenue/i });

    await userEvent.clear(screen.getByLabelText('From'));
    await userEvent.type(screen.getByLabelText('From'), '2026-07-01');

    expect(apiCalls().some((c) => c.path.includes('from=2026-07-01'))).toBe(true);
  });

  it('names technicians from the staff list', async () => {
    mockApi({
      '/repair-orders/labour': { ok: true, body: labour() },
      '/staff': { ok: true, body: [technician] },
    });
    renderLabour();

    expect(await screen.findByText('Workshop Technician')).toBeVisible();
  });

  it('keeps the hours nobody is credited with, because that is the point', async () => {
    // Work invoiced with no technician assigned is exactly what a service
    // manager wants to see. Dropping the row would make the total stop adding up
    // and hide the problem at the same time.
    mockApi({
      '/repair-orders/labour': { ok: true, body: labour() },
      '/staff': { ok: true, body: [technician] },
    });
    renderLabour();

    expect(await screen.findByText('Nobody credited')).toBeVisible();
    const row = screen.getByText('Nobody credited').closest('tr')!;
    expect(within(row).getByText('46.5')).toBeVisible();
  });

  it('says why nobody is named when the staff list is out of reach', async () => {
    // Not silence, and not a screen full of raw identifiers: the numbers are
    // still right and the reader is told what is missing and why.
    mockApi({
      '/repair-orders/labour': { ok: true, body: labour() },
      '/staff': { ok: false, status: 403, code: 'staff.read_forbidden', detail: 'No.' },
    });
    renderLabour();

    expect(await screen.findByText(/cannot read the staff list/i)).toBeVisible();
    expect(screen.getAllByText('Not named').length).toBeGreaterThan(0);
  });

  it('shows the mix by payer, because the mix is itself the thing to watch', async () => {
    mockApi({
      '/repair-orders/labour': { ok: true, body: labour() },
      '/staff': { ok: true, body: [technician] },
    });
    renderLabour();

    const payers = await screen.findByRole('table', { name: /split by who settles/i });
    expect(within(payers).getByText('Customer pays')).toBeVisible();
    expect(within(payers).getByText('Warranty')).toBeVisible();
  });

  it('says which figures it cannot produce, and what each one would need', async () => {
    // See the file header. This is the test worth keeping.
    mockApi({
      '/repair-orders/labour': { ok: true, body: labour() },
      '/staff': { ok: true, body: [technician] },
    });
    renderLabour();

    expect(
      await screen.findByRole('heading', { name: 'What this does not measure' }),
    ).toBeVisible();
    expect(screen.getByText(/Efficiency — hours produced/)).toBeVisible();
    expect(screen.getByText(/no roster/)).toBeVisible();
    expect(screen.getByText(/Productivity — hours billed/)).toBeVisible();
    expect(screen.getByText(/no time clock/)).toBeVisible();
  });

  it('says the figures come from invoiced work only', async () => {
    mockApi({
      '/repair-orders/labour': { ok: true, body: labour() },
      '/staff': { ok: true, body: [technician] },
    });
    renderLabour();

    expect(await screen.findByText(/Work still in progress is not revenue/)).toBeVisible();
  });

  it('says plainly when nothing was invoiced, rather than showing an empty table', async () => {
    mockApi({
      '/repair-orders/labour': {
        ok: true,
        body: labour({ hoursSold: 0, labourRevenue: 0, effectiveLabourRate: 0, byTechnician: [], byPayer: [] }),
      },
      '/staff': { ok: true, body: [technician] },
    });
    renderLabour();

    expect(await screen.findByText(/no hours to report/i)).toBeVisible();
  });

  it('says plainly when the caller may not see these figures', async () => {
    mockApi({
      '/repair-orders/labour': { ok: false, status: 403, code: 'service.forbidden', detail: 'No.' },
      '/staff': { ok: true, body: [technician] },
    });
    renderLabour();

    expect(await screen.findByText(/do not have access to this workshop/i)).toBeVisible();
  });

  it('offers a way back from a failure', async () => {
    mockApi({
      '/repair-orders/labour': { ok: false, status: 500, code: 'server', detail: 'It fell over.' },
      '/staff': { ok: true, body: [technician] },
    });
    renderLabour();

    expect(await screen.findByRole('button', { name: 'Try again' })).toBeVisible();
  });

  it('keeps the wide tables scrolling inside their own box', async () => {
    mockApi({
      '/repair-orders/labour': { ok: true, body: labour() },
      '/staff': { ok: true, body: [technician] },
    });
    const { container } = renderLabour();
    await screen.findByText('Workshop Technician');

    expect(container.querySelectorAll('.scroll > table').length).toBe(2);
  });
});
