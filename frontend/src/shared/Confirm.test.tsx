// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Confirm.test — the dialog that replaced window.confirm (UX audit C2).
//
// Usage:
//   npm test.
//
// Coding Instructions:
//   Three things here are the reason window.confirm was replaced, not taste:
//   focus lands somewhere safe when the dialog opens, Escape and the backdrop
//   both cancel and never confirm, and a typed answer that does not match
//   keeps the destructive button disabled.

import { render, screen } from '../test/render';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { Confirm } from './Confirm';

describe('the two-button form', () => {
  it('starts focus on Cancel, the safe default', async () => {
    render(
      <Confirm
        title="Forget this passkey?"
        body="That device stops being able to sign you in."
        confirmLabel="Forget it"
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />,
    );

    expect(await screen.findByRole('button', { name: 'Cancel' })).toHaveFocus();
  });

  it('cancels on Escape, never confirms', async () => {
    const onConfirm = vi.fn();
    const onCancel = vi.fn();
    render(
      <Confirm
        title="Forget this passkey?"
        body="That device stops being able to sign you in."
        confirmLabel="Forget it"
        onConfirm={onConfirm}
        onCancel={onCancel}
      />,
    );

    await userEvent.keyboard('{Escape}');

    expect(onCancel).toHaveBeenCalledOnce();
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it('cancels on a click outside the dialog', async () => {
    const onCancel = vi.fn();
    render(
      <Confirm
        title="Forget this passkey?"
        body="That device stops being able to sign you in."
        confirmLabel="Forget it"
        onConfirm={vi.fn()}
        onCancel={onCancel}
      />,
    );

    // eslint-disable-next-line testing-library/no-node-access -- the backdrop itself has no role
    await userEvent.click(document.querySelector('.confirm-backdrop')!);

    expect(onCancel).toHaveBeenCalledOnce();
  });

  it('does not cancel on a click inside the dialog', async () => {
    const onCancel = vi.fn();
    render(
      <Confirm
        title="Forget this passkey?"
        body="That device stops being able to sign you in."
        confirmLabel="Forget it"
        onConfirm={vi.fn()}
        onCancel={onCancel}
      />,
    );

    await userEvent.click(await screen.findByText('That device stops being able to sign you in.'));

    expect(onCancel).not.toHaveBeenCalled();
  });
});

describe('the typed-confirmation form', () => {
  it('starts focus in the typed field and keeps the confirm button disabled', async () => {
    render(
      <Confirm
        title="Suspend North Auto Group?"
        body="Everyone there is signed out immediately."
        confirmLabel="Suspend"
        typeToConfirm="North Auto Group"
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />,
    );

    expect(await screen.findByRole('textbox')).toHaveFocus();
    expect(screen.getByRole('button', { name: 'Suspend' })).toBeDisabled();
  });

  it('stays disabled on a near miss and enables on an exact match', async () => {
    render(
      <Confirm
        title="Suspend North Auto Group?"
        body="Everyone there is signed out immediately."
        confirmLabel="Suspend"
        typeToConfirm="North Auto Group"
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />,
    );

    const field = screen.getByRole('textbox');
    const confirmButton = screen.getByRole('button', { name: 'Suspend' });

    await userEvent.type(field, 'north auto group');
    expect(confirmButton).toBeDisabled();

    await userEvent.clear(field);
    await userEvent.type(field, 'North Auto Group');
    expect(confirmButton).toBeEnabled();
  });
});
