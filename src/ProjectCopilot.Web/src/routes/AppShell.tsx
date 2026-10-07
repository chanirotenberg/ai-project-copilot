import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/useAuth';
import { ProjectsPage } from '../projects/ProjectsPage';

/**
 * Root shell rendered at `/` (inside `ProtectedRoute`).
 *
 * Slice 1.9 adds the first real feature screen (Projects) plus a minimal
 * header with a working Logout control. Further screens (Dashboard, Tasks,
 * Documents, ...) are added in later slices.
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
        <ProjectsPage />
      </main>
    </div>
  );
}
