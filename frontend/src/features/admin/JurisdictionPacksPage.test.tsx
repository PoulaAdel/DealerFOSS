// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   JurisdictionPacksPage.test — the console screen that decides whether a rate
//   table may charge anybody anything.
//
// Usage:
//   npm test.
//
// Coding Instructions:
//   THE LOAD-BEARING TEST HERE IS THE ONE ABOUT THE MISSING BUTTON. A pack whose
//   basis flags nobody has decided cannot be approved, and the screen must not
//   offer the approval anyway and let the API refuse it — that is how a refusal
//   becomes something people click through. It has to say WHICH flags are
//   undecided instead, because "it cannot be used" is not actionable.
//
//   The second is that a Draft pack is drawn as an ordinary state and not as a
//   fault. The product ships Indiana that way on purpose.

import { render, screen } from '../../test/render';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { JurisdictionPacksPage } from './JurisdictionPacksPage';
import { apiCalls, mockApi } from '../../test/setup';
import type { PackView } from '../../shared/contracts';

const indiana: PackView = {
  packId: 'us-in',
  country: 'US',
  scheme: 'Sst',
  version: 1,
  status: 'Draft',
  effectiveFrom: '2026-01-01',
  effectiveTo: null,
  sourceUri: 'https://www.in.gov/dor/i-am-a/business-corp/sales-tax/',
  fetchedAt: '2026-10-13T00:00:00Z',
  contentHash: 'abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789',
  reviewedBy: null,
  reviewedAt: null,
  rateCount: 1,
  missingRules: ['tradeInReducesBasis', 'documentationFeeIsTaxable', 'otherFeesAreTaxable'],
};

const ready: PackView = {
  ...indiana,
  packId: 'zz-example',
  country: 'ZZ',
  scheme: 'Manual',
  version: 2,
  status: 'Supported',
  reviewedBy: 'admin@dev.local',
  reviewedAt: '2026-10-18T09:00:00Z',
  missingRules: [],
};

describe('the rate tables a deployment holds', () => {
  it('will not offer to approve a pack whose basis flags nobody has decided', async () => {
    mockApi({ '/admin/jurisdiction-packs': { ok: true, body: [indiana] } });
    render(<JurisdictionPacksPage />);

    // Named, not merely counted: a reviewer needs to know what to go and decide.
    expect(await screen.findByText(/Nobody has decided: tradeInReducesBasis/)).toBeVisible();

    // And the action is absent rather than present-and-refused.
    expect(
      screen.queryByRole('button', { name: /I have read the source/ }),
    ).not.toBeInTheDocument();
  });

  it('draws an unreviewed pack as an ordinary state, not a fault', async () => {
    mockApi({ '/admin/jurisdiction-packs': { ok: true, body: [indiana] } });
    render(<JurisdictionPacksPage />);

    // The chip says its own name, so the colour never carries the meaning.
    expect(await screen.findByText('Not reviewed')).toBeVisible();
    expect(screen.getByText('Streamlined Sales Tax')).toBeVisible();
  });

  it('shows where the figures came from and when somebody read it', async () => {
    mockApi({ '/admin/jurisdiction-packs': { ok: true, body: [indiana] } });
    render(<JurisdictionPacksPage />);

    const source = await screen.findByRole('link', { name: 'Open the source' });
    expect(source).toHaveAttribute('href', indiana.sourceUri);
    // Somebody else's page, so it opens away from the console and carries no
    // referrer — which installation is checking a tax table is nobody's business.
    expect(source).toHaveAttribute('rel', 'noopener noreferrer');

    // The hash is shown because an approval covers BYTES: if the file changes,
    // the approval no longer applies, and this is the only way to see that.
    expect(screen.getByText('abcdef012345')).toBeVisible();
    expect(screen.getByText(/Nobody yet/)).toBeVisible();
  });

  it('offers approval once every flag is decided, and records who said so', async () => {
    mockApi({
      '/admin/jurisdiction-packs': [
        { ok: true, body: [{ ...ready, status: 'Draft', reviewedBy: null, reviewedAt: null }] },
        { ok: true, body: [ready] },
      ],
      '/admin/jurisdiction-packs/zz-example/2/review': { ok: true, body: ready },
    });

    render(<JurisdictionPacksPage />);

    await userEvent.click(
      await screen.findByRole('button', { name: /I have read the source/ }),
    );

    expect(
      apiCalls().some((call) => call.path === '/admin/jurisdiction-packs/zz-example/2/review'),
    ).toBe(true);

    expect(await screen.findByText(/admin@dev.local/)).toBeVisible();
    expect(screen.getByText('In use')).toBeVisible();
  });

  it('asks before withdrawing, because it changes what people are charged', async () => {
    mockApi({ '/admin/jurisdiction-packs': { ok: true, body: [ready] } });
    render(<JurisdictionPacksPage />);

    await userEvent.click(await screen.findByRole('button', { name: 'Withdraw' }));

    expect(screen.getByText(/Withdraw zz-example version 2\?/)).toBeVisible();
    expect(
      screen.getByText(/Deals already taxed from it keep what they were charged/),
    ).toBeVisible();

    // Nothing was sent on opening the question.
    expect(
      apiCalls().some((call) => call.path.includes('/withdraw')),
    ).toBe(false);
  });

  it('says so, and offers a retry, when the list cannot be read', async () => {
    mockApi({
      '/admin/jurisdiction-packs': {
        ok: false,
        status: 403,
        code: 'compliance.forbidden',
        detail: 'You cannot change this deployment’s tax packs.',
      },
    });

    render(<JurisdictionPacksPage />);

    expect(await screen.findByRole('alert')).toBeVisible();
    expect(screen.getByRole('button', { name: /Try again|Retry/i })).toBeVisible();
  });

  it('reports that nothing was added when every shipped table is already held', async () => {
    mockApi({
      '/admin/jurisdiction-packs': { ok: true, body: [indiana] },
      '/admin/jurisdiction-packs/shipped': { ok: true, body: [] },
    });

    render(<JurisdictionPacksPage />);
    await userEvent.click(
      await screen.findByRole('button', { name: 'Load the tables that ship' }),
    );

    expect(await screen.findByText(/Nothing new/)).toBeVisible();
  });
});
