// render — mounting a screen the way the application actually mounts it.
//
// Use:  import { render, screen } from '../../test/render';
//       ...exactly as you would from '@testing-library/react'.
//
// Edit: this exists because every screen now reads its words from I18nProvider,
//       so `render(<CustomersPage />)` bare is not a lighter test — it is a tree
//       the application never builds, and it throws.
//
//       Overriding `render` rather than adding a wrapper at each of the ~60 call
//       sites keeps the tests reading as they did, and means the next context
//       the shell gains is added HERE instead of in seventeen files. That is the
//       same argument the shell itself makes for putting providers above the
//       router.
//
//       The provider is NOT added silently to hide a design problem: a screen
//       used outside the provider in the real application is a bug, and
//       i18n.test.tsx still asserts that `useI18n` throws in that case. This
//       file supplies the provider because the real tree has one.
//
//       Tests that need the raw, unwrapped mount — the two that prove a
//       provider is required — import from '@testing-library/react' directly.

import { render as testingLibraryRender, type RenderOptions } from '@testing-library/react';
import type { ReactElement, ReactNode } from 'react';
import { I18nProvider } from '../shared/i18n';
import { AppearanceProvider } from '../shared/appearance';

/**
 * The two the shell mounts above the router. Appearance joined i18n here when a
 * third screen — password recovery — started carrying the appearance and
 * language controls, which are on every screen a signed-out person can reach.
 *
 * `appearance.test.tsx` still proves `useAppearance` throws outside its
 * provider; that test imports the raw `render` deliberately, so this wrapper
 * cannot hide the design rule it asserts.
 */
function Providers({ children }: { children: ReactNode }) {
  return (
    <I18nProvider>
      <AppearanceProvider>{children}</AppearanceProvider>
    </I18nProvider>
  );
}

/**
 * Mounts inside the providers the shell mounts. A `wrapper` passed by the
 * caller still wins for its own subtree — it is nested inside these rather than
 * replacing them, which is what the caller means when they pass a router.
 */
export function render(ui: ReactElement, options?: Omit<RenderOptions, 'wrapper'>) {
  return testingLibraryRender(ui, { wrapper: Providers, ...options });
}

// Everything else — screen, waitFor, within, act, cleanup — unchanged.
export * from '@testing-library/react';
