import { cleanup, render, screen, fireEvent, waitFor } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from 'vitest';
import { AuthProvider } from '../auth/AuthContext';
import { __resetForTests, REFRESH_TOKEN_STORAGE_KEY } from '../auth/authClient';
import { fakeResponse } from '../test/fakeResponse';
import { LoginPage } from './LoginPage';

function renderLoginPage() {
  const router = createMemoryRouter(
    [
      { path: '/login', element: <LoginPage /> },
      { path: '/', element: <div>Home Screen</div> },
    ],
    { initialEntries: ['/login'] },
  );

  render(
    <AuthProvider>
      <RouterProvider router={router} />
    </AuthProvider>,
  );
}

describe('LoginPage', () => {
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

  it('successful login creates a session and redirects to the protected home route', async () => {
    (fetch as Mock).mockResolvedValueOnce(
      fakeResponse(200, {
        accessToken: 'access-1',
        expiresAtUtc: '2030-01-01T00:00:00Z',
        userId: 'user-1',
        email: 'demo@example.com',
        refreshToken: 'refresh-1',
      }),
    );

    renderLoginPage();
    // No stored refresh token in this test, so bootstrap resolves on the
    // next microtask with nothing to do; wait for it before interacting.
    await waitFor(() => expect(screen.getByRole('button', { name: /sign in/i })).toBeEnabled());

    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'demo@example.com' } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'password123' } });
    fireEvent.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => expect(screen.getByText('Home Screen')).toBeInTheDocument());
  });

  it('failed login (401) shows a generic error and does not create a session', async () => {
    (fetch as Mock).mockResolvedValueOnce(
      fakeResponse(401, {
        status: 401,
        title: 'Authentication failed.',
        detail: 'Invalid email or password.',
      }),
    );

    renderLoginPage();
    await waitFor(() => expect(screen.getByRole('button', { name: /sign in/i })).toBeEnabled());

    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'demo@example.com' } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'wrong-password' } });
    fireEvent.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() =>
      expect(screen.getByRole('alert')).toHaveTextContent('Invalid email or password.'),
    );
    expect(screen.queryByText('Home Screen')).not.toBeInTheDocument();
  });

  it('rejects an invalid email client-side without calling the backend', async () => {
    renderLoginPage();
    await waitFor(() => expect(screen.getByRole('button', { name: /sign in/i })).toBeEnabled());

    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'not-an-email' } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'password123' } });
    fireEvent.click(screen.getByRole('button', { name: /sign in/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/valid email/i);
    expect(fetch).not.toHaveBeenCalled();
  });

  it('disables the email, password, and submit controls while the initial session bootstrap is in flight', async () => {
    // A stored refresh token means `AuthProvider` kicks off a real
    // bootstrap refresh call on mount; keep it pending to observe the
    // disabled state deterministically instead of racing a microtask.
    sessionStorage.setItem(REFRESH_TOKEN_STORAGE_KEY, 'refresh-0');
    let resolveFetch!: (value: Response) => void;
    (fetch as Mock).mockReturnValueOnce(
      new Promise<Response>((resolve) => {
        resolveFetch = resolve;
      }),
    );

    renderLoginPage();

    expect(screen.getByLabelText('Email')).toBeDisabled();
    expect(screen.getByLabelText('Password')).toBeDisabled();
    expect(screen.getByRole('button', { name: /sign in/i })).toBeDisabled();

    resolveFetch(
      fakeResponse(200, {
        accessToken: 'access-1',
        expiresAtUtc: '2030-01-01T00:00:00Z',
        userId: 'user-1',
        email: 'demo@example.com',
        refreshToken: 'refresh-1',
      }),
    );

    await waitFor(() =>
      expect(screen.getByRole('button', { name: /sign in/i })).not.toBeDisabled(),
    );
  });
});
