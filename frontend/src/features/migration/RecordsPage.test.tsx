// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   RecordsPage.test — the safeguard, the exception list, and the download.
//
// Usage:
//   npm test
//
// Coding Instructions:
//   The one worth guarding hardest is that **the real import is unreachable
//   until a practice run has finished on this exact file**. Somebody is
//   about to write thousands of rows into their own business's history. If
//   that lock ever comes off by accident, the mistake it prevents is one
//   nobody can undo.

import { render, screen, waitFor, within } from '../../test/render';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { RecordsPage } from './RecordsPage';
import { apiCalls, mockApi } from '../../test/setup';

// The download test replaces the global URL with a plain object so it can watch
// createObjectURL. Left in place it breaks every later test in this file with
// "URL is not a constructor", from inside jsdom's cookie jar rather than from
// anything the test did — which cost an afternoon the first time.
afterEach(() => vi.unstubAllGlobals());

const job = (over: Record<string, unknown> = {}) => ({
  id: 'j1', kind: 'Customers', mode: 'Trial', status: 'Completed',
  sourceName: 'people.csv', sourceHash: 'abc',
  rowsTotal: 3, rowsCreated: 2, rowsUpdated: 0, rowsSkipped: 0, rowsFailed: 1,
  queuedAt: '2026-08-04T09:00:00Z', startedAt: '2026-08-04T09:00:01Z',
  finishedAt: '2026-08-04T09:00:02Z', failureReason: null,
  ...over,
});

const problemRows = [
  {
    rowNumber: 4,
    raw: 'A1,,Nameless',
    outcome: 'Failed',
    message: 'This row has no last name or business name.',
  },
];

function csv(name = 'people.csv') {
  return new File(
    ['externalid,lastname\nA1,Smith\nA2,Jones\nA3,'],
    name,
    { type: 'text/csv' },
  );
}

describe('bringing records in', () => {
  it('will not let anybody import before running a practice', async () => {
    mockApi({});
    render(<RecordsPage />);

    // Nothing chosen: neither button does anything.
    expect(screen.getByRole('button', { name: 'Practice run' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Import for real' })).toBeDisabled();

    await userEvent.upload(screen.getByLabelText('The file'), csv());

    // A file is chosen, so a practice is possible — but the real run is not.
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Practice run' })).toBeEnabled(),
    );
    expect(screen.getByRole('button', { name: 'Import for real' })).toBeDisabled();
    expect(screen.getByText(/Run the practice first/)).toBeVisible();
  });

  it('unlocks the real import once the practice has finished', async () => {
    mockApi({
      '/migration/imports': { ok: true, body: job() },
      '/migration/imports/j1': { ok: true, body: job() },
      '/migration/imports/j1/rows': { ok: true, body: problemRows },
    });

    render(<RecordsPage />);
    await userEvent.upload(screen.getByLabelText('The file'), csv());
    await userEvent.click(screen.getByRole('button', { name: 'Practice run' }));

    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Import for real' })).toBeEnabled(),
    );
  });

  it('locks it again if a different file is chosen', async () => {
    mockApi({
      '/migration/imports': { ok: true, body: job() },
      '/migration/imports/j1': { ok: true, body: job() },
      '/migration/imports/j1/rows': { ok: true, body: problemRows },
    });

    render(<RecordsPage />);
    await userEvent.upload(screen.getByLabelText('The file'), csv('first.csv'));
    await userEvent.click(screen.getByRole('button', { name: 'Practice run' }));
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Import for real' })).toBeEnabled(),
    );

    await userEvent.upload(
      screen.getByLabelText('The file'),
      new File(['externalid,lastname\nB9,Different'], 'second.csv', { type: 'text/csv' }),
    );

    // A rehearsal of one file says nothing about another.
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Import for real' })).toBeDisabled(),
    );
  });

  it('says plainly that a practice wrote nothing', async () => {
    mockApi({
      '/migration/imports': { ok: true, body: job() },
      '/migration/imports/j1': { ok: true, body: job() },
      '/migration/imports/j1/rows': { ok: true, body: problemRows },
    });

    render(<RecordsPage />);
    await userEvent.upload(screen.getByLabelText('The file'), csv());
    await userEvent.click(screen.getByRole('button', { name: 'Practice run' }));

    expect(await screen.findByText(/Nothing has been written/)).toBeVisible();
    expect(screen.getByRole('status')).toHaveTextContent(/2 would be added/);
  });

  it('lists a refused row by its spreadsheet line, quoted exactly', async () => {
    mockApi({
      '/migration/imports': { ok: true, body: job() },
      '/migration/imports/j1': { ok: true, body: job() },
      '/migration/imports/j1/rows': { ok: true, body: problemRows },
    });

    render(<RecordsPage />);
    await userEvent.upload(screen.getByLabelText('The file'), csv());
    await userEvent.click(screen.getByRole('button', { name: 'Practice run' }));

    const table = await screen.findByRole('table');
    expect(within(table).getByText('4')).toBeVisible();
    expect(within(table).getByText(/no last name or business name/)).toBeVisible();

    // Quoted back as it arrived: fixing the source is the workflow, so somebody
    // has to be able to see what they actually sent.
    expect(within(table).getByText('A1,,Nameless')).toBeVisible();
  });

  it('sends the chosen kind and the file’s own text', async () => {
    mockApi({
      '/migration/imports': { ok: true, body: job({ kind: 'Vehicles' }) },
      '/migration/imports/j1': { ok: true, body: job({ kind: 'Vehicles' }) },
      '/migration/imports/j1/rows': { ok: true, body: [] },
    });

    render(<RecordsPage />);
    await userEvent.selectOptions(screen.getByLabelText('What is in the file'), 'Vehicles');
    await userEvent.upload(screen.getByLabelText('The file'), csv('cars.csv'));
    await userEvent.click(screen.getByRole('button', { name: 'Practice run' }));

    await screen.findByRole('status');

    const submitted = apiCalls().find(
      (c) => c.path === '/migration/imports' && c.init?.method === 'POST',
    );
    const body = JSON.parse(submitted!.init!.body as string) as Record<string, string>;

    expect(body.kind).toBe('Vehicles');
    expect(body.mode).toBe('Trial');
    expect(body.sourceName).toBe('cars.csv');
    expect(body.content).toContain('externalid,lastname');
  });

  it('shows the server’s refusal rather than a generic failure', async () => {
    mockApi({
      '/migration/imports': {
        ok: false, status: 400, code: 'migration.missing_columns',
        detail: 'Missing: modelyear, model.',
      },
    });

    render(<RecordsPage />);
    await userEvent.upload(screen.getByLabelText('The file'), csv());
    await userEvent.click(screen.getByRole('button', { name: 'Practice run' }));

    expect(await screen.findByText('Missing: modelyear, model.')).toBeVisible();
    expect(screen.getByRole('button', { name: 'Import for real' })).toBeDisabled();
  });

  it('waits for the background worker rather than reading a queued job', async () => {
    mockApi({
      '/migration/imports': { ok: true, body: job({ status: 'Queued' }) },
      // Queued, then running, then done — the screen must not believe the first.
      '/migration/imports/j1': [
        { ok: true, body: job({ status: 'Queued', rowsCreated: 0 }) },
        { ok: true, body: job({ status: 'Running', rowsCreated: 0 }) },
        { ok: true, body: job({ status: 'Completed' }) },
      ],
      '/migration/imports/j1/rows': { ok: true, body: problemRows },
    });

    render(<RecordsPage />);
    await userEvent.upload(screen.getByLabelText('The file'), csv());
    await userEvent.click(screen.getByRole('button', { name: 'Practice run' }));

    expect(await screen.findByRole('status', {}, { timeout: 5000 }))
      .toHaveTextContent(/2 would be added/);
  });
});

describe('taking records out', () => {
  it('downloads through the API so the dealership header is sent', async () => {
    // A plain link cannot carry X-Tenant, so the request would arrive without a
    // dealership and be refused. This is the whole reason it is a fetch.
    const createObjectURL = vi.fn(() => 'blob:x');
    vi.stubGlobal('URL', { ...URL, createObjectURL, revokeObjectURL: vi.fn() });

    mockApi({ '/migration/exports/Customers': { ok: true, body: 'externalid\nA1' } });

    render(<RecordsPage />);
    await userEvent.click(screen.getByRole('button', { name: 'Download customers' }));

    await waitFor(() => expect(createObjectURL).toHaveBeenCalled());

    const call = apiCalls().find((c) => c.path.startsWith('/migration/exports'));
    expect((call?.init?.headers as Record<string, string>)['X-Tenant']).toBeDefined();
  });
});

describe('moving a whole lot', () => {
  const organization = {
    id: 'o1',
    name: 'North Auto Group',
    slug: 'northgroup',
    legalEntities: [
      {
        id: 'e1',
        name: 'North Auto Inc',
        rooftops: [
          { id: 'r1', name: 'North Auto Downtown', code: 'NAG-01', timeZone: 'UTC', legalEntityId: 'e1' },
          { id: 'r2', name: 'North Auto Uptown', code: 'NAG-02', timeZone: 'UTC', legalEntityId: 'e1' },
        ],
      },
    ],
  };

  function packageFile(contents = '{"format":"dealerfoss.package","version":1}') {
    return new File([contents], 'nag-01-records.json', { type: 'application/json' });
  }

  it('asks for the lot by the name a person calls it, not by its id', async () => {
    mockApi({ '/organization': { ok: true, body: organization } });
    render(<RecordsPage />);

    const lots = await screen.findByLabelText('Which location');
    expect(within(lots).getByRole('option', { name: 'North Auto Uptown (NAG-02)' })).toBeInTheDocument();
  });

  it('sends the package to the lot the person chose, not the one inside the file', async () => {
    // The rooftop in the file belongs to the installation it came from and names
    // nothing here. If this ever starts reading it, records land in a lot that
    // does not exist.
    mockApi({
      '/organization': { ok: true, body: organization },
      '/migration/packages/r2': {
        ok: true,
        body: { rooftopId: 'r2', applied: 4, reused: 0, byKind: [], refused: [] },
      },
    });

    render(<RecordsPage />);
    await userEvent.selectOptions(await screen.findByLabelText('Which location'), 'r2');
    await userEvent.upload(screen.getByLabelText('A file from another installation'), packageFile());
    await userEvent.click(screen.getByRole('button', { name: 'Bring these records in' }));

    expect(await screen.findByRole('status')).toHaveTextContent('4 written, 0 already here, 0 not brought in.');

    const call = apiCalls().find((c) => c.init?.method === 'POST');
    expect(call?.path).toBe('/migration/packages/r2');
  });

  it('names every record it would not bring in, and why', async () => {
    // A count sends somebody to logs they do not have. The reason is the whole
    // value of the report.
    mockApi({
      '/organization': { ok: true, body: organization },
      '/migration/packages/r1': {
        ok: true,
        body: {
          rooftopId: 'r1',
          applied: 6,
          reused: 2,
          byKind: [],
          refused: [
            {
              kind: 'Deals',
              id: '8c888d54-0000-0000-0000-000000000000',
              reasonCode: 'deals.customer_not_found',
              reason: 'Record the customer before starting their deal.',
            },
          ],
        },
      },
    });

    render(<RecordsPage />);
    await userEvent.upload(
      await screen.findByLabelText('A file from another installation'),
      packageFile(),
    );
    await userEvent.click(screen.getByRole('button', { name: 'Bring these records in' }));

    const table = await screen.findByRole('table');
    expect(within(table).getByText('Record the customer before starting their deal.')).toBeVisible();
    expect(within(table).getByText('Deals')).toBeVisible();
  });

  it('will not send anything until a file has been chosen', async () => {
    mockApi({ '/organization': { ok: true, body: organization } });
    render(<RecordsPage />);

    expect(await screen.findByRole('button', { name: 'Bring these records in' })).toBeDisabled();
  });

  it('says plainly when the caller can see no lots at all', async () => {
    mockApi({ '/organization': { ok: false, status: 403, code: 'org.forbidden', detail: 'No.' } });
    render(<RecordsPage />);

    expect(await screen.findByText(/nothing here to move/i)).toBeVisible();
  });

  it('shows the server’s refusal rather than a generic failure', async () => {
    mockApi({
      '/organization': { ok: true, body: organization },
      '/migration/packages/r1': {
        ok: false,
        status: 400,
        code: 'migration.package_is_newer',
        detail: 'That package is version 99 and this installation reads up to 1. Upgrade before importing it.',
      },
    });

    render(<RecordsPage />);
    await userEvent.upload(
      await screen.findByLabelText('A file from another installation'),
      packageFile(),
    );
    await userEvent.click(screen.getByRole('button', { name: 'Bring these records in' }));

    expect(await screen.findByText(/version 99/)).toBeVisible();
  });
});
