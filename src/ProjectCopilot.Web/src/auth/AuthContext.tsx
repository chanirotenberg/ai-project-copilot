import { useEffect, useState, useSyncExternalStore, type ReactNode } from 'react';
import { bootstrapSession, getCurrentSession, login, logout, subscribe } from './authClient';
import { AuthContext, type AuthContextValue } from './authContextInstance';

/**
 * Thin React layer over `authClient.ts`.
 *
 * Observes session state via `useSyncExternalStore` (no polling, no
 * duplicated state) and runs `bootstrapSession()` once on mount, tracking a
 * local "bootstrapping" flag so `ProtectedRoute` can show a loading state
 * instead of redirecting before the bootstrap check has resolved.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const session = useSyncExternalStore(subscribe, getCurrentSession, getCurrentSession);
  const [isBootstrapping, setIsBootstrapping] = useState(true);

  useEffect(() => {
    let isMounted = true;

    void bootstrapSession().finally(() => {
      if (isMounted) {
        setIsBootstrapping(false);
      }
    });

    return () => {
      isMounted = false;
    };
  }, []);

  const value: AuthContextValue = {
    isAuthenticated: session.isAuthenticated,
    isBootstrapping,
    user: session.user,
    login,
    logout,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
