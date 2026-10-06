import { ApiError } from '../api/types';
import { parseJsonResponseOrThrow, resolveApiBaseUrl } from '../api/httpCore';
import type { AuthSessionResponse, SessionSnapshot, SessionUser } from './types';

/**
 * authClient.ts — single source of truth for frontend session state.
 *
 * - Access token + user info: module-level, in-memory only. Never written
 *   to any Web Storage.
 * - Refresh token: sessionStorage only (never localStorage, never a cookie).
 *
 * This module is plain TypeScript (no React import), so React observes it
 * via the subscribe/notify pub-sub below (see `AuthContext.tsx`).
 */

/** Exported so tests reference the real key instead of re-typing the literal. */
export const REFRESH_TOKEN_STORAGE_KEY = 'projectcopilot.refreshToken';

interface InMemorySession {
  accessToken: string;
  expiresAtUtc: string;
  user: SessionUser;
}

let currentSession: InMemorySession | null = null;

/**
 * Cached, UI-facing snapshot. Replaced (new object) only when session state
 * actually changes, so `useSyncExternalStore` can rely on reference equality
 * and never spins into an infinite re-render loop.
 */
let snapshot: SessionSnapshot = { isAuthenticated: false, user: null };

/** Single-flight coordinator state for `refreshSession()`. */
let refreshPromise: Promise<void> | null = null;

const listeners = new Set<() => void>();

function notify(): void {
  for (const listener of listeners) {
    listener();
  }
}

/** Subscribe to session changes (login, logout, refresh success/failure). Returns an unsubscribe function. */
export function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

/** Synchronous read of the current session snapshot, for `useSyncExternalStore`. */
export function getCurrentSession(): SessionSnapshot {
  return snapshot;
}

/** The current in-memory access token, or null if there is no session. */
export function getAccessToken(): string | null {
  return currentSession?.accessToken ?? null;
}

function readRefreshToken(): string | null {
  try {
    return sessionStorage.getItem(REFRESH_TOKEN_STORAGE_KEY);
  } catch {
    // sessionStorage may be unavailable (private mode, blocked storage, ...).
    return null;
  }
}

function writeRefreshToken(token: string): void {
  try {
    sessionStorage.setItem(REFRESH_TOKEN_STORAGE_KEY, token);
  } catch {
    // Session still works in-memory for this page load; a reload simply
    // will not be able to silently re-hydrate without a stored token.
  }
}

function clearRefreshToken(): void {
  try {
    sessionStorage.removeItem(REFRESH_TOKEN_STORAGE_KEY);
  } catch {
    // ignore
  }
}

function applySession(result: AuthSessionResponse): void {
  currentSession = {
    accessToken: result.accessToken,
    expiresAtUtc: result.expiresAtUtc,
    user: { userId: result.userId, email: result.email },
  };
  snapshot = { isAuthenticated: true, user: currentSession.user };
  writeRefreshToken(result.refreshToken);
}

function clearSession(): void {
  currentSession = null;
  snapshot = { isAuthenticated: false, user: null };
  clearRefreshToken();
}

/**
 * Bare fetch helper used ONLY for `/auth/login` and `/auth/refresh`.
 *
 * Deliberately does not inject an Authorization header and has no
 * 401-interceptor logic, so a login/refresh call can never recursively
 * trigger a refresh of itself.
 */
async function bareAuthFetch(path: string, body: unknown): Promise<AuthSessionResponse> {
  const baseUrl = resolveApiBaseUrl();

  const response = await fetch(`${baseUrl}${path}`, {
    method: 'POST',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(body),
  });

  return parseJsonResponseOrThrow<AuthSessionResponse>(
    response,
    `Request to ${path} failed with status ${response.status}`,
  );
}

/** Log in with email/password. Throws `ApiError` on failure (e.g. 401). */
export async function login(email: string, password: string): Promise<void> {
  const result = await bareAuthFetch('/api/v1/auth/login', { email, password });
  applySession(result);
  notify();
}

/** Clear the current session (in-memory token + stored refresh token). */
export function logout(): void {
  clearSession();
  notify();
}

function doRefresh(): Promise<void> {
  const refreshToken = readRefreshToken();

  if (!refreshToken) {
    clearSession();
    notify();
    return Promise.reject(new Error('No refresh token available.'));
  }

  return bareAuthFetch('/api/v1/auth/refresh', { refreshToken }).then(
    (result) => {
      applySession(result);
      notify();
    },
    (error: unknown) => {
      // Only clear the session when the server explicitly rejected the
      // refresh token (401). Any other failure (network error, unexpected
      // status, parse failure) must not destroy a session that may still
      // be valid — rethrow as-is so the caller can retry later. Cleanup
      // on an actual 401 lives here, exactly once, regardless of how many
      // concurrent callers are awaiting this same refresh.
      if (error instanceof ApiError && error.status === 401) {
        clearSession();
        notify();
      }
      throw error;
    },
  );
}

/**
 * The single-flight refresh coordinator.
 *
 * If a refresh is already in flight, every caller gets the SAME promise
 * (no duplicate HTTP calls). The check-and-assign is a single synchronous
 * expression with no `await` in between, so there is no race window where
 * two concurrent callers could each start their own refresh.
 */
export function refreshSession(): Promise<void> {
  if (refreshPromise) return refreshPromise;
  return (refreshPromise = doRefresh().finally(() => {
    refreshPromise = null;
  }));
}

/**
 * Called once on app start. If a refresh token exists in sessionStorage,
 * attempts to re-hydrate the in-memory access token via the SAME
 * `refreshSession()` coordinator (not a separate code path). Resolves
 * either way (success or failure) so callers can stop showing a bootstrap
 * loading state; `refreshSession()` itself already clears/notifies on
 * failure.
 */
export function bootstrapSession(): Promise<void> {
  if (!readRefreshToken()) {
    return Promise.resolve();
  }

  return refreshSession().catch(() => {
    // Already cleaned up inside refreshSession/doRefresh; nothing left
    // to do here except let bootstrap finish.
  });
}

/**
 * Test-only reset of module-level singleton state.
 *
 * `authClient.ts` holds module-level state, which would otherwise leak
 * across test cases within the same test file. Guarded to vitest's "test"
 * mode so it cannot be called from application code.
 */
export function __resetForTests(): void {
  if (import.meta.env.MODE !== 'test') {
    throw new Error('__resetForTests() must only be called in tests.');
  }

  currentSession = null;
  snapshot = { isAuthenticated: false, user: null };
  refreshPromise = null;
  listeners.clear();
}
