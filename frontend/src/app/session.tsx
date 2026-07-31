// session — who is signed in, as far as the browser can tell.
//
// Use:  const { user, signOut } = useSession() inside the authenticated shell.
// Edit: the cookie is HttpOnly, so this cannot read it. "Signed in?" is answered
//       by asking the server, which is also the only answer worth having — a
//       session revoked on another device must stop working here on the next
//       request, and only the server knows that.

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

  const value = useMemo<Session>(
    () => ({ user, tenant, refresh, signOut }),
    [user, tenant, refresh, signOut],
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
