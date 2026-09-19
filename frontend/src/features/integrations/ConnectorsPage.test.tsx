// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ConnectorsPage.test — the screen that had to exist before a certification
//   could be shown honestly.
//
// Usage:
//   npm test.
//
// Coding Instructions:
//   Two tests here are the point and the rest are ordinary.
//
//   "says what a fixture-tested connector actually promises" is why the screen
//   exists. CertificationStatus had four levels and no reader; a status nobody
//   can see cannot be shown honestly.
//
//   "never shows the held values" guards ADR-022. The payload is a customer's
//   name, address and telephone number as a provider sent them. If somebody
//   ever adds the fields to the list "so you can see what is wrong with it",
//   that test fails, and it should.

import { render, screen, within } from '../../test/render';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ConnectorsPage } from './ConnectorsPage';
import { apiCalls, mockApi } from '../../test/setup';
import type { ConnectorSummary, QuarantineEntry } from '../../shared/contracts';

const fixture: ConnectorSummary = {
  provider: 'Fixture',
  version: '1.0',
  certification: 'FixtureTested',
  capabilities: ['Customers v1', 'Deals v1'],
  knownLimitations: ['Serves fabricated records. Never certify anything against this.'],
};

const held: QuarantineEntry = {
  id: 'q1',
  connector: 'Fixture',
  rooftopId: 'r1',
  contract: 'Customers',
  version: 1,
  externalId: 'PROV-0003',
  reasonCode: 'customers.missing_name',
  reasonDetail: 'A surname is required.',
  quarantinedAt: '2026-09-10T09:00:00Z',
  expiresAt: '2026-12-09T09:00:00Z',
};

function arrange(connectors: ConnectorSummary[] = [fixture], queue: QuarantineEntry[] = [held]) {
  mockApi({
    '/integrations/connectors': { ok: true, body: connectors },
    '/integrations/quarantine': { ok: true, body: queue },
  });
}

describe('the connectors screen', () => {
  it('says what a fixture-tested connector actually promises', async () => {
    arrange();
    render(<ConnectorsPage />);

    expect(await screen.findByText('Fixture')).toBeVisible();
    expect(screen.getByText('Fixture tested')).toBeVisible();

    // The level alone means nothing to a reader. What it PROMISES is the point.
    expect(
      screen.getByText(/not a promise that it works against a real provider/i),
    ).toBeVisible();

    // And the connector's own warning, which is the most important sentence here.
    expect(
      screen.getByText(/Never certify anything against this/),
    ).toBeVisible();
  });

  it('never shows the held values, only why they were held', async () => {
    // ADR-022. The reason is ours to show; the payload is somebody's personal
    // details and stays on the server.
    arrange();
    render(<ConnectorsPage />);

    const table = await screen.findByRole('table');
    expect(within(table).getByText('A surname is required.')).toBeVisible();
    expect(within(table).getByText('PROV-0003')).toBeVisible();

    // Nothing on this screen asked for a payload, and the contract has no field
    // for one — so there is nothing to render even by accident.
    expect(JSON.stringify(held)).not.toContain('payload');
  });

  it('runs a held record again and says it applied', async () => {
    mockApi({
      '/integrations/connectors': { ok: true, body: [fixture] },
      '/integrations/quarantine': [
        { ok: true, body: [held] },
        { ok: true, body: [] },
      ],
      '/integrations/quarantine/q1/replay': {
        ok: true,
        body: { applied: true, reasonCode: null, reasonDetail: null },
      },
    });
    render(<ConnectorsPage />);

    await userEvent.click(await screen.findByRole('button', { name: 'Run it again' }));

    expect(await screen.findByText(/applied this time/)).toBeVisible();
    expect(await screen.findByText('Nothing is being held back.')).toBeVisible();
  });

  it('says plainly when a replay is refused a second time', async () => {
    // A refusal is the ANSWER, not a failure of the request. Reporting it as an
    // error would hide the one thing replay is for: finding out.
    mockApi({
      '/integrations/connectors': { ok: true, body: [fixture] },
      '/integrations/quarantine': { ok: true, body: [held] },
      '/integrations/quarantine/q1/replay': {
        ok: true,
        body: {
          applied: false,
          reasonCode: 'customers.unknown_kind',
          reasonDetail: 'That is not a customer kind.',
        },
      },
    });
    render(<ConnectorsPage />);

    await userEvent.click(await screen.findByRole('button', { name: 'Run it again' }));

    expect(await screen.findByText(/refused again: That is not a customer kind\./)).toBeVisible();
    expect(screen.getByRole('table')).toBeVisible();
  });

  it('will not dismiss a record without a reason', async () => {
    // The server refuses a blank note. The screen must not send one either, or
    // a person gets an error for pressing cancel.
    arrange();
    vi.spyOn(window, 'prompt').mockReturnValue('   ');

    render(<ConnectorsPage />);
    await userEvent.click(await screen.findByRole('button', { name: 'It will never apply' }));

    expect(apiCalls().some((c) => c.path.includes('/dismiss'))).toBe(false);
  });

  it('says so plainly when nothing is held back', async () => {
    arrange([fixture], []);
    render(<ConnectorsPage />);

    expect(await screen.findByText('Nothing is being held back.')).toBeVisible();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('says plainly when the caller may not see the edge', async () => {
    mockApi({
      '/integrations/connectors': { ok: false, status: 403, code: 'integration.forbidden', detail: 'No.' },
      '/integrations/quarantine': { ok: false, status: 403, code: 'integration.forbidden', detail: 'No.' },
    });
    render(<ConnectorsPage />);

    expect(await screen.findByRole('alert')).toHaveTextContent(/do not have access/i);
  });
});
