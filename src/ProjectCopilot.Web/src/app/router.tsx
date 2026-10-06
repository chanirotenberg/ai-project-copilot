import { createBrowserRouter } from 'react-router-dom';
import { AppShell } from '../routes/AppShell';

/**
 * Router skeleton: a single root route rendering the app shell.
 * Additional routes (login, projects, tasks, documents, ...) are added
 * in later slices as their screens are implemented.
 */
export const router = createBrowserRouter([
  {
    path: '/',
    element: <AppShell />,
  },
]);
