import { createBrowserRouter } from 'react-router-dom';
import { ProjectDashboardPage } from '../projects/ProjectDashboardPage';
import { ProjectsPage } from '../projects/ProjectsPage';
import { AppShell } from '../routes/AppShell';
import { LoginPage } from '../routes/LoginPage';
import { ProtectedRoute } from '../routes/ProtectedRoute';

/**
 * `/login` is public. Everything under `ProtectedRoute` requires a valid
 * session. `AppShell` hosts the shared header/Logout and renders whichever
 * feature route is active via its own `<Outlet/>`.
 */
export const router = createBrowserRouter([
  {
    path: '/login',
    element: <LoginPage />,
  },
  {
    element: <ProtectedRoute />,
    children: [
      {
        element: <AppShell />,
        children: [
          {
            path: '/',
            element: <ProjectsPage />,
          },
          {
            path: '/projects/:projectId',
            element: <ProjectDashboardPage />,
          },
        ],
      },
    ],
  },
]);
