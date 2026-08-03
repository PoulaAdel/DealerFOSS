// adminSession — who is operating the installation, as far as the browser knows.
//
// Use:  const { administrator } = useAdminSession() inside the console.
// Edit: deliberately separate from `session.tsx`, and never mounted at the same
//       time. They answer different questions of different servers-side stores,
//       and a single "who am I?" context would make it possible to write a
//       screen that does not know which one it got.

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { adminApi, adminPost } from '../shared/adminApi';
import type { CurrentAdministrator } from '../shared/contracts';

interface AdminSession {
  /** Null when signed out. Undefined while we are still asking. */
  administrator: CurrentAdministrator | null | undefined;
  refresh: () => Promise<void>;
  signOut: () => Promise<void>;
}

const AdminSessionContext = createContext<AdminSession | null>(null);

export function AdminSessionProvider({ children }: { children: ReactNode }) {
  const [administrator, setAdministrator] = useState<
    CurrentAdministrator | null | undefined
  >(undefined);

  const refresh = useCallback(async () => {
    try {
      setAdministrator(await adminApi<CurrentAdministrator>('/me'));
    } catch {
      setAdministrator(null);
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const signOut = useCallback(async () => {
    try {
      await adminPost<void>('/logout', {});
    } finally {
      setAdministrator(null);
    }
  }, []);

  const value = useMemo<AdminSession>(
    () => ({ administrator, refresh, signOut }),
    [administrator, refresh, signOut],
  );

  return (
    <AdminSessionContext.Provider value={value}>{children}</AdminSessionContext.Provider>
  );
}

export function useAdminSession(): AdminSession {
  const session = useContext(AdminSessionContext);
  if (session === null) {
    throw new Error('useAdminSession must be used inside an AdminSessionProvider.');
  }

  return session;
}
