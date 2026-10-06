import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from '../auth/useAuth';

/**
 * Gate for authenticated-only routes.
 *
 * While the initial session bootstrap is still resolving, shows a minimal
 * loading state instead of redirecting (avoids a flash of the login page
 * for an already-authenticated user whose session is being re-hydrated).
 */
export function ProtectedRoute() {
  const { isAuthenticated, isBootstrapping } = useAuth();

  if (isBootstrapping) {
    return <p>Loading session…</p>;
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  return <Outlet />;
}
