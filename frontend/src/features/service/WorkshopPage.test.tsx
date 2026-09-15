// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   WorkshopPage.test — the workshop screen, and the safeguards on it.
//
// Usage:
//   npm test.
//
// Coding Instructions:
//   The test that matters most is "offers Invoice even with work unanswered".
//   It looks wrong at a glance, and it is the point: the screen must ASK and
//   show the server's refusal, because that refusal names the specific job
//   somebody still needs to ring about. A disabled button would be a second
//   copy of the rule and a worse message.

import { render, screen, within } from '../../test/render';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router';
import { WorkshopPage } from './WorkshopPage';
import { apiCalls, mockApi, page } from '../../test/setup';
import { setCurrentTenant } from '../../shared/api';
import type {
  RepairOrderDetail,
  RepairOrderSummary,
  ServiceLineView,
} from '../../shared/contracts';

const summary = (over: Partial<RepairOrderSummary> = {}): RepairOrderSummary => ({
  id: 'ro1',
  rooftopId: 'r1',
  rooftopCode: 'NAG-01',
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
  payType: 'CustomerPay',
  authorization: 'Authorized',
  authorizedAt: '2026-08-01T09:05:00Z',
  authorizedByUserId: 'u1',
  authorizationNote: null,
  ...over,
});

const detail = (over: Partial<RepairOrderDetail> = {}): RepairOrderDetail => ({
  id: 'ro1',
  rooftopId: 'r1',
  rooftopCode: 'NAG-01',
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
  warrantyTotal: 0,
  internalTotal: 0,
  workTotal: 180,
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
    mockApi({ '/appointments': noDiary, '/repair-orders': { ok: true, body: page([summary()]) }, '/staff': noStaff });
    renderWorkshop();

    expect(await screen.findByRole('button', { name: 'RO-1001' })).toBeVisible();
    expect(screen.getByText('2019 Honda Civic EX')).toBeVisible();
    expect(screen.getByText('$180.00')).toBeVisible();
  });

  it('says which lot a job is at, but only when the list mixes them', async () => {
    // Job numbers restart per rooftop BY DESIGN — 162 jobs at this dealership
    // share 82 numbers, 80 of them used twice — so a list spanning two lots
    // shows two different jobs under one number. Asking the running screen for
    // RO-1082 on 2026-09-15 returned two rows and the only way to tell them
    // apart was to read the DOM.
    mockApi({
      '/appointments': noDiary,
      '/repair-orders': {
        ok: true,
        body: page([
          summary({ id: 'a', number: 'RO-1082', rooftopId: 'r1', rooftopCode: 'NAG-01' }),
          summary({ id: 'b', number: 'RO-1082', rooftopId: 'r2', rooftopCode: 'NAG-02' }),
        ]),
      },
      '/staff': noStaff,
    });

    renderWorkshop();

    expect(await screen.findByText('NAG-01')).toBeVisible();
    expect(screen.getByText('NAG-02')).toBeVisible();
  });

  it('keeps the lot out of the way at a dealership with one', async () => {
    // Every row would carry the same code and it would say nothing. The
    // question is "is what I am looking at ambiguous", not "how many lots does
    // this person cover".
    mockApi({
      '/appointments': noDiary,
      '/repair-orders': {
        ok: true,
        body: page([
          summary({ id: 'a', number: 'RO-1001', rooftopId: 'r1', rooftopCode: 'NAG-01' }),
          summary({ id: 'b', number: 'RO-1002', rooftopId: 'r1', rooftopCode: 'NAG-01' }),
        ]),
      },
      '/staff': noStaff,
    });

    renderWorkshop();

    expect(await screen.findByRole('button', { name: 'RO-1001' })).toBeVisible();
    expect(screen.queryByText('NAG-01')).not.toBeInTheDocument();
  });

  it('leads with the calls somebody owes, because each one blocks an invoice', async () => {
    mockApi({
      '/appointments': noDiary,
      '/repair-orders': { ok: true, body: page([summary({ linesAwaitingAnswer: 2 })]) },
      '/staff': noStaff,
    });
    renderWorkshop();

    const waiting = await screen.findByRole('heading', { name: 'Waiting on a customer' });
    expect(waiting).toBeVisible();
    expect(screen.getByText(/2 jobs to ask about/)).toBeVisible();
  });

  it('says nothing about calls when there are none to make', async () => {
    mockApi({ '/appointments': noDiary, '/repair-orders': { ok: true, body: page([summary()]) }, '/staff': noStaff });
    renderWorkshop();
    await screen.findByRole('button', { name: 'RO-1001' });

    expect(screen.queryByRole('heading', { name: 'Waiting on a customer' })).toBeNull();
  });

  it('defaults to jobs still open', async () => {
    mockApi({ '/appointments': noDiary, '/repair-orders': { ok: true, body: page([summary()]) }, '/staff': noStaff });
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
    mockApi({ '/appointments': noDiary, '/repair-orders': { ok: true, body: page([summary()]) }, '/staff': noStaff });
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
      '/repair-orders': { ok: true, body: page([summary()]) },
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
      '/repair-orders': { ok: true, body: page([summary()]) },
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
      '/repair-orders': { ok: true, body: page([summary()]) },
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
      '/repair-orders': { ok: true, body: page([summary()]) },
      '/staff': noStaff,
    });
    renderWorkshop();
    await openJob();

    // The wording changed on 2026-09-11: "Nothing more to do" stopped being true
    // the moment an invoiced job could be paid. The stage note now points at the
    // money instead of declaring the job closed.
    expect(await screen.findByText(/The work is finished/)).toBeVisible();
  });

  it('hides the write-up form once the work is frozen', async () => {
    mockApi({
      '/appointments': noDiary,
      '/repair-orders/ro1': {
        ok: true,
        body: detail({ status: 'Completed', linesAreOpen: false, availableMoves: ['Invoiced'] }),
      },
      '/repair-orders': { ok: true, body: page([summary()]) },
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
      '/repair-orders': { ok: true, body: page([summary({ linesAwaitingAnswer: 1 })]) },
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
      '/repair-orders': { ok: true, body: page([summary({ linesAwaitingAnswer: 1 })]) },
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
      '/repair-orders': { ok: true, body: page([summary({ linesAwaitingAnswer: 1 })]) },
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
      '/repair-orders': { ok: true, body: page([summary({ linesAwaitingAnswer: 1 })]) },
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
      '/repair-orders': { ok: true, body: page([summary({ linesAwaitingAnswer: 1 })]) },
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

describe('who pays for the work', () => {
  // A job with all three payers on it, which is the ordinary case rather than a
  // contrived one: the customer's brake pads, a warranty claim for the part that
  // failed, and an internal charge for getting the car ready.
  const mixed = detail({
    lines: [
      line({ id: 'l1', description: 'Replace front pads', amount: 180 }),
      line({ id: 'l2', description: 'Replace failed caliper', payType: 'Warranty', amount: 240 }),
      line({ id: 'l3', description: 'Valet before handover', payType: 'Internal', amount: 40 }),
    ],
    // Deliberately all different, so an assertion on one figure cannot pass by
    // matching a different row that happens to hold the same number.
    labourTotal: 400,
    partsTotal: 60,
    amountDue: 180,
    warrantyTotal: 240,
    internalTotal: 40,
    workTotal: 460,
  });

  const arrange = () =>
    mockApi({
      '/appointments': noDiary,
      '/repair-orders/ro1': { ok: true, body: mixed },
      '/repair-orders': { ok: true, body: page([summary()]) },
      '/staff': noStaff,
    });

  it('says who settles each line', async () => {
    arrange();
    renderWorkshop();
    await openJob();

    // Scoped to each line's own row. "Warranty" and "Internal" also appear in
    // the totals block below, and an unscoped getByText would be satisfied by
    // the wrong one — which would let the line column disappear entirely
    // without this test noticing.
    const caliper = (await screen.findByText('Replace failed caliper')).closest('tr')!;
    expect(within(caliper).getByText('Warranty')).toBeVisible();

    const valet = screen.getByText('Valet before handover').closest('tr')!;
    expect(within(valet).getByText('Internal')).toBeVisible();

    const pads = screen.getByText('Replace front pads').closest('tr')!;
    expect(within(pads).getByText('Customer pays')).toBeVisible();
  });

  it('does not claim the customer agreed to work they are not paying for', async () => {
    // The server marks warranty and internal lines authorized on arrival, so
    // they arrive as 'Authorized' — and printing "Agreed" against them would be
    // a record of a conversation that never happened.
    arrange();
    renderWorkshop();
    await openJob();

    await screen.findByText('Replace failed caliper');
    expect(screen.getAllByText('Not the customer’s to agree')).toHaveLength(2);
    // ...and the one line that IS the customer's still says so.
    expect(screen.getByText('Agreed')).toBeVisible();
  });

  it('keeps Due to what the customer owes, and shows the rest apart from it', async () => {
    // The defect this test exists to stop: 460 on the customer's invoice.
    arrange();
    renderWorkshop();
    await openJob();

    const totals = await screen.findByRole('table', { name: /what the job comes to/i });
    const due = within(totals).getByRole('row', { name: /^Due/ });

    expect(within(due).getByText('$180.00')).toBeVisible();
    expect(within(totals).getByText('$240.00')).toBeVisible();
    expect(within(totals).getByText('$40.00')).toBeVisible();
    expect(within(totals).getByText('$460.00')).toBeVisible();
  });

  it('leaves the payer rows out of an ordinary customer job', async () => {
    // A permanent "Warranty 0.00" on every job is noise on the one block
    // somebody reads while deciding what to charge.
    mockApi({
      '/appointments': noDiary,
      '/repair-orders/ro1': { ok: true, body: detail() },
      '/repair-orders': { ok: true, body: page([summary()]) },
      '/staff': noStaff,
    });
    renderWorkshop();
    await openJob();

    const totals = await screen.findByRole('table', { name: /what the job comes to/i });
    expect(within(totals).queryByRole('row', { name: /Warranty/ })).toBeNull();
    expect(within(totals).queryByRole('row', { name: /All the work/ })).toBeNull();
  });

  it('keeps the open job on screen while the list is refetched', async () => {
    // Found by driving the real screen: every act on a job replaced the WHOLE
    // page with "Loading the workshop…" while the list refetched, then rebuilt
    // it — unmounting the detail band and losing half-typed input.
    //
    // The delay is what makes this test real. With an instant reply the refetch
    // finishes inside the same act() and the blank never becomes observable,
    // so the assertion would pass against the broken code too.
    mockApi({
      '/appointments': noDiary,
      '/repair-orders/ro1': { ok: true, body: detail() },
      '/repair-orders/ro1/lines': { ok: true, body: detail() },
      '/repair-orders': [
        { ok: true, body: page([summary()]) },
        { ok: true, body: page([summary()]), delayMs: 50 },
      ],
      '/staff': noStaff,
    });
    renderWorkshop();
    await openJob();

    await userEvent.type(await screen.findByLabelText('Description'), 'Wiper blades');
    await userEvent.click(screen.getByRole('button', { name: 'Write it up' }));

    // Mid-refetch: the job is still there and the page has not blanked.
    expect(screen.getByRole('heading', { name: /RO-1001/ })).toBeVisible();
    expect(screen.queryByText('Loading the workshop…')).toBeNull();
  });

  it('sends who pays when work is written up', async () => {
    mockApi({
      '/appointments': noDiary,
      '/repair-orders/ro1': { ok: true, body: detail() },
      '/repair-orders/ro1/lines': { ok: true, body: detail() },
      '/repair-orders': { ok: true, body: page([summary()]) },
      '/staff': noStaff,
    });
    renderWorkshop();
    await openJob();

    await userEvent.selectOptions(await screen.findByLabelText('Who pays'), 'Warranty');
    await userEvent.type(screen.getByLabelText('Description'), 'Replace failed caliper');
    await userEvent.click(screen.getByRole('button', { name: 'Write it up' }));

    const call = apiCalls().find((c) => c.path === '/repair-orders/ro1/lines');
    expect(JSON.parse(String(call?.init?.body)).payType).toBe('Warranty');
  });

  it('defaults to the customer, so silence never produces a warranty claim', async () => {
    mockApi({
      '/appointments': noDiary,
      '/repair-orders/ro1': { ok: true, body: detail() },
      '/repair-orders/ro1/lines': { ok: true, body: detail() },
      '/repair-orders': { ok: true, body: page([summary()]) },
      '/staff': noStaff,
    });
    renderWorkshop();
    await openJob();

    await userEvent.type(await screen.findByLabelText('Description'), 'Wiper blades');
    await userEvent.click(screen.getByRole('button', { name: 'Write it up' }));

    const call = apiCalls().find((c) => c.path === '/repair-orders/ro1/lines');
    expect(JSON.parse(String(call?.init?.body)).payType).toBe('CustomerPay');
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
      '/repair-orders': { ok: true, body: page([summary()]) },
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
      '/repair-orders': { ok: true, body: page([summary()]) },
      '/staff': { ok: false, status: 403, code: 'staff.read_forbidden', detail: 'No.' },
    });
    renderWorkshop();
    await openJob();

    await screen.findByRole('heading', { name: /RO-1001/ });
    expect(screen.queryByLabelText('Who is on it')).toBeNull();
  });
});

/**
 * The parts picker on a job line.
 *
 * `src/App/Parts` was a complete capability from the day it was built, and
 * verify-e2e proved its average costing on every run. What did not exist was any
 * way to reach it: the write-up form sent a description and an amount and never a
 * part id, so every part billed in a browser was free text with no cost, 5300 and
 * 1400 never posted, and the dashboard stated a 100% margin on service as fact.
 * These tests are about the reaching.
 */
describe('billing a part off the shelf', () => {
  const brakePads = {
    id: 'p1',
    partNumber: 'BP-4471',
    description: 'Front brake pads',
    rooftopId: 'r1',
    quantityOnHand: 12,
    unitCost: 34.5,
    currency: 'USD',
  };

  function mockWithCatalogue() {
    mockApi({
      '/appointments': noDiary,
      '/repair-orders': { ok: true, body: page([summary()]) },
      '/repair-orders/ro1': { ok: true, body: detail({ status: 'InProgress' }) },
      '/repair-orders/ro1/lines': { ok: true, body: detail({ status: 'InProgress' }) },
      '/parts': { ok: true, body: page([brakePads]) },
      '/staff': noStaff,
    });
  }

  async function openPartLine() {
    renderWorkshop();
    await openJob();
    await userEvent.selectOptions(screen.getByLabelText('What'), 'Part');
  }

  function lineBody(): Record<string, unknown> {
    const call = [...apiCalls()]
      .reverse()
      .find((c) => c.path === '/repair-orders/ro1/lines' && c.init?.method === 'POST');

    expect(call, 'the line was never sent').toBeDefined();
    return JSON.parse(call!.init!.body as string) as Record<string, unknown>;
  }

  it('offers the catalogue only once the line is a part', async () => {
    mockWithCatalogue();
    renderWorkshop();
    await openJob();

    expect(screen.queryByLabelText('Off the shelf')).not.toBeInTheDocument();

    await userEvent.selectOptions(screen.getByLabelText('What'), 'Part');

    expect(await screen.findByLabelText('Off the shelf')).toBeVisible();
  });

  it('sends the part and the quantity, so it comes off the shelf at cost', async () => {
    mockWithCatalogue();
    await openPartLine();

    await userEvent.selectOptions(await screen.findByLabelText('Off the shelf'), 'p1');
    await userEvent.clear(screen.getByLabelText('How many'));
    await userEvent.type(screen.getByLabelText('How many'), '2');
    await userEvent.type(screen.getByLabelText('Amount'), '68.40');
    await userEvent.click(screen.getByRole('button', { name: 'Write it up' }));

    expect(lineBody()).toMatchObject({
      kind: 'Part',
      partId: 'p1',
      partQuantity: 2,
      unitAmount: 68.4,
    });
  });

  it('fills the description from the catalogue, and leaves it editable', async () => {
    mockWithCatalogue();
    await openPartLine();

    await userEvent.selectOptions(await screen.findByLabelText('Off the shelf'), 'p1');

    expect(screen.getByLabelText('Description')).toHaveValue('Front brake pads');
  });

  it('says what is on the shelf, so nobody bills two of a part there is one of', async () => {
    mockWithCatalogue();
    await openPartLine();

    await userEvent.selectOptions(await screen.findByLabelText('Off the shelf'), 'p1');

    expect(screen.getByText(/BP-4471: 12 on the shelf/)).toBeVisible();
  });

  it('still bills a one-off item nobody stocks', async () => {
    // Free text stays a legitimate choice, not a shortcut: something bought for
    // a single job never enters the catalogue and still has to be billable.
    mockWithCatalogue();
    await openPartLine();

    await screen.findByLabelText('Off the shelf');
    await userEvent.type(screen.getByLabelText('Description'), 'Special-order trim clip');
    await userEvent.type(screen.getByLabelText('Amount'), '12');
    await userEvent.click(screen.getByRole('button', { name: 'Write it up' }));

    const body = lineBody();
    expect(body.partId).toBeNull();
    expect(body.partQuantity).toBeNull();
    expect(body.description).toBe('Special-order trim clip');
  });

  it('writes the job up anyway when the catalogue will not load', async () => {
    mockApi({
      '/appointments': noDiary,
      '/repair-orders': { ok: true, body: page([summary()]) },
      '/repair-orders/ro1': { ok: true, body: detail({ status: 'InProgress' }) },
      '/repair-orders/ro1/lines': { ok: true, body: detail({ status: 'InProgress' }) },
      '/parts': { ok: false, status: 403, code: 'parts.forbidden', detail: 'No.' },
      '/staff': noStaff,
    });
    await openPartLine();

    // The picker is there with only the free-text choice in it, and the line
    // still goes on. A catalogue that will not load must not stop the workshop.
    expect(await screen.findByLabelText('Off the shelf')).toBeVisible();
    await userEvent.type(screen.getByLabelText('Description'), 'Oil filter');
    await userEvent.type(screen.getByLabelText('Amount'), '9');
    await userEvent.click(screen.getByRole('button', { name: 'Write it up' }));

    expect(lineBody().description).toBe('Oil filter');
  });
});
