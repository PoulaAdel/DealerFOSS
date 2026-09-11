// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   RecordAnEntry.test — the only screen that chooses accounts directly.
//
// Usage:
//   npm test
//
// Coding Instructions:
//   The valuable assertions are the ones about what is refused BEFORE anything
//   is sent: an entry that does not balance, and one with no explanation. The
//   server refuses both anyway; the point is that the person typing finds out
//   while they can still fix it.

import { render, screen } from '../../test/render';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { RecordAnEntry } from './RecordAnEntry';
import { apiCalls, mockApi } from '../../test/setup';
import type { AccountView } from '../../shared/contracts';

const accounts: AccountView[] = [
  { id: 'a1', code: '1000', name: 'Cash', kind: 'Asset' },
  { id: 'a2', code: '6100', name: 'Rent and premises', kind: 'Expense' },
  { id: 'a3', code: '3000', name: "Owners' capital", kind: 'Equity' },
];

const organization = {
  id: 'o1',
  name: 'North Auto Group',
  legalEntities: [
    {
      id: 'e1',
      name: 'North Auto Group LLC',
      rooftops: [{ id: 'r1', code: 'NAG-01', name: 'North Auto Downtown' }],
    },
  ],
};

function mockChart() {
  mockApi({
    '/accounting/accounts': { ok: true, body: accounts },
    '/organization': { ok: true, body: organization },
    '/accounting/journal': { ok: true, body: { id: 'j1' } },
  });
}

/** Fills a balanced rent entry, the commonest thing this screen is for. */
async function fillRent() {
  await userEvent.type(await screen.findByLabelText('What it is for'), 'September rent');
  await userEvent.selectOptions(screen.getByLabelText('Account on line 1'), '6100');
  await userEvent.type(screen.getByLabelText('Debit on line 1'), '4500');
  await userEvent.selectOptions(screen.getByLabelText('Account on line 2'), '1000');
  await userEvent.type(screen.getByLabelText('Credit on line 2'), '4500');
}

describe('recording an entry by hand', () => {
  it('sends the lines the person chose', async () => {
    mockChart();
    render(<RecordAnEntry onPosted={() => {}} />);
    await fillRent();

    await userEvent.click(screen.getByRole('button', { name: 'Record it' }));

    const call = [...apiCalls()]
      .reverse()
      .find((c) => c.path === '/accounting/journal' && c.init?.method === 'POST');

    expect(call, 'the entry was never sent').toBeDefined();
    const body = JSON.parse(call!.init!.body as string) as Record<string, unknown>;

    expect(body.memo).toBe('September rent');
    expect(body.lines).toEqual([
      { accountCode: '6100', debit: 4500, credit: 0, memo: null },
      { accountCode: '1000', debit: 0, credit: 4500, memo: null },
    ]);
  });

  it('will not send an entry that does not balance, and says by how much', async () => {
    mockChart();
    render(<RecordAnEntry onPosted={() => {}} />);

    await userEvent.type(await screen.findByLabelText('What it is for'), 'Wrong on purpose');
    await userEvent.selectOptions(screen.getByLabelText('Account on line 1'), '6100');
    await userEvent.type(screen.getByLabelText('Debit on line 1'), '1000');
    await userEvent.selectOptions(screen.getByLabelText('Account on line 2'), '1000');
    await userEvent.type(screen.getByLabelText('Credit on line 2'), '900');

    expect(screen.getByText(/out by \$100\.00/)).toBeVisible();
    expect(screen.getByRole('button', { name: 'Record it' })).toBeDisabled();
  });

  it('will not send an entry nobody has explained', async () => {
    // The one somebody will be asked about in a year and nobody will be able to
    // answer.
    mockChart();
    render(<RecordAnEntry onPosted={() => {}} />);

    await userEvent.selectOptions(await screen.findByLabelText('Account on line 1'), '6100');
    await userEvent.type(screen.getByLabelText('Debit on line 1'), '4500');
    await userEvent.selectOptions(screen.getByLabelText('Account on line 2'), '1000');
    await userEvent.type(screen.getByLabelText('Credit on line 2'), '4500');

    expect(screen.getByRole('button', { name: 'Record it' })).toBeDisabled();
  });

  it('adds a line when a two-sided entry is not enough', async () => {
    mockChart();
    render(<RecordAnEntry onPosted={() => {}} />);

    await userEvent.click(await screen.findByRole('button', { name: 'Add a line' }));

    expect(screen.getByLabelText('Account on line 3')).toBeVisible();
  });

  it('tells the page it posted, so the balance behind it is refetched', async () => {
    const posted = vi.fn();
    mockChart();
    render(<RecordAnEntry onPosted={posted} />);
    await fillRent();

    await userEvent.click(screen.getByRole('button', { name: 'Record it' }));

    expect(posted).toHaveBeenCalled();
  });

  it('reports a refusal instead of pretending it posted', async () => {
    mockApi({
      '/accounting/accounts': { ok: true, body: accounts },
      '/organization': { ok: true, body: organization },
      '/accounting/journal': {
        ok: false,
        status: 403,
        code: 'accounting.forbidden',
        detail: 'You do not have access to this.',
      },
    });
    render(<RecordAnEntry onPosted={() => {}} />);
    await fillRent();

    await userEvent.click(screen.getByRole('button', { name: 'Record it' }));

    expect(await screen.findByRole('alert')).toBeVisible();
  });
});
