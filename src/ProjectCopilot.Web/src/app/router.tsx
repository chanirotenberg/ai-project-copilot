import { createBrowserRouter, type RouteObject } from 'react-router-dom';
import { ProjectDashboardPage } from '../projects/ProjectDashboardPage';
import { ProjectsPage } from '../projects/ProjectsPage';
import { AppShell } from '../routes/AppShell';
import { LoginPage } from '../routes/LoginPage';
import { NotFoundPage } from '../routes/NotFoundPage';
import { ProtectedRoute } from '../routes/ProtectedRoute';

/**
 * `/login` is public. Everything under `ProtectedRoute` requires a valid
 * session. `AppShell` hosts the shared header/Logout and renders whichever
 * feature route is active via its own `<Outlet/>`.
 *
 * Two `NotFoundPage` wildcards cover any URL that matches no route pattern
 * (AUDIT-6): one nested inside `ProtectedRoute`/`AppShell` so an
 * authenticated user keeps the header/Logout around the message, and one at
 * the top level, as a sibling of `/login` and the `ProtectedRoute` branch.
 *
 * The top-level one is currently UNREACHABLE for every path and every auth
 * state: `ProtectedRoute`/`AppShell` are pathless layout routes, so their
 * nested `*` always wins React Router's matching tie (declaration order,
 * first child wins) before the top-level `*` is ever considered - see
 * `router.test.tsx`'s "unauthenticated + unknown path" case, which redirects
 * to Login rather than ever reaching this entry. Kept anyway as a cheap
 * safety net in case the nested tree is ever restructured; do not assume
 * it's live today. Exported as `appRoutes` (rather than only the built
 * `router`) so tests can mount the exact same route tree with
 * `createMemoryRouter`.
 */
export const appRoutes: RouteObject[] = [
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
          {
            path: '*',
            element: <NotFoundPage />,
          },
        ],
      },
    ],
  },
  {
    path: '*',
    element: <NotFoundPage />,
  },
];

export const router = createBrowserRouter(appRoutes);
