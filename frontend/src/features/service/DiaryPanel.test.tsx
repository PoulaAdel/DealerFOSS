// DiaryPanel.test — the cars that are coming, and the one click that turns a
// promise into a job.
//
// Use:  npm test.
// Edit: the test that matters most is "marking a car in opens the job and hands
//       it straight over". That single call is the reconciliation point of the
//       whole capability (roadmap I5) — if the screen ever splits it into "mark
//       arrived" then "open a job", the two can disagree, and the diary stops
//       being able to account for what the workshop did.

import { render, screen, waitForElementToBeRemoved } from '../../test/render';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { DiaryPanel } from './DiaryPanel';
import { apiCalls, mockApi } from '../../test/setup';
import type { AppointmentView, Diary } from '../../shared/contracts';

const booking = (over: Partial<AppointmentView> = {}): AppointmentView => ({
  id: 'a1',
  rooftopId: 'r1',
  scheduledFor: '2026-08-12T09:00:00Z',
  estimatedHours: 2,
  status: 'Scheduled',
  customerId: 'c1',
  customerName: 'Daniel Okafor',
  vehicleId: 'v1',
  vehicle: '2019 Honda Civic EX',
  reason: 'Annual service',
  advisorUserId: null,
  repairOrderId: null,
  repairOrderNumber: null,
  arrivedAt: null,
  outcome: null,
  isOpen: true,
  ...over,
});

const diary = (over: Partial<Diary> = {}): Diary => ({
  appointments: [booking()],
  load: [{ date: '2026-08-12', expected: 1, bookedHours: 2, unestimated: 0 }],
  ...over,
});

function show(body: Diary = diary(), extra: Record<string, unknown> = {}) {
  mockApi({ '/appointments?openOnly=true&limit=100': { ok: true, body }, ...extra });
  return render(<DiaryPanel onArrived={() => {}} />);
}

describe('the service diary', () => {
  it('lists the cars that are expected', async () => {
    show();

    expect(await screen.findByText('Annual service')).toBeVisible();
    expect(screen.getByText('Daniel Okafor')).toBeVisible();
    expect(screen.getByText('2019 Honda Civic EX')).toBeVisible();
  });

  it('says what the day is carrying, which is the only question a diary answers', async () => {
    show(
      diary({
        appointments: [booking(), booking({ id: 'a2', estimatedHours: 3.5 })],
        load: [{ date: '2026-08-12', expected: 2, bookedHours: 5.5, unestimated: 0 }],
      }),
    );

    // Straight from the server. A browser that summed the rows itself would be a
    // second copy of the rule about which bookings count.
    expect(await screen.findByText(/2 cars, 5.5 h of work/)).toBeVisible();
  });

  it('never reports a day with an unestimated car on it as empty', async () => {
    show(
      diary({
        appointments: [booking({ estimatedHours: null })],
        load: [{ date: '2026-08-12', expected: 1, bookedHours: 0, unestimated: 1 }],
      }),
    );

    // "1 car, 0 h of work" reads as a free day. It is the one thing this figure
    // must never say by mistake, because a manager takes a booking on it.
    expect(await screen.findByText(/1 car, 0 h booked and 1 not estimated/)).toBeVisible();
    expect(screen.queryByText(/0 h of work/)).not.toBeInTheDocument();
  });

  it('shows a car nobody estimated as unestimated, not as zero', async () => {
    show(diary({ appointments: [booking({ estimatedHours: null })] }));

    // Nobody estimated is a different fact from estimating nothing, and a
    // workshop planning its week needs to be able to tell them apart.
    expect(await screen.findByText('Not estimated')).toBeVisible();
  });

  it('marking a car in opens the job and hands it straight over', async () => {
    const arrived = vi.fn();

    mockApi({
      '/appointments?openOnly=true&limit=100': { ok: true, body: diary() },
      '/appointments/a1/arrive': {
        ok: true,
        body: {
          appointment: booking({ status: 'Arrived', repairOrderId: 'ro9', repairOrderNumber: 'RO-1009', isOpen: false }),
          repairOrder: { id: 'ro9', number: 'RO-1009', status: 'Booked' },
        },
      },
    });

    render(<DiaryPanel onArrived={arrived} />);

    await userEvent.click(await screen.findByRole('button', { name: 'It’s here' }));

    // One call, not two. The server opens the job and links the booking in one
    // transaction; offering it as two steps here would be a lie about that.
    const calls = apiCalls().filter((call) => call.path.includes('/arrive'));
    expect(calls).toHaveLength(1);

    // And the job goes straight to the caller, because the car is at the counter
    // and writing up what it came in for is the very next thing anybody does.
    expect(arrived).toHaveBeenCalledWith(
      expect.objectContaining({ id: 'ro9', number: 'RO-1009' }),
    );
  });

  it('shows the job a booking became, rather than just saying it arrived', async () => {
    show(
      diary({
        appointments: [
          booking({ status: 'Arrived', repairOrderId: 'ro9', repairOrderNumber: 'RO-1009', isOpen: false }),
        ],
        load: [],
      }),
    );

    // The link is the whole point: a diary that only said "Arrived" could not be
    // reconciled against what the workshop actually did.
    expect(await screen.findByText('Job RO-1009')).toBeVisible();
    expect(screen.queryByRole('button', { name: 'It’s here' })).not.toBeInTheDocument();
  });

  it('records a car that did not come rather than removing it', async () => {
    mockApi({
      '/appointments?openOnly=true&limit=100': { ok: true, body: diary() },
      '/appointments/a1/close': { ok: true, body: booking({ status: 'NoShow', isOpen: false }) },
    });

    render(<DiaryPanel onArrived={() => {}} />);
    await userEvent.click(await screen.findByRole('button', { name: 'Did not come' }));

    // `cancelled: false` is the distinction that matters — silence, not the
    // customer ringing. Collapsing the two loses the figure a service manager
    // uses to decide who to remind the day before.
    const close = apiCalls().find((call) => call.path.includes('/close'));
    expect(JSON.parse(String(close?.init?.body))).toEqual({ cancelled: false });
  });

  it('re-estimates in place, without opening a form', async () => {
    mockApi({
      '/appointments?openOnly=true&limit=100': { ok: true, body: diary() },
      '/appointments/a1/reschedule': { ok: true, status: 204 },
    });

    render(<DiaryPanel onArrived={() => {}} />);

    // The value is a BUTTON, not a div with a click handler — so it is reachable
    // by keyboard and announces what pressing it does. That is the difference
    // between inline editing and a mouse-only secret.
    const value = await screen.findByRole('button', { name: /Est\..*2.*change it/i });
    await userEvent.click(value);

    const box = screen.getByRole('textbox', { name: 'Est.' });
    await userEvent.clear(box);
    await userEvent.type(box, '4{Enter}');

    const sent = apiCalls().find((call) => call.path.includes('/reschedule'));
    expect(JSON.parse(String(sent?.init?.body))).toMatchObject({ estimatedHours: 4 });
  });

  it('keeps the booking’s time when only the hours change', async () => {
    mockApi({
      '/appointments?openOnly=true&limit=100': { ok: true, body: diary() },
      '/appointments/a1/reschedule': { ok: true, status: 204 },
    });

    render(<DiaryPanel onArrived={() => {}} />);
    await userEvent.click(await screen.findByRole('button', { name: /Est\..*change it/i }));

    const box = screen.getByRole('textbox', { name: 'Est.' });
    await userEvent.clear(box);
    await userEvent.type(box, '4{Enter}');

    // Reschedule carries both. Sending the existing instant back is what says
    // "only the estimate changed" rather than silently moving the car.
    const sent = apiCalls().find((call) => call.path.includes('/reschedule'));
    expect(JSON.parse(String(sent?.init?.body))).toMatchObject({
      scheduledFor: '2026-08-12T09:00:00Z',
    });
  });

  it('escape abandons the edit and sends nothing', async () => {
    show();

    await userEvent.click(await screen.findByRole('button', { name: /Est\..*change it/i }));

    const box = screen.getByRole('textbox', { name: 'Est.' });
    await userEvent.clear(box);
    await userEvent.type(box, '99{Escape}');

    // Somebody who starts typing in the wrong row needs a way out that is not
    // "work out what it said before".
    expect(await screen.findByRole('button', { name: /Est\..*2.*change it/i })).toBeVisible();
    expect(apiCalls().some((call) => call.path.includes('/reschedule'))).toBe(false);
  });

  it('puts the old value back when the server refuses', async () => {
    mockApi({
      '/appointments?openOnly=true&limit=100': { ok: true, body: diary() },
      '/appointments/a1/reschedule': {
        ok: false,
        status: 409,
        code: 'appointments.not_movable',
        detail: 'That time has already passed.',
      },
    });

    render(<DiaryPanel onArrived={() => {}} />);
    await userEvent.click(await screen.findByRole('button', { name: /Est\..*change it/i }));

    const box = screen.getByRole('textbox', { name: 'Est.' });
    await userEvent.clear(box);
    await userEvent.type(box, '4{Enter}');

    // The record did not change, so the screen must not imply it did by leaving
    // the typed value sitting there looking accepted.
    expect(await screen.findByRole('alert')).toHaveTextContent('That time has already passed.');
    expect(screen.getByRole('button', { name: /Est\..*2.*change it/i })).toBeVisible();

    // And reopening shows what the record actually holds, not the rejected
    // number. Without that the next edit starts from a value the server has
    // already refused, and saving it "unchanged" would look like a no-op.
    await userEvent.click(screen.getByRole('button', { name: /Est\..*change it/i }));
    expect(screen.getByRole('textbox', { name: 'Est.' })).toHaveValue('2');
  });

  it('will not let an arrived booking be re-estimated', async () => {
    show(
      diary({
        appointments: [
          booking({ status: 'Arrived', repairOrderId: 'ro9', repairOrderNumber: 'RO-1009', isOpen: false }),
        ],
        load: [],
      }),
    );

    // Its hours belong to the job now. Editing them here would change a figure
    // that no longer drives anything.
    const value = await screen.findByRole('button', { name: /Est\..*change it/i });
    expect(value).toBeDisabled();
  });

  it('says the diary is clear rather than showing an empty table', async () => {
    show(diary({ appointments: [], load: [] }));

    expect(await screen.findByText('Nothing booked in. The diary is clear.')).toBeVisible();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('stays out of the way for somebody who may not see the diary', async () => {
    mockApi({
      '/appointments?openOnly=true&limit=100': {
        ok: false,
        status: 403,
        code: 'appointments.forbidden',
        detail: 'No.',
      },
    });

    const { container } = render(<DiaryPanel onArrived={() => {}} />);

    // Plenty of people who can see the workshop have no business taking
    // bookings. A red panel would tell them they had done something wrong, so
    // the panel renders nothing at all once the refusal comes back.
    await waitForElementToBeRemoved(() => screen.queryByText('Loading the diary…'));

    expect(container).toBeEmptyDOMElement();
    expect(screen.queryByText('Coming in')).not.toBeInTheDocument();
  });

  it('offers a retry when the diary cannot be loaded at all', async () => {
    mockApi({
      '/appointments?openOnly=true&limit=100': {
        ok: false,
        status: 500,
        code: 'server_error',
        detail: 'The diary is unavailable.',
      },
    });

    render(<DiaryPanel onArrived={() => {}} />);

    expect(await screen.findByText('The diary is unavailable.')).toBeVisible();
    expect(screen.getByRole('button', { name: 'Try again' })).toBeVisible();
  });
});
