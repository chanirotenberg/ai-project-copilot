import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from 'vitest';
import { AuthProvider } from '../auth/AuthContext';
import { __resetForTests, REFRESH_TOKEN_STORAGE_KEY } from '../auth/authClient';
import { fakeResponse } from '../test/fakeResponse';
import type { Project } from '../projects/types';
import { appRoutes } from './router';

const sampleProject: Project = {
  id: 'project-1',
  name: 'Demo Project',
  description: 'A demo project',
  status: 'Active',
  deadline: null,
  createdByUserId: 'user-1',
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
};

const BOOTSTRAP_REFRESH_RESPONSE = fakeResponse(200, {
  accessToken: 'access-1',
  expiresAtUtc: '2030-01-01T00:00:00Z',
  userId: 'user-1',
  email: 'demo@example.com',
  refreshToken: 'refresh-1',
});

/** Mounts the real route tree (`appRoutes`) exactly as `App.tsx` does, minus `BrowserRouter`. */
function renderApp(initialEntry: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  const router = createMemoryRouter(appRoutes, { initialEntries: [initialEntry] });

  render(
    <AuthProvider>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </AuthProvider>,
  );
}

/** Arranges a resolved, authenticated session before the router mounts. */
function givenAuthenticatedSession() {
  sessionStorage.setItem(REFRESH_TOKEN_STORAGE_KEY, 'refresh-0');
  (fetch as Mock).mockResolvedValueOnce(BOOTSTRAP_REFRESH_RESPONSE);
}

describe('app router (AUDIT-6: unmatched-path fallback)', () => {
  beforeEach(() => {
    __resetForTests();
    sessionStorage.clear();
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    // `test.globals` is deliberately off (see vite.config.ts), so Testing
    // Library's own auto-cleanup never registers itself; clean up explicitly.
    cleanup();
    vi.unstubAllGlobals();
  });

  it('shows "Page not found" (inside the app shell) for an authenticated user at an unknown path', async () => {
    givenAuthenticatedSession();

    renderApp('/this-path-does-not-exist');

    expect(await screen.findByRole('heading', { name: 'Page not found' })).toBeInTheDocument();
    // Still rendered inside AppShell (the nested wildcard under ProtectedRoute/AppShell
    // matched, not a bare page) - the header/Logout stay around the message.
    expect(screen.getByRole('heading', { name: 'AI Project Copilot' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Logout' })).toBeInTheDocument();
  });

  it(
    'redirects an unauthenticated user hitting an unknown path to /login (documents actual ' +
      'behavior: route matching is structural and auth-independent, so the nested wildcard ' +
      "under ProtectedRoute always wins the tie over the top-level one - see router.tsx's " +
      'comment and the implementation report for the full reasoning)',
    async () => {
      // No stored refresh token: bootstrap resolves unauthenticated without calling fetch.
      renderApp('/this-path-does-not-exist');

      await waitFor(() =>
        expect(screen.getByRole('heading', { name: 'Sign in' })).toBeInTheDocument(),
      );
      expect(screen.queryByText('Page not found')).not.toBeInTheDocument();
      expect(fetch).not.toHaveBeenCalled();
    },
  );

  it('still renders the Login screen at /login', async () => {
    renderApp('/login');

    await waitFor(() =>
      expect(screen.getByRole('heading', { name: 'Sign in' })).toBeInTheDocument(),
    );
  });

  it('still renders the Projects list at / for an authenticated user', async () => {
    givenAuthenticatedSession();
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, [sampleProject]));

    renderApp('/');

    expect(await screen.findByRole('heading', { name: 'Projects' })).toBeInTheDocument();
    expect(await screen.findByText('Demo Project')).toBeInTheDocument();
  });

  it('still renders the Project Dashboard at /projects/abc-123 for an authenticated user', async () => {
    givenAuthenticatedSession();
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, { ...sampleProject, id: 'abc-123' }));

    renderApp('/projects/abc-123');

    expect(await screen.findByText('Demo Project')).toBeInTheDocument();
    // The status value now renders as its own badge element inside the
    // "Status: " paragraph (visual polish pass) - query the label and value
    // as separate text nodes rather than one combined "Status: Active" match.
    expect(screen.getByText(/Status:/)).toBeInTheDocument();
    expect(screen.getByText('Active')).toBeInTheDocument();
  });
});
