// WorkshopPage.test — the workshop screen, and the safeguards on it.
//
// Use:  npm test.
// Edit: the test that matters most is "offers Invoice even with work unanswered".
//       It looks wrong at a glance, and it is the point: the screen must ASK and
//       show the server's refusal, because that refusal names the specific job
//       somebody still needs to ring about. A disabled button would be a second
//       copy of the rule and a worse message.

import { render, screen, within } from '../../test/render';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router';
import { WorkshopPage } from './WorkshopPage';
import { apiCalls, mockApi } from '../../test/setup';
import { setCurrentTenant } from '../../shared/api';
import type {
  RepairOrderDetail,
  RepairOrderSummary,
  ServiceLineView,
} from '../../shared/contracts';

const summary = (over: Partial<RepairOrderSummary> = {}): RepairOrderSummary => ({
  id: 'ro1',
  rooftopId: 'r1',
  number: 'RO-1001',
  status: 'InProgress',
  customerId: 'c1',
  customerName: 'Daniel Okafor',
  vehicleId: 'v1',
  vehicle: '2019 Honda Civic EX',
  complaint: 'Grinding noise at the front',
  amountDue: 180,
  currency: 'USD',
  advisorUserId: null,
  technicianUserId: null,
  linesAwaitingAnswer: 0,
  openedAt: '2026-08-01T09:00:00Z',
  ...over,
});

const line = (over: Partial<ServiceLineView> = {}): ServiceLineView => ({
  id: 'l1',
  kind: 'Labour',
  description: 'Investigate front-end noise',
  hours: 1.5,
  rate: 120,
  amount: 180,
  authorization: 'Authorized',
  authorizedAt: '2026-08-01T09:05:00Z',
  authorizedByUserId: 'u1',
  authorizationNote: null,
  ...over,
});

const detail = (over: Partial<RepairOrderDetail> = {}): RepairOrderDetail => ({
  id: 'ro1',
  rooftopId: 'r1',
  number: 'RO-1001',
  status: 'InProgress',
  customerId: 'c1',
  customerName: 'Daniel Okafor',
  vehicleId: 'v1',
  vehicle: '2019 Honda Civic EX',
  complaint: 'Grinding noise at the front',
  odometerReading: 64000,
  currency: 'USD',
  labourTotal: 180,
  partsTotal: 0,
  subletTotal: 0,
  amountDue: 180,
  advisorUserId: null,
  technicianUserId: null,
  openedAt: '2026-08-01T09:00:00Z',
  invoicedAt: null,
  linesAreOpen: true,
  availableMoves: ['Completed', 'Cancelled'],
  lines: [line()],
  history: [
    {
      fromStatus: null,
      toStatus: 'Booked',
      occurredAt: '2026-08-01T09:00:00Z',
      changedByUserId: 'u1',
      note: null,
      amountAtChange: 0,
    },
  ],
  ...over,
});

function renderWorkshop() {
  setCurrentTenant('northgroup');

  return render(
    <MemoryRouter initialEntries={['/workshop']}>
      <WorkshopPage />
    </MemoryRouter>,
  );
}

const noStaff = { ok: true as const, body: [] };

// The screen now carries the diary above the job list. These tests are about the
// jobs, so the diary answers "nothing booked in" — arranged rather than left
// unmocked, because an unarranged path throws and every test here would quietly
// render an error panel instead. The diary has its own file.
const noDiary = { ok: true as const, body: { appointments: [], load: [] } };

async function openJob() {
  await userEvent.click(await screen.findByRole('button', { name: 'RO-1001' }));
}

describe('the workshop list', () => {
  it('shows the jobs in the workshop', async () => {
    mockApi({ '/appointments': noDiary, '/repair-orders': { ok: true, body: [summary()] }, '/staff': noStaff });
    renderWorkshop();

    expect(await screen.findByRole('button', { name: 'RO-1001' })).toBeVisible();
    expect(screen.getByText('2019 Honda Civic EX')).toBeVisible();
    expect(screen.getByText('$180.00')).toBeVisible();
  });

  it('leads with the calls somebody owes, because each one blocks an invoice', async () => {
    mockApi({
      '/appointments': noDiary,
      '/repair-orders': { ok: true, body: [summary({ linesAwaitingAnswer: 2 })] },
      '/staff': noStaff,
    });
    renderWorkshop();

    const waiting = await screen.findByRole('heading', { name: 'Waiting on a customer' });
    expect(waiting).toBeVisible();
    expect(screen.getByText(/2 jobs to ask about/)).toBeVisible();
  });

  it('says nothing about calls when there are none to make', async () => {
    mockApi({ '/appointments': noDiary, '/repair-orders': { ok: true, body: [summary()] }, '/staff': noStaff });
    renderWorkshop();
    await screen.findByRole('button', { name: 'RO-1001' });

    expect(screen.queryByRole('heading', { name: 'Waiting on a customer' })).toBeNull();
  });

  it('defaults to jobs still open', async () => {
    mockApi({ '/appointments': noDiary, '/repair-orders': { ok: true, body: [summary()] }, '/staff': noStaff });
    renderWorkshop();
    await screen.findByRole('button', { name: 'RO-1001' });

    expect(apiCalls().some((c) => c.path.includes('openOnly=true'))).toBe(true);
  });

  it('says plainly when the caller may not see this workshop', async () => {
    mockApi({
      '/appointments': noDiary,
      '/repair-orders': { ok: false, status: 403, code: 'service.forbidden', detail: 'No.' },
      '/staff': noStaff,
    });
    renderWorkshop();

    expect(await screen.findByText(/do not have access to this location/i)).toBeVisible();
  });

  it('offers a way back from a failure', async () => {
    mockApi({
      '/appointments': noDiary,
      '/repair-orders': { ok: false, status: 500, code: 'server', detail: 'It fell over.' },
      '/staff': noStaff,
    });
    renderWorkshop();

    expect(await screen.findByRole('button', { name: 'Try again' })).toBeVisible();
  });

  it('keeps the wide table scrolling inside its own box', async () => {
    mockApi({ '/appointments': noDiary, '/repair-orders': { ok: true, body: [summary()] }, '/staff': noStaff });
    const { container } = renderWorkshop();
    await screen.findByRole('button', { name: 'RO-1001' });

    expect(container.querySelector('.scroll > table')).not.toBeNull();
  });
});

describe('one job', () => {
  it('splits labour from parts, because one total tells a manager nothing', async () => {
    mockApi({
      '/appointments': noDiary,
      '/repair-orders/ro1': {
        ok: true,
        body: detail({ labourTotal: 180, partsTotal: 284, amountDue: 464 }),
      },
      '/repair-orders': { ok: true, body: [summary()] },
      '/staff': noStaff,
    });
    renderWorkshop();
    await openJob();

    const totals = await screen.findByRole('table', { name: /what the job comes to/i });
    expect(within(totals).getByText('$180.00')).toBeVisible();
    expect(within(totals).getByText('$284.00')).toBeVisible();
    expect(within(totals).getByText('$464.00')).toBeVisible();
  });

  it('shows declined work at nothing rather than at what it would have cost', async () => {
    mockApi({
      '/appointments': noDiary,
      '/repair-orders/ro1': {
        ok: true,
        body: detail({
          lines: [line({ authorization: 'Declined', amount: 0, description: 'Replace discs' })],
        }),
      },
      '/repair-orders': { ok: true, body: [summary()] },
      '/staff': noStaff,
    });
    renderWorkshop();
    await openJob();

    expect(await screen.findByText('Said no')).toBeVisible();
    expect(screen.getByText('Replace discs')).toBeVisible();
  });

  it('offers only the moves the server says are legal', async () => {
    mockApi({
      '/appointments': noDiary,
      '/repair-orders/ro1': { ok: true, body: detail({ availableMoves: ['Completed', 'Cancelled'] }) },
      '/repair-orders': { ok: true, body: [summary()] },
      '/staff': noStaff,
    });
    renderWorkshop();
    await openJob();

    expect(await screen.findByRole('button', { name: 'Work is finished' })).toBeVisible();
    expect(screen.getByRole('button', { name: 'Cancel the job' })).toBeVisible();
    expect(screen.queryByRole('button', { name: 'Invoice it' })).toBeNull();
  });

  it('offers nothing on a finished job', async () => {
    mockApi({
      '/appointments': noDiary,
      '/repair-orders/ro1': {
        ok: true,
        body: detail({ status: 'Invoiced', availableMoves: [], invoicedAt: '2026-08-04T10:00:00Z' }),
      },
      '/repair-orders': { ok: true, body: [summary()] },
      '/staff': noStaff,
    });
    renderWorkshop();
    await openJob();

    expect(await screen.findByText(/Nothing more to do/)).toBeVisible();
  });

  it('hides the write-up form once the work is frozen', async () => {
    mockApi({
      '/appointments': noDiary,
      '/repair-orders/ro1': {
        ok: true,
        body: detail({ status: 'Completed', linesAreOpen: false, availableMoves: ['Invoiced'] }),
      },
      '/repair-orders': { ok: true, body: [summary()] },
      '/staff': noStaff,
    });
    renderWorkshop();
    await openJob();

    await screen.findByRole('button', { name: 'Invoice it' });
    // A disabled form still reads as somewhere to type. Same reasoning as the
    // deal desk's charges editor.
    expect(screen.queryByRole('heading', { name: 'Write up more work' })).toBeNull();
  });
});

describe('work nobody has agreed to', () => {
  const unanswered = detail({
    lines: [line(), line({ id: 'l2', description: 'Replace discs', authorization: 'Pending', amount: 340 })],
    availableMoves: ['Completed', 'Cancelled'],
  });

  it('marks it as nobody having asked', async () => {
    mockApi({
      '/appointments': noDiary,
      '/repair-orders/ro1': { ok: true, body: unanswered },
      '/repair-orders': { ok: true, body: [summary({ linesAwaitingAnswer: 1 })] },
      '/staff': noStaff,
    });
    renderWorkshop();
    await openJob();

    expect(await screen.findByText('Nobody has asked')).toBeVisible();
  });

  it('records how the answer was obtained, not just the answer', async () => {
    mockApi({
      '/appointments': noDiary,
      '/repair-orders/ro1': { ok: true, body: unanswered },
      '/repair-orders': { ok: true, body: [summary({ linesAwaitingAnswer: 1 })] },
      '/repair-orders/ro1/lines/l2/answer': { ok: true, body: detail() },
      '/staff': noStaff,
    });
    renderWorkshop();
    await openJob();

    await userEvent.click(await screen.findByRole('button', { name: 'I rang them' }));
    await userEvent.type(
      screen.getByLabelText('How it was obtained'),
      'Phoned 10:40, spoke to Mr Okafor',
    );
    await userEvent.click(screen.getByRole('button', { name: 'They agreed' }));

    const call = apiCalls().find((c) => c.path === '/repair-orders/ro1/lines/l2/answer');
    expect(JSON.parse(String(call?.init?.body))).toEqual({
      approved: true,
      note: 'Phoned 10:40, spoke to Mr Okafor',
    });
  });

  it('treats "they said no" as a real answer rather than a deletion', async () => {
    mockApi({
      '/appointments': noDiary,
      '/repair-orders/ro1': { ok: true, body: unanswered },
      '/repair-orders': { ok: true, body: [summary({ linesAwaitingAnswer: 1 })] },
      '/repair-orders/ro1/lines/l2/answer': { ok: true, body: detail() },
      '/staff': noStaff,
    });
    renderWorkshop();
    await openJob();

    await userEvent.click(await screen.findByRole('button', { name: 'I rang them' }));
    await userEvent.click(screen.getByRole('button', { name: 'They said no' }));

    const call = apiCalls().find((c) => c.path === '/repair-orders/ro1/lines/l2/answer');
    expect(JSON.parse(String(call?.init?.body)).approved).toBe(false);
  });

  it('still lets somebody record the answer after the work is frozen', async () => {
    // The dead end this test exists to stop, found by walking the real flow:
    // finish the job, try to invoice, get told to ring — and the screen had
    // hidden the answer button because the LINES were frozen. Freezing the work
    // and recording what the customer said are different acts, and the server's
    // AnswerLine deliberately has no LinesAreOpen check.
    mockApi({
      '/appointments': noDiary,
      '/repair-orders/ro1': {
        ok: true,
        body: detail({
          status: 'Completed',
          linesAreOpen: false,
          availableMoves: ['Invoiced', 'InProgress'],
          lines: [line({ id: 'l2', description: 'Replace discs', authorization: 'Pending' })],
        }),
      },
      '/repair-orders': { ok: true, body: [summary({ linesAwaitingAnswer: 1 })] },
      '/staff': noStaff,
    });
    renderWorkshop();
    await openJob();

    expect(await screen.findByRole('button', { name: 'I rang them' })).toBeVisible();
    // ...but the work itself is still frozen.
    expect(screen.queryByRole('button', { name: 'Remove' })).toBeNull();
    expect(screen.queryByRole('heading', { name: 'Write up more work' })).toBeNull();
  });

  it('offers Invoice even with work unanswered, and shows the refusal', async () => {
    // Deliberate. The server's refusal names the job somebody still needs to ring
    // about; a greyed-out button would say less and would be a second copy of the
    // rule. Predicting the answer here is the mistake this test exists to stop.
    mockApi({
      '/appointments': noDiary,
      '/repair-orders/ro1': {
        ok: true,
        body: detail({
          status: 'Completed',
          linesAreOpen: false,
          availableMoves: ['Invoiced', 'InProgress'],
          lines: [line({ id: 'l2', description: 'Replace discs', authorization: 'Pending' })],
        }),
      },
      '/repair-orders': { ok: true, body: [summary({ linesAwaitingAnswer: 1 })] },
      '/repair-orders/ro1/status': {
        ok: false,
        status: 409,
        code: 'service.line_awaiting_answer',
        detail: 'Replace discs is still waiting on the customer.',
      },
      '/staff': noStaff,
    });
    renderWorkshop();
    await openJob();

    const invoice = await screen.findByRole('button', { name: 'Invoice it' });
    expect(invoice).toBeEnabled();

    await userEvent.click(invoice);

    expect(await screen.findByText(/still waiting on the customer/i)).toBeVisible();
  });
});

describe('who is doing the work', () => {
  const technician = {
    id: 'u5',
    email: 'tech@dev.local',
    displayName: 'Workshop Technician',
    isActive: true,
    hasSecondFactor: false,
    canSignIn: true,
    awaitingEnrolment: false,
    assignments: [
      { id: 'a1', roleId: 'r', roleName: 'Technician', isOrganizationWide: false, rooftopId: 'r1' },
    ],
  };

  it('offers colleagues by name', async () => {
    mockApi({
      '/appointments': noDiary,
      '/repair-orders/ro1': { ok: true, body: detail() },
      '/repair-orders': { ok: true, body: [summary()] },
      '/staff': { ok: true, body: [technician] },
    });
    renderWorkshop();
    await openJob();

    expect(await screen.findByRole('option', { name: 'Workshop Technician' })).toBeInTheDocument();
  });

  it('draws no picker when the caller may not read the staff list', async () => {
    mockApi({
      '/appointments': noDiary,
      '/repair-orders/ro1': { ok: true, body: detail() },
      '/repair-orders': { ok: true, body: [summary()] },
      '/staff': { ok: false, status: 403, code: 'staff.read_forbidden', detail: 'No.' },
    });
    renderWorkshop();
    await openJob();

    await screen.findByRole('heading', { name: /RO-1001/ });
    expect(screen.queryByLabelText('Who is on it')).toBeNull();
  });
});
