// RecallCheck.test — the three properties that make this feature honest.
//
// Use:  npm test.
// Edit: none of these three is a style preference, and none should be relaxed
//       to make a refactor easier.
//
//       (1) It asks nothing until somebody presses the button. Decided
//       2026-08-15: this is an outbound call to a regulator, and a stock screen
//       left open on a desk must not keep making it.
//
//       (2) "Could not reach the service" is drawn differently from "no
//       campaigns found". Collapsing the two would turn a network timeout into
//       an all-clear on a screen somebody uses to decide whether a car is safe
//       to hand over.
//
//       (3) The caveat rides with the data. The public record is by MODEL and
//       says nothing about whether THIS car has had the work done.

import { render, screen } from '../../test/render';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { RecallCheck } from './RecallCheck';
import { apiCalls, mockApi } from '../../test/setup';
import { setCurrentTenant } from '../../shared/api';
import type { RecallCampaign, RecallReport } from '../../shared/contracts';

const campaign = (over: Partial<RecallCampaign> = {}): RecallCampaign => ({
  campaignNumber: '23V-456',
  manufacturer: 'Honda',
  component: 'Brake hose',
  summary: 'The front brake hose may chafe against the wheel liner.',
  remedy: 'Dealers will inspect and replace the hose, free of charge.',
  reportedOn: '2023-07-14',
  doNotDrive: false,
  parkOutside: false,
  ...over,
});

const report = (over: Partial<RecallReport> = {}): RecallReport => ({
  vehicleId: 'v1',
  modelYear: 2019,
  make: 'Honda',
  model: 'Civic',
  campaigns: [campaign()],
  appliesToModelNotVehicle: true,
  ...over,
});

function renderCheck() {
  setCurrentTenant('northgroup');
  return render(<RecallCheck vehicleId="v1" />);
}

async function press(name = 'Check for recalls') {
  await userEvent.click(screen.getByRole('button', { name }));
}

describe('checking for safety recalls', () => {
  it('asks the regulator nothing until somebody asks it to', async () => {
    // Property (1). A screen left open all afternoon makes no calls.
    mockApi({ '/vehicles/v1/recalls': { ok: true, body: report() } });
    renderCheck();

    expect(screen.getByRole('button', { name: 'Check for recalls' })).toBeVisible();
    expect(apiCalls()).toHaveLength(0);
  });

  it('asks once, when pressed', async () => {
    mockApi({ '/vehicles/v1/recalls': { ok: true, body: report() } });
    renderCheck();
    await press();

    expect(await screen.findByText('23V-456')).toBeVisible();
    expect(apiCalls().filter((c) => c.path === '/vehicles/v1/recalls')).toHaveLength(1);
  });

  it('carries the caveat above the campaigns, not below them', async () => {
    // Property (3). Naming the year, make and model is part of it: it says out
    // loud what the answer is actually about.
    mockApi({ '/vehicles/v1/recalls': { ok: true, body: report() } });
    renderCheck();
    await press();

    const caveat = await screen.findByText(/kept by model and not by car/i);
    expect(caveat).toBeVisible();
    expect(caveat.textContent).toContain('2019 Honda Civic');
  });

  it('does not read an unreachable service as an all-clear', async () => {
    // Property (2), and the reason RecallErrors.Unavailable exists at all.
    mockApi({
      '/vehicles/v1/recalls': {
        ok: false,
        status: 503,
        code: 'recalls.unavailable',
        detail:
          'The safety recall service could not be reached. This is not the same as the vehicle having no recalls — try again shortly.',
      },
    });
    renderCheck();
    await press();

    expect(await screen.findByText(/could not be reached/i)).toBeVisible();
    expect(screen.queryByText(/No campaigns are published/i)).toBeNull();
  });

  it('says "nothing published" without saying "this car is fine"', async () => {
    // The other half of property (2). An empty list is a real answer, and it is
    // still not a statement about this particular car.
    mockApi({ '/vehicles/v1/recalls': { ok: true, body: report({ campaigns: [] }) } });
    renderCheck();
    await press();

    expect(await screen.findByText(/No campaigns are published for this model/i)).toBeVisible();
    expect(screen.getByText(/not the same as this car having been checked/i)).toBeVisible();
  });

  it('gives the regulator’s two judgements as words', async () => {
    // Colour alone would fail somebody reading this aloud over the phone, and
    // "do not drive" is the most serious thing this component can say.
    mockApi({
      '/vehicles/v1/recalls': {
        ok: true,
        body: report({ campaigns: [campaign({ doNotDrive: true, parkOutside: true })] }),
      },
    });
    renderCheck();
    await press();

    expect(await screen.findByText('Do not drive')).toBeVisible();
    expect(screen.getByText('Park it outside')).toBeVisible();
  });

  it('says when the car cannot be identified well enough to look up', async () => {
    mockApi({
      '/vehicles/v1/recalls': {
        ok: false,
        status: 400,
        code: 'recalls.not_identifiable',
        detail: 'This vehicle does not carry enough identification to look up recalls.',
      },
    });
    renderCheck();
    await press();

    expect(await screen.findByText(/not carry enough identification/i)).toBeVisible();
  });

  it('offers another go once an answer is on screen', async () => {
    mockApi({ '/vehicles/v1/recalls': { ok: true, body: report() } });
    renderCheck();
    await press();
    await screen.findByText('23V-456');

    expect(screen.getByRole('button', { name: 'Check again' })).toBeVisible();
  });
});
