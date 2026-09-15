// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   RecordPicker.test — the picker that replaced a dropdown of the first 200.
//
// Usage:
//   npm test
//
// Coding Instructions:
//   THE HINT ASSERTIONS ARE THE POINT OF THIS FILE. The defect being guarded
//   against is not "the list is short" — it is two cars with identical labels
//   and no way to tell which one was chosen. A test that only checks the label
//   would pass against exactly the screen this component was written to
//   replace, so every option assertion here names the hint too.
//
//   The search stub is given the AbortSignal on purpose. A version of this
//   component that debounced without cancelling would pass a test that only
//   counted calls, and would still show the answer for "f" over the answer for
//   "focus" in a browser.

import { render, screen, waitFor } from '../test/render';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { RecordPicker, type PickerOption } from './RecordPicker';

const civic: PickerOption = { id: 'v1', label: '2021 Honda Civic EX', hint: 'K41882' };
const otherCivic: PickerOption = { id: 'v2', label: '2021 Honda Civic EX', hint: 'P77310' };

function pick(over: Partial<Parameters<typeof RecordPicker>[0]> = {}) {
  const onChoose = vi.fn();
  const search = vi.fn(async () => [civic, otherCivic]);

  render(
    <RecordPicker
      id="car"
      label="Car"
      chosen={null}
      onChoose={onChoose}
      search={search}
      {...over}
    />,
  );

  return { onChoose, search };
}

describe('choosing one record out of a dealership', () => {
  it('asks nobody anything until somebody types', async () => {
    // The old dropdown fetched 200 rows on mount whether or not anybody was
    // going to use it. Worse, it presented them as the whole list.
    const { search } = pick();

    expect(await screen.findByText('Type a few letters to find one.')).toBeVisible();
    expect(search).not.toHaveBeenCalled();
  });

  it('searches the server and shows what tells two identical cars apart', async () => {
    const { search } = pick();

    await userEvent.type(screen.getByLabelText('Car'), 'civic');

    // Both options read "2021 Honda Civic EX". Without the hint this screen is
    // the 32-duplicate-labels defect exactly as it was.
    const options = await screen.findAllByRole('button', { name: /2021 Honda Civic EX/ });
    expect(options).toHaveLength(2);

    expect(options[0]).toHaveTextContent('K41882');
    expect(options[1]).toHaveTextContent('P77310');

    expect(search).toHaveBeenCalledWith('civic', expect.any(AbortSignal));
  });

  it('hands back the record that was clicked, not the one that was typed', async () => {
    const { onChoose } = pick();

    await userEvent.type(screen.getByLabelText('Car'), 'civic');
    await userEvent.click(await screen.findByRole('button', { name: /P77310/ }));

    expect(onChoose).toHaveBeenCalledWith(otherCivic);
  });

  it('offers what the caller already knows before anybody types, and drops it after', async () => {
    // Most bookings are a returning customer with the same car. That should be
    // one click rather than a search.
    // The search deliberately does NOT return the shortlisted car: if it did,
    // the car staying on screen would prove nothing about where it came from.
    pick({
      shortlist: [civic],
      search: vi.fn(async () => [{ id: 'v9', label: '2021 Toyota RAV4 XLE', hint: 'B20514' }]),
    });

    expect(await screen.findByRole('button', { name: /K41882/ })).toBeVisible();

    await userEvent.type(screen.getByLabelText('Car'), 'rav4');
    expect(await screen.findByRole('button', { name: /B20514/ })).toBeVisible();

    // Somebody who has started typing is looking for something NOT on the
    // shortlist, so leaving it under the results would be offering the answer
    // they have just told us is wrong.
    await waitFor(() =>
      expect(screen.queryByRole('button', { name: /K41882/ })).not.toBeInTheDocument());
  });

  it('says a search failed instead of saying there is nothing', async () => {
    // These render almost identically and mean opposite things. "No matches" is
    // read as "this customer does not exist", and the next thing somebody does
    // is create a duplicate.
    pick({ search: vi.fn().mockRejectedValue(new Error('gateway went away')) });

    await userEvent.type(screen.getByLabelText('Car'), 'civic');

    expect(await screen.findByRole('alert')).toHaveTextContent('That search could not run.');
    expect(screen.queryByText('Nothing matches that.')).not.toBeInTheDocument();
  });

  it('says nothing matched when nothing matched', async () => {
    pick({ search: vi.fn(async () => []) });

    await userEvent.type(screen.getByLabelText('Car'), 'zzzz');

    expect(await screen.findByText('Nothing matches that.')).toBeVisible();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('cancels the request a keystroke replaced', async () => {
    // Not "makes fewer requests" — the property that matters is that a slow
    // answer for "f" can never land on top of the right answer for "focus".
    const seen: AbortSignal[] = [];
    const search = vi.fn(async (_term: string, signal: AbortSignal) => {
      seen.push(signal);
      return [civic];
    });

    pick({ search });

    await userEvent.type(screen.getByLabelText('Car'), 'ho');
    await waitFor(() => expect(seen.length).toBeGreaterThan(0));

    await userEvent.type(screen.getByLabelText('Car'), 'nda');
    await waitFor(() => expect(seen.length).toBeGreaterThan(1));

    expect(seen[0]?.aborted, 'the first search was left running').toBe(true);
  });

  it('shows the choice with what identifies it, and lets it be changed', async () => {
    const onChoose = vi.fn();

    render(
      <RecordPicker
        id="car"
        label="Car"
        chosen={civic}
        onChoose={onChoose}
        search={vi.fn(async () => [])}
      />,
    );

    // The hint survives selection. A chosen car reading only "2021 Honda Civic
    // EX" leaves somebody unable to check they picked the right one.
    expect(screen.getByText('2021 Honda Civic EX')).toBeVisible();
    expect(screen.getByText('K41882')).toBeVisible();

    await userEvent.click(screen.getByRole('button', { name: 'Change' }));
    expect(onChoose).toHaveBeenCalledWith(null);
  });
});
