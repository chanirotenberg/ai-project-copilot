import { createBrowserRouter } from 'react-router-dom';
import { AppShell } from '../routes/AppShell';
import { LoginPage } from '../routes/LoginPage';
import { ProtectedRoute } from '../routes/ProtectedRoute';

/**
 * `/login` is public. `/` (and every other feature route added in later
 * slices) is protected by `ProtectedRoute` and requires a valid session.
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
        path: '/',
        element: <AppShell />,
      },
    ],
  },
]);
