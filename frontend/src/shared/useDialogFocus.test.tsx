// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Exercises keyboard return through the real confirmation and help surfaces.
//
// Usage:
//   npm test
//
// Coding Instructions:
//   Walk Tab in both directions; a dialog merely having focus once does not
//   prove containment. Do not submit a business operation to test dismissal.

import { useState } from 'react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '../test/render';
import { Confirm } from './Confirm';
import { ShortcutsPanel } from '../app/Shortcuts';

describe('dialog keyboard lifecycle', () => {
  it.each(['confirmation', 'shortcuts'] as const)('contains Tab and restores the opener for %s', async (kind) => {
    const user = userEvent.setup();
    const confirmed = vi.fn();
    function Example() {
      const [open, setOpen] = useState(false);
      return <>
        <button onClick={() => setOpen(true)}>Open</button>
        <button>Outside</button>
        {open && (kind === 'confirmation'
          ? <Confirm title="Continue?" body="Keep this choice explicit." confirmLabel="Continue"
              onConfirm={confirmed} onCancel={() => setOpen(false)} />
          : <ShortcutsPanel onClose={() => setOpen(false)} />)}
      </>;
    }
    render(<Example />);
    const opener = screen.getByRole('button', { name: 'Open' });
    await user.click(opener);
    const dialog = screen.getByRole(kind === 'confirmation' ? 'alertdialog' : 'dialog');
    for (let i = 0; i < 4; i += 1) {
      await user.tab();
      expect(dialog.contains(document.activeElement)).toBe(true);
      await user.tab({ shift: true });
      expect(dialog.contains(document.activeElement)).toBe(true);
    }
    await user.keyboard('{Escape}');
    expect(dialog).not.toBeInTheDocument();
    expect(opener).toHaveFocus();
    expect(confirmed).not.toHaveBeenCalled();
    await user.tab();
    expect(screen.getByRole('button', { name: 'Outside' })).toHaveFocus();
  });

  it('skips the unavailable destructive action until the exact confirmation is typed', async () => {
    const user = userEvent.setup();
    render(<Confirm title="Close period?" body="Requires the period name."
      confirmLabel="Close period" typeToConfirm="October" onConfirm={vi.fn()} onCancel={vi.fn()} />);
    const input = screen.getByRole('textbox');
    expect(input).toHaveFocus();
    await user.tab();
    expect(screen.getByRole('button', { name: 'Cancel' })).toHaveFocus();
    await user.tab();
    expect(input).toHaveFocus();
    await user.type(input, 'October');
    await user.tab();
    expect(screen.getByRole('button', { name: 'Close period' })).toHaveFocus();
  });
});
