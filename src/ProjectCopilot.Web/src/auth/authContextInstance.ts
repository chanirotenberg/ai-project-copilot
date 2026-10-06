import { createContext } from 'react';
import type { SessionUser } from './types';

/**
 * The raw React context instance, kept in its own non-component file.
 *
 * `react-refresh/only-export-components` requires component files to only
 * export components; the context object and the `useAuth` hook therefore
 * live here and in `useAuth.ts` respectively, while `AuthContext.tsx`
 * exports only the `AuthProvider` component.
 */
export interface AuthContextValue {
  isAuthenticated: boolean;
  isBootstrapping: boolean;
  user: SessionUser | null;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined);
