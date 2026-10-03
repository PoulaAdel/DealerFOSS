// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   A navigation disclosure is a set of links in normal keyboard order.
//
// Usage:
//   npm test
//
// Coding Instructions:
//   Leaving the group with Tab closes it without pulling focus back; Escape
//   returns to the trigger. These are different user intentions.

import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { render, screen } from '../test/render';
import { NavGroup } from './NavGroup';

describe('navigation disclosure', () => {
  it('keeps links in Tab order and closes when focus leaves', async () => {
    const user = userEvent.setup();
    render(<><NavGroup label="Sales" active>
      <a href="/customers">Customers</a><a href="/deals">Deals</a>
    </NavGroup><button>After navigation</button></>);
    const trigger = screen.getByRole('button', { name: 'Sales' });
    await user.tab();
    expect(trigger).toHaveFocus();
    await user.keyboard('{Enter}');
    expect(trigger).toHaveAttribute('aria-expanded', 'true');
    expect(document.getElementById(trigger.getAttribute('aria-controls')!)).not.toBeNull();
    await user.tab();
    expect(screen.getByRole('link', { name: 'Customers' })).toHaveFocus();
    await user.tab();
    expect(screen.getByRole('link', { name: 'Deals' })).toHaveFocus();
    await user.tab();
    expect(screen.getByRole('button', { name: 'After navigation' })).toHaveFocus();
    expect(trigger).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });

  it('Escape returns focus to the trigger', async () => {
    const user = userEvent.setup();
    render(<NavGroup label="Sales" active={false}><a href="/deals">Deals</a></NavGroup>);
    const trigger = screen.getByRole('button', { name: 'Sales' });
    await user.click(trigger);
    await user.tab();
    await user.keyboard('{Escape}');
    expect(trigger).toHaveFocus();
    expect(trigger).toHaveAttribute('aria-expanded', 'false');
  });
});
