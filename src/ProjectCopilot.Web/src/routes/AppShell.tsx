import { Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/useAuth';

/**
 * Root shell rendered inside `ProtectedRoute`, hosting the header/Logout and an
 * `<Outlet/>` for whichever feature route is active (`/` = Projects list,
 * `/projects/:projectId` = Project Dashboard, ...). Further screens (Tasks,
 * Documents, ...) are added in later slices as additional sibling routes.
 */
export function AppShell() {
  const { logout } = useAuth();
  const navigate = useNavigate();

  function handleLogout() {
    logout();
    navigate('/login', { replace: true });
  }

  return (
    <div>
      <header>
        <h1>AI Project Copilot</h1>
        <button type="button" onClick={handleLogout}>
          Logout
        </button>
      </header>
      <main>
        <Outlet />
      </main>
    </div>
  );
}
