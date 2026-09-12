// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   PartsPage.test — the catalogue screen and the manager's costing control.
//
// Usage:
//   npm test.
//
// Coding Instructions:
//   The test that matters most is
//   "says the costing choice applies to future sales only". A manager who
//   believes switching to FIFO restates last month has been misled by the
//   control, and the system cannot do it anyway — the ledger is immutable
//   and a sold line's cost is frozen. That sentence is a safeguard, not
//   decoration.

import { render, screen, within } from '../../test/render';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router';
import { PartsPage } from './PartsPage';
import { apiCalls, mockApi, page } from '../../test/setup';
import { setCurrentTenant } from '../../shared/api';
import type { PartDetail, PartSummary, PartsCostingSetting } from '../../shared/contracts';

const costing: PartsCostingSetting = {
  method: 'MovingAverage',
  options: [
    { method: 'MovingAverage', name: 'Moving average', explanation: 'The average cost of what is on the shelf.' },
    { method: 'LastCost', name: 'Last cost paid', explanation: 'Whatever the most recent delivery cost.' },
    { method: 'Fifo', name: 'Oldest stock first (FIFO)', explanation: 'Costs each sale against the oldest delivery.' },
  ],
};

const organization = {
  ok: true as const,
  body: { legalEntities: [{ rooftops: [{ id: 'r1', name: 'Northgate', code: 'NAG-01' }] }] },
};

const summary: PartSummary = {
  id: 'p1',
  partNumber: 'MZ690411',
  description: 'Front brake pad set',
  rooftopId: 'r1',
  quantityOnHand: 18,
  unitCost: 7,
  currency: 'USD',
};

const detail: PartDetail = {
  id: 'p1',
  partNumber: 'MZ690411',
  description: 'Front brake pad set',
  costingMethod: 'MovingAverage',
  stock: [
    {
      rooftopId: 'r1',
      quantityOnHand: 18,
      unitCost: 7,
      currency: 'USD',
      layers: [
        {
          id: 'l1',
          quantityReceived: 10,
          remainingQuantity: 8,
          unitCost: 5,
          receivedAt: '2026-08-01T09:00:00Z',
          reference: 'DN-1001',
        },
        {
          id: 'l2',
          quantityReceived: 10,
          remainingQuantity: 10,
          unitCost: 9,
          receivedAt: '2026-08-03T09:00:00Z',
          reference: 'DN-1002',
        },
      ],
    },
  ],
};

function renderParts() {
  setCurrentTenant('northgroup');

  return render(
    <MemoryRouter initialEntries={['/parts']}>
      <PartsPage />
    </MemoryRouter>,
  );
}

function withParts(parts: PartSummary[]) {
  mockApi({
    '/parts/costing': { ok: true, body: costing },
    '/parts/p1': { ok: true, body: detail },
    '/parts': { ok: true, body: page(parts) },
    '/organization': organization,
  });
}

describe('the parts catalogue', () => {
  it('shows what is on the shelf and what it costs', async () => {
    withParts([summary]);
    renderParts();

    expect(await screen.findByRole('button', { name: 'MZ690411' })).toBeVisible();
    expect(screen.getByText('Front brake pad set')).toBeVisible();
    expect(screen.getByText('18')).toBeVisible();
    expect(screen.getByText('$7.00')).toBeVisible();
  });

  it('marks a part that has run out rather than showing a bare zero', async () => {
    withParts([{ ...summary, quantityOnHand: 0 }]);
    renderParts();

    expect(await screen.findByText('None')).toBeVisible();
  });

  it('says plainly when the caller may not see parts', async () => {
    mockApi({
      '/parts/costing': { ok: false, status: 403, code: 'parts.forbidden', detail: 'No.' },
      '/parts': { ok: false, status: 403, code: 'parts.forbidden', detail: 'No.' },
      '/organization': organization,
    });
    renderParts();

    expect(await screen.findByText(/do not have access to parts/i)).toBeVisible();
  });

  it('keeps the wide table scrolling inside its own box', async () => {
    withParts([summary]);
    const { container } = renderParts();
    await screen.findByRole('button', { name: 'MZ690411' });

    expect(container.querySelector('.scroll > table')).not.toBeNull();
  });
});

describe('how parts are costed', () => {
  it('offers every method the server knows, and explains the current one', async () => {
    withParts([summary]);
    renderParts();

    const method = await screen.findByLabelText('Method');
    expect(within(method).getByRole('option', { name: 'Moving average' })).toBeInTheDocument();
    expect(within(method).getByRole('option', { name: 'Oldest stock first (FIFO)' })).toBeInTheDocument();
    expect(screen.getByText(/average cost of what is on the shelf/i)).toBeVisible();
  });

  it('says the costing choice applies to future sales only', async () => {
    // A manager who thinks switching restates last month has been misled by the
    // control. It cannot: the ledger is immutable and a sold line's cost is frozen.
    withParts([summary]);
    renderParts();

    expect(await screen.findByText(/future sales only/i)).toBeVisible();
    expect(screen.getByText(/keeps the cost it was sold at/i)).toBeVisible();
  });

  it('sends the chosen method to the server', async () => {
    mockApi({
      '/parts/costing': [
        { ok: true, body: costing },
        { ok: true, body: { ...costing, method: 'Fifo' } },
        { ok: true, body: { ...costing, method: 'Fifo' } },
      ],
      '/parts/p1': { ok: true, body: detail },
      '/parts': { ok: true, body: page([summary]) },
      '/organization': organization,
    });
    renderParts();

    await userEvent.selectOptions(await screen.findByLabelText('Method'), 'Fifo');

    const call = apiCalls().find((c) => c.path === '/parts/costing' && c.init?.method === 'POST');
    expect(JSON.parse(String(call?.init?.body))).toEqual({ method: 'Fifo' });
  });

  it('shows the refusal when the caller may not change it', async () => {
    mockApi({
      '/parts/costing': [
        { ok: true, body: costing },
        { ok: false, status: 403, code: 'parts.costing_forbidden', detail: 'This needs organization-wide permission.' },
      ],
      '/parts/p1': { ok: true, body: detail },
      '/parts': { ok: true, body: page([summary]) },
      '/organization': organization,
    });
    renderParts();

    await userEvent.selectOptions(await screen.findByLabelText('Method'), 'Fifo');

    expect(await screen.findByText(/organization-wide permission/i)).toBeVisible();
  });
});

describe('one part', () => {
  it('shows the deliveries behind the cost', async () => {
    // "Why does this cost that?" is answered by the layers, not by a number
    // somebody has to trust.
    withParts([summary]);
    renderParts();

    await userEvent.click(await screen.findByRole('button', { name: 'MZ690411' }));

    expect(await screen.findByText('DN-1001')).toBeVisible();
    expect(screen.getByText('DN-1002')).toBeVisible();
    expect(screen.getByRole('heading', { name: /NAG-01 — 18 on hand at \$7\.00 each/ })).toBeVisible();
  });

  it('books a delivery in against a named shelf', async () => {
    mockApi({
      '/parts/costing': { ok: true, body: costing },
      '/parts/p1/receipts': { ok: true, body: detail },
      '/parts/p1': { ok: true, body: detail },
      '/parts': { ok: true, body: page([summary]) },
      '/organization': organization,
    });
    renderParts();

    await userEvent.click(await screen.findByRole('button', { name: 'MZ690411' }));
    await userEvent.type(await screen.findByLabelText('How many'), '10');
    await userEvent.type(screen.getByLabelText('Cost each'), '5.50');
    await userEvent.type(screen.getByLabelText('Delivery note'), 'DN-1003');
    await userEvent.click(screen.getByRole('button', { name: 'Book it in' }));

    const call = apiCalls().find((c) => c.path === '/parts/p1/receipts');
    expect(JSON.parse(String(call?.init?.body))).toEqual({
      quantity: 10,
      unitCost: 5.5,
      rooftopId: 'r1',
      reference: 'DN-1003',
    });
  });
});
