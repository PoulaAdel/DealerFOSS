// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   session — who is signed in, as far as the browser can tell.
//
// Usage:
//   const { user, signOut } = useSession() inside the authenticated shell.
//
// Coding Instructions:
//   The cookie is HttpOnly, so this cannot read it. "Signed in?" is answered
//   by asking the server, which is also the only answer worth having — a
//   session revoked on another device must stop working here on the next
//   request, and only the server knows that.
//
//   `holds` is the same kind of answer and carries the same warning: it says
//   what to OFFER, never what is allowed. Read its own comment before using it
//   anywhere except to decide whether to draw something.

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { api, clearCurrentTenant, currentTenant } from '../shared/api';
import type { CurrentUser } from '../shared/contracts';

interface Session {
  /** Null when signed out. Undefined while we are still asking. */
  user: CurrentUser | null | undefined;
  tenant: string;
  refresh: () => Promise<void>;
  signOut: () => Promise<void>;

  /**
   * Whether the signed-in person holds a permission *somewhere*.
   *
   * **Use this to decide what to DRAW, and nothing else.** It is not a
   * security check and cannot be one: the list came over the wire and the
   * browser is not a place where access is decided. Every endpoint enforces
   * for itself and will refuse an act whatever this returns.
   *
   * What it is for: a technician used to see Books and Staff on the navigation
   * and learn by clicking and being refused. Now those links are simply not
   * drawn. If this function ever returns the wrong answer the worst outcome is
   * a link that leads to a screen saying "you do not have access" — which is
   * exactly what happened before, for everybody, all the time.
   *
   * False while the session is still loading, so nothing flashes into view and
   * then disappears.
   */
  holds: (permission: string) => boolean;
}

const SessionContext = createContext<Session | null>(null);

export function SessionProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null | undefined>(undefined);
  const [tenant, setTenant] = useState(currentTenant());

  const refresh = useCallback(async () => {
    if (!currentTenant()) {
      setUser(null);
      return;
    }

    try {
      setUser(await api<CurrentUser>('/auth/me'));
      setTenant(currentTenant());
    } catch {
      // Any failure here means "not signed in". The reason is the server's to
      // know; the browser only needs the answer.
      setUser(null);
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const signOut = useCallback(async () => {
    try {
      await api<void>('/auth/logout', { method: 'POST' });
    } finally {
      // Whatever the server said, this browser is done.
      clearCurrentTenant();
      setUser(null);
      setTenant('');
    }
  }, []);

  // A Set rather than `array.includes`, because the navigation asks this once
  // per destination on every render of the shell.
  const held = useMemo(() => new Set(user?.permissions ?? []), [user]);

  const holds = useCallback((permission: string) => held.has(permission), [held]);

  const value = useMemo<Session>(
    () => ({ user, tenant, refresh, signOut, holds }),
    [user, tenant, refresh, signOut, holds],
  );

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export function useSession(): Session {
  const session = useContext(SessionContext);
  if (session === null) {
    throw new Error('useSession must be used inside a SessionProvider.');
  }

  return session;
}
