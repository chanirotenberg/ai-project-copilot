import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from 'vitest';
import { AuthProvider } from '../auth/AuthContext';
import { __resetForTests, REFRESH_TOKEN_STORAGE_KEY } from '../auth/authClient';
import { fakeResponse } from '../test/fakeResponse';
import { ProtectedRoute } from './ProtectedRoute';

function renderProtected() {
  const router = createMemoryRouter(
    [
      {
        element: <ProtectedRoute />,
        children: [{ path: '/', element: <div>Protected Home</div> }],
      },
      { path: '/login', element: <div>Login Screen</div> },
    ],
    { initialEntries: ['/'] },
  );

  render(
    <AuthProvider>
      <RouterProvider router={router} />
    </AuthProvider>,
  );
}

describe('ProtectedRoute', () => {
  beforeEach(() => {
    __resetForTests();
    sessionStorage.clear();
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    // `test.globals` is deliberately off (see vite.config.ts), so
    // Testing Library's own auto-cleanup (which relies on a global
    // `afterEach`) never registers itself; clean up explicitly instead.
    cleanup();
    vi.unstubAllGlobals();
  });

  it('redirects to /login when there is no session (no refresh token, bootstrap resolves unauthenticated)', async () => {
    renderProtected();

    await waitFor(() => expect(screen.getByText('Login Screen')).toBeInTheDocument());
    expect(screen.queryByText('Protected Home')).not.toBeInTheDocument();
    expect(fetch).not.toHaveBeenCalled();
  });

  it('renders protected content once bootstrap re-hydrates a valid session', async () => {
    sessionStorage.setItem(REFRESH_TOKEN_STORAGE_KEY, 'refresh-0');
    (fetch as Mock).mockResolvedValueOnce(
      fakeResponse(200, {
        accessToken: 'access-1',
        expiresAtUtc: '2030-01-01T00:00:00Z',
        userId: 'user-1',
        email: 'demo@example.com',
        refreshToken: 'refresh-1',
      }),
    );

    renderProtected();

    await waitFor(() => expect(screen.getByText('Protected Home')).toBeInTheDocument());
    expect(screen.queryByText('Login Screen')).not.toBeInTheDocument();
  });

  it('on a refresh failure during bootstrap, the session is cleared and the user lands on /login exactly once', async () => {
    sessionStorage.setItem(REFRESH_TOKEN_STORAGE_KEY, 'refresh-0');
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(401, { title: 'Authentication failed.' }));

    renderProtected();

    await waitFor(() => expect(screen.getAllByText('Login Screen')).toHaveLength(1));
    expect(screen.queryByText('Protected Home')).not.toBeInTheDocument();
    expect(sessionStorage.getItem(REFRESH_TOKEN_STORAGE_KEY)).toBeNull();
  });
});
