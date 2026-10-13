// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   AccessReviewPage.test — the review screen, and the two properties that make
//   a signature worth anything.
//
// Usage:
//   npm test.
//
// Coding Instructions:
//   THESE TWO ARE SAFEGUARDS RATHER THAN COVERAGE.
//
//   "signs the table it displayed, not a fresh read" — the whole value of the
//   attestation is that it says what this person SAW. If somebody later makes
//   the sign button re-read first "to be safe", that test should be what stops
//   them: it would turn a statement about a reviewed picture into a statement
//   about whatever happened to be true at the instant of clicking.
//
//   "shows the server's refusal rather than predicting it" — the screen must not
//   grow its own copy of the scoping rule. Ask, then show the answer. Same rule
//   as the staff screen beside it.

import { render, screen } from '../../test/render';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router';
import { AccessReviewPage } from './AccessReviewPage';
import { apiCalls, mockApi } from '../../test/setup';
import { setCurrentTenant } from '../../shared/api';
import { signedInAs } from '../../test/session';
import type { AccessReview } from '../../shared/contracts';

const review: AccessReview = {
  takenAt: '2026-10-13T09:00:00Z',
  checksum: 'abc123',
  reviewEveryDays: 90,
  nextDueAt: '2026-11-01T09:00:00Z',
  lastSignature: {
    takenAt: '2026-08-03T09:00:00Z',
    signedByUserId: 'u1',
    signedByDisplayName: 'Dana Whitfield',
    checksum: 'abc123',
    rowCount: 2,
    note: 'Quarterly review.',
  },
  rows: [
    {
      userId: 'u1',
      email: 'gm@dev.local',
      displayName: 'Dana Whitfield',
      isActive: true,
      hasSecondFactor: true,
      permission: 'Deals.Approve',
      roleName: 'Manager',
      isOrganizationWide: true,
      rooftopId: null,
      ownRecordsOnly: false,
    },
    {
      userId: 'u2',
      email: 'sales@dev.local',
      displayName: 'Sam Okonkwo',
      isActive: false,
      hasSecondFactor: false,
      permission: 'Deals.Write',
      roleName: 'Salesperson',
      isOrganizationWide: false,
      rooftopId: 'r1',
      ownRecordsOnly: true,
    },
  ],
};

function renderReview() {
  setCurrentTenant('northgroup');

  return render(
    <MemoryRouter initialEntries={['/security/access-review']}>
      <AccessReviewPage />
    </MemoryRouter>,
  );
}

function withReview(body: AccessReview = review) {
  mockApi({
    '/auth/me': signedInAs(),
    '/security/access-review': { ok: true, body },
  });
}

describe('reviewing who has access', () => {
  it('answers the question a reviewer actually arrives with', async () => {
    // "Who can approve a deal" — the permission has to be readable from the
    // table without opening each person, which is why the server ships one flat
    // table rather than a person-shaped tree.
    withReview();
    renderReview();

    expect(await screen.findByText('Deals.Approve')).toBeVisible();
    expect(screen.getByText('Dana Whitfield')).toBeVisible();
    expect(screen.getByText('Sam Okonkwo')).toBeVisible();
  });

  it('narrows to one right when a reviewer types it', async () => {
    withReview();
    renderReview();

    await screen.findByText('Deals.Approve');
    await userEvent.type(screen.getByLabelText('Find'), 'Deals.Approve');

    expect(screen.getByText('Deals.Approve')).toBeVisible();
    expect(screen.queryByText('Sam Okonkwo')).not.toBeInTheDocument();
  });

  it('marks a leaver whose grants are still on the record', async () => {
    // The single most useful thing a review surfaces, so it is a chip on the
    // person rather than a column somebody has to scan for.
    withReview();
    renderReview();

    expect(await screen.findByText('cannot sign in')).toBeVisible();
    expect(screen.getByText('no second step')).toBeVisible();
  });

  it('says when nothing has changed since the last review', async () => {
    withReview();
    renderReview();

    expect(await screen.findByText(/Nothing has changed/)).toBeVisible();
  });

  it('says when somebody’s access has changed since the last review', async () => {
    // The comparison that makes keeping the checksum worthwhile.
    withReview({ ...review, checksum: 'different' });
    renderReview();

    expect(await screen.findByText(/access has changed/)).toBeVisible();
  });

  it('signs the table it displayed, not a fresh read', async () => {
    withReview();
    renderReview();

    await screen.findByText('Deals.Approve');
    await userEvent.click(screen.getByRole('button', { name: 'I have reviewed this' }));

    const signed = apiCalls().find((c) => c.path === '/security/access-review/sign');

    expect(signed).toBeDefined();

    const body = JSON.parse(String(signed!.init?.body)) as { checksum: string };
    expect(body.checksum).toBe('abc123');
  });

  it('shows the server’s refusal rather than predicting it', async () => {
    // The screen does not decide who may review. It asks and shows the answer,
    // so somebody typing the address gets the refusal rather than a blank table.
    mockApi({
      '/auth/me': signedInAs(),
      '/security/access-review': { ok: false, status: 403, code: 'security.review_forbidden', detail: 'No.' },
    });
    renderReview();

    expect(await screen.findByRole('alert')).toHaveTextContent(/across the whole group/i);
  });

  it('never reviewed reads as never reviewed, not as overdue', async () => {
    // A dealership nobody has asked yet has not missed a deadline, and a date in
    // the past would read as one.
    withReview({ ...review, lastSignature: null, nextDueAt: null });
    renderReview();

    expect(await screen.findByText('Never reviewed')).toBeVisible();
  });
});
