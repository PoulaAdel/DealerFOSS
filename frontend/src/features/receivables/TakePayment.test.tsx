// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   TakePayment.test — the band that finally lets a bill be paid.
//
// Usage:
//   npm test
//
// Coding Instructions:
//   The valuable assertions are the two that keep the sub-ledger honest: what
//   is sent to the server, and that nothing is computed locally. If a future
//   change starts subtracting the typed amount to avoid a round trip, the
//   "server owns the arithmetic" test is what should fail.

import { render, screen } from '../../test/render';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { TakePayment } from './TakePayment';
import { apiCalls, mockApi, page } from '../../test/setup';
import type { ReceivableDetail } from '../../shared/contracts';

const receivable: ReceivableDetail = {
  id: '77777777-7777-7777-7777-777777777777',
  rooftopId: '22222222-2222-2222-2222-222222222222',
  customerId: '33333333-3333-3333-3333-333333333333',
  customerName: 'Ashgrove Couriers Ltd',
  source: 'RepairOrder',
  reference: '44444444-4444-4444-4444-444444444444',
  amount: 190,
  paid: 0,
  outstanding: 190,
  currency: 'USD',
  billedAt: '2026-09-01T10:00:00Z',
  daysOutstanding: 9,
  isSettled: false,
  payments: [],
  creditsRaised: [],
};

const path = `/receivables/for/RepairOrder/${receivable.reference}`;

function renderBand() {
  return render(<TakePayment source="RepairOrder" reference={receivable.reference} />);
}

describe('taking a payment', () => {
  it('shows nothing at all when there is no bill yet', async () => {
    // A job still on the ramp is not owed. The server answers 204 and the band
    // must not draw an empty "nothing owed" panel on every open job.
    mockApi({ [path]: { ok: true, status: 204 } });
    const { container } = renderBand();

    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(container).toBeEmptyDOMElement();
  });


  it('looks again when the thing it is attached to changes', async () => {
    // The defect this guards against was found by walking the screen, not by a
    // test. The band looked its receivable up once, when the job was opened and
    // still on the ramp, got "nothing is owed", and never looked again — so
    // invoicing in that same session showed no band at all. It rendered
    // perfectly on a fresh page load, which is the one place nobody was looking.
    mockApi({
      [path]: [
        { ok: true, status: 204 },
        { ok: true, body: receivable },
      ],
    });

    const { rerender } = render(
      <TakePayment source="RepairOrder" reference={receivable.reference} watch="Completed" />,
    );

    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(screen.queryByText('Still owed')).not.toBeInTheDocument();

    rerender(
      <TakePayment source="RepairOrder" reference={receivable.reference} watch="Invoiced" />,
    );

    expect(await screen.findByText('Still owed')).toBeVisible();
  });

  it('says what is billed, what is paid and what is still owed', async () => {
    // Part-paid on purpose: with nothing paid all three figures are the same
    // number and the test cannot tell whether the right one is in the right row.
    mockApi({
      [path]: { ok: true, body: { ...receivable, paid: 40, outstanding: 150 } },
    });
    renderBand();

    const rowFor = (term: string) =>
      screen.getByText(term).nextElementSibling as HTMLElement;

    expect(await screen.findByText('Still owed')).toBeVisible();
    expect(rowFor('Billed')).toHaveTextContent('$190.00');
    expect(rowFor('Paid so far')).toHaveTextContent('$40.00');
    expect(rowFor('Still owed')).toHaveTextContent('$150.00');
    expect(screen.getByText('Owed for 9 days.')).toBeVisible();
  });

  it('sends the amount, the method and the reference', async () => {
    mockApi({
      [path]: { ok: true, body: receivable },
      [`/receivables/${receivable.id}/payments`]: {
        ok: true,
        body: { ...receivable, paid: 190, outstanding: 0, isSettled: true },
      },
    });
    renderBand();

    await userEvent.type(await screen.findByLabelText('How much'), '190');
    await userEvent.selectOptions(screen.getByLabelText('How they paid'), 'Card');
    await userEvent.type(screen.getByLabelText('Reference (goes on the record)'), 'Auth 4471');
    await userEvent.click(screen.getByRole('button', { name: 'Record the payment' }));

    const call = [...apiCalls()]
      .reverse()
      .find((c) => c.path === `/receivables/${receivable.id}/payments`);

    expect(call, 'the payment was never sent').toBeDefined();
    expect(JSON.parse(call!.init!.body as string)).toEqual({
      amount: 190,
      method: 'Card',
      note: 'Auth 4471',
    });
  });

  it('lets the server own the arithmetic rather than working it out here', async () => {
    // The server's answer here deliberately does NOT equal 190 - 50. A colleague
    // can take a payment at the till a second earlier, and the figure that comes
    // back is derived from every payment on the row, not from the one just typed.
    //
    // The first version of this test used 140, which is exactly what local
    // subtraction produces — so a rehearsal that replaced the server's reply with
    // `outstanding - typed` passed, and the test was proving nothing.
    mockApi({
      [path]: { ok: true, body: receivable },
      [`/receivables/${receivable.id}/payments`]: {
        ok: true,
        body: { ...receivable, paid: 65, outstanding: 125, isSettled: false },
      },
    });
    renderBand();

    await userEvent.type(await screen.findByLabelText('How much'), '50');
    await userEvent.click(screen.getByRole('button', { name: 'Record the payment' }));

    expect(await screen.findByText('$125.00')).toBeVisible();
    expect(screen.queryByText('$140.00')).not.toBeInTheDocument();
  });

  it('still refuses nothing at all, and zero', async () => {
    // What replaced "will not offer to send more than is outstanding" on
    // 2026-09-14. More than is outstanding is now a real thing a customer does
    // and is taken; an empty box and a zero are still not payments.
    mockApi({ [path]: { ok: true, body: receivable } });
    renderBand();

    expect(await screen.findByRole('button', { name: 'Record the payment' })).toBeDisabled();

    await userEvent.type(screen.getByLabelText('How much'), '0');
    expect(screen.getByRole('button', { name: 'Record the payment' })).toBeDisabled();
  });

  it('offers no form once it is settled, and says so', async () => {
    mockApi({
      [path]: {
        ok: true,
        body: {
          ...receivable,
          paid: 190,
          outstanding: 0,
          isSettled: true,
          daysOutstanding: 0,
          payments: [{
            id: '88888888-8888-8888-8888-888888888888',
            amount: 190,
            currency: 'USD',
            method: 'Card',
            receivedAt: '2026-09-03T11:00:00Z',
            note: 'Auth 4471',
          }],
        },
      },
    });
    renderBand();

    expect(await screen.findByText('Paid in full. Nothing more is owed on this.')).toBeVisible();
    expect(screen.queryByRole('button', { name: 'Record the payment' })).not.toBeInTheDocument();

    // The receipt is still findable. "Paid in full on the 3rd, by card" is the
    // answer somebody wants at the counter.
    expect(screen.getByText(/Card/)).toBeVisible();
    expect(screen.getByText(/Auth 4471/)).toBeVisible();
  });

  it('reports a refusal instead of pretending the money arrived', async () => {
    mockApi({
      [path]: { ok: true, body: receivable },
      [`/receivables/${receivable.id}/payments`]: {
        ok: false,
        status: 409,
        code: 'receivables.already_settled',
        detail: 'This has been paid in full already.',
      },
    });
    renderBand();

    await userEvent.type(await screen.findByLabelText('How much'), '10');
    await userEvent.click(screen.getByRole('button', { name: 'Record the payment' }));

    expect(await screen.findByRole('alert')).toBeVisible();
  });

  it('says what will happen to the extra BEFORE the money is taken', async () => {
    // The band used to disable the button on anything over the outstanding
    // amount, which was the wrong answer to a real situation: a customer paying
    // a $190 bill with $200 has not made a mistake. Saying so while they are
    // still standing there is the point — after the fact it is just news.
    mockApi({ [path]: { ok: true, body: receivable } });
    renderBand();

    await userEvent.type(await screen.findByLabelText('How much'), '200');

    expect(await screen.findByRole('status')).toHaveTextContent(
      /\$10\.00 more than is owed/);

    expect(screen.getByRole('button', { name: 'Record the payment' })).toBeEnabled();
  });

  it('shows the money still owed back, and hands it over', async () => {
    const credit = {
      id: '99999999-9999-9999-9999-999999999999',
      rooftopId: receivable.rooftopId,
      customerId: receivable.customerId,
      customerName: receivable.customerName,
      amount: 10,
      spent: 0,
      remaining: 10,
      currency: 'USD',
      reference: receivable.reference,
      raisedAt: '2026-09-03T11:00:00Z',
      isSpent: false,
      uses: [],
    };

    const settled = {
      ...receivable,
      paid: 190,
      outstanding: 0,
      isSettled: true,
      daysOutstanding: 0,
      creditsRaised: [credit],
    };

    mockApi({
      [path]: { ok: true, body: settled },
      '/receivables/credits': { ok: true, body: page([credit]) },
      [`/receivables/credits/${credit.id}/refund`]: {
        ok: true,
        body: { ...credit, spent: 10, remaining: 0, isSpent: true },
      },
    });

    renderBand();

    // A settled bill quietly holding somebody's change is exactly the state
    // this is here to make visible, so it shows even though nothing is owed.
    expect(await screen.findByText('Owed back to this customer')).toBeVisible();

    await userEvent.click(screen.getByRole('button', { name: 'Give it back' }));

    const refund = apiCalls().find((call) => call.path.endsWith('/refund'));
    expect(refund?.init?.method).toBe('POST');
    expect(JSON.parse(String(refund?.init?.body))).toMatchObject({
      amount: 10,
      method: 'BankTransfer',
    });
  });

  it('offers a credit the customer already has against the bill in front of you', async () => {
    // The credit came from something else entirely — a service invoice they
    // overpaid weeks ago. It is only useful if it turns up on the bill being
    // settled today rather than on the one that created it.
    const credit = {
      id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      rooftopId: receivable.rooftopId,
      customerId: receivable.customerId,
      customerName: receivable.customerName,
      amount: 300,
      spent: 0,
      remaining: 300,
      currency: 'USD',
      reference: 'RO-1084',
      raisedAt: '2026-08-20T11:00:00Z',
      isSpent: false,
      uses: [],
    };

    mockApi({
      [path]: { ok: true, body: receivable },
      '/receivables/credits': { ok: true, body: page([credit]) },
      [`/receivables/credits/${credit.id}/apply`]: { ok: true, body: credit },
    });

    renderBand();

    // Capped at what the bill can absorb, not at what the credit holds: $300 of
    // credit against a $190 bill is $190, and offering to apply all of it would
    // promise something the server would refuse.
    const use = await screen.findByRole('button', { name: /\$190\.00/ });
    await userEvent.click(use);

    const applied = apiCalls().find((call) => call.path.endsWith('/apply'));
    expect(JSON.parse(String(applied?.init?.body))).toMatchObject({
      receivableId: receivable.id,
      amount: 190,
    });
  });
});
