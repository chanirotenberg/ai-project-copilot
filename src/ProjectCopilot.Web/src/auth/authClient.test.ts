import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from 'vitest';
import * as authClient from './authClient';
import { ApiError } from '../api/types';
import { fakeResponse } from '../test/fakeResponse';

const { REFRESH_TOKEN_STORAGE_KEY } = authClient;

function sessionBody(overrides: Partial<Record<string, unknown>> = {}) {
  return {
    accessToken: 'access-1',
    expiresAtUtc: '2030-01-01T00:00:00Z',
    userId: 'user-1',
    email: 'demo@example.com',
    refreshToken: 'refresh-1',
    ...overrides,
  };
}

describe('authClient', () => {
  beforeEach(() => {
    // Module-level singleton state would otherwise leak across these test
    // cases within this file; reset it explicitly before each one.
    authClient.__resetForTests();
    sessionStorage.clear();
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('login stores the access token in memory and the refresh token in sessionStorage', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, sessionBody()));

    await authClient.login('demo@example.com', 'password123');

    expect(authClient.getAccessToken()).toBe('access-1');
    expect(authClient.getCurrentSession()).toEqual({
      isAuthenticated: true,
      user: { userId: 'user-1', email: 'demo@example.com' },
    });
    expect(sessionStorage.getItem(REFRESH_TOKEN_STORAGE_KEY)).toBe('refresh-1');
  });

  it('login does not create a session on a 401 and rejects with the backend message', async () => {
    (fetch as Mock).mockResolvedValueOnce(
      fakeResponse(401, {
        status: 401,
        title: 'Authentication failed.',
        detail: 'Invalid email or password.',
      }),
    );

    await expect(authClient.login('demo@example.com', 'wrong')).rejects.toThrow(
      'Invalid email or password.',
    );

    expect(authClient.getAccessToken()).toBeNull();
    expect(authClient.getCurrentSession().isAuthenticated).toBe(false);
  });

  it('refreshSession is single-flight: concurrent callers share one in-flight HTTP call', async () => {
    sessionStorage.setItem(REFRESH_TOKEN_STORAGE_KEY, 'refresh-0');

    let resolveFetch!: (value: Response) => void;
    (fetch as Mock).mockReturnValueOnce(
      new Promise<Response>((resolve) => {
        resolveFetch = resolve;
      }),
    );

    const first = authClient.refreshSession();
    const second = authClient.refreshSession();
    const third = authClient.refreshSession();

    expect(first).toBe(second);
    expect(second).toBe(third);

    resolveFetch(
      fakeResponse(200, sessionBody({ accessToken: 'access-2', refreshToken: 'refresh-2' })),
    );
    await Promise.all([first, second, third]);

    expect(fetch).toHaveBeenCalledTimes(1);
    expect(authClient.getAccessToken()).toBe('access-2');

    // A subsequent refresh starts a fresh coordinator run.
    (fetch as Mock).mockResolvedValueOnce(
      fakeResponse(200, sessionBody({ accessToken: 'access-3', refreshToken: 'refresh-3' })),
    );
    await authClient.refreshSession();
    expect(fetch).toHaveBeenCalledTimes(2);
    expect(authClient.getAccessToken()).toBe('access-3');
  });

  it('a refresh rejected with 401 (server explicitly rejects the refresh token) clears the in-memory token and the stored refresh token exactly once', async () => {
    sessionStorage.setItem(REFRESH_TOKEN_STORAGE_KEY, 'refresh-0');
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(401, { title: 'Authentication failed.' }));

    let notifyCount = 0;
    const unsubscribe = authClient.subscribe(() => {
      notifyCount += 1;
    });

    await expect(authClient.refreshSession()).rejects.toBeInstanceOf(ApiError);

    expect(authClient.getAccessToken()).toBeNull();
    expect(authClient.getCurrentSession().isAuthenticated).toBe(false);
    expect(sessionStorage.getItem(REFRESH_TOKEN_STORAGE_KEY)).toBeNull();
    expect(notifyCount).toBe(1);

    unsubscribe();
  });

  it('a refresh that fails for a reason other than a 401 (network error, 500, ...) rethrows WITHOUT clearing the session or the stored refresh token', async () => {
    // Start from an authenticated session, same as a page that already
    // has a valid access token and is merely proactively refreshing it.
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, sessionBody()));
    await authClient.login('demo@example.com', 'password123');
    expect(authClient.getAccessToken()).toBe('access-1');

    // A transient network failure during refresh — not a server rejection
    // of the refresh token itself.
    (fetch as Mock).mockRejectedValueOnce(new TypeError('Network request failed'));

    let notifyCount = 0;
    const unsubscribe = authClient.subscribe(() => {
      notifyCount += 1;
    });

    await expect(authClient.refreshSession()).rejects.toThrow('Network request failed');

    // Session must survive: the refresh token itself was never rejected.
    expect(authClient.getAccessToken()).toBe('access-1');
    expect(authClient.getCurrentSession().isAuthenticated).toBe(true);
    expect(sessionStorage.getItem(REFRESH_TOKEN_STORAGE_KEY)).toBe('refresh-1');
    expect(notifyCount).toBe(0);

    unsubscribe();

    // A 500 from the refresh endpoint itself (server error, not an
    // explicit rejection of the refresh token) must behave the same way.
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(500, { title: 'Internal Server Error' }));

    await expect(authClient.refreshSession()).rejects.toMatchObject({ status: 500 });

    expect(authClient.getAccessToken()).toBe('access-1');
    expect(authClient.getCurrentSession().isAuthenticated).toBe(true);
    expect(sessionStorage.getItem(REFRESH_TOKEN_STORAGE_KEY)).toBe('refresh-1');
  });

  it('bootstrapSession resolves with no HTTP call when there is no stored refresh token', async () => {
    await authClient.bootstrapSession();

    expect(fetch).not.toHaveBeenCalled();
    expect(authClient.getCurrentSession().isAuthenticated).toBe(false);
  });

  it('bootstrapSession re-hydrates the session via the refreshSession coordinator when a refresh token exists', async () => {
    sessionStorage.setItem(REFRESH_TOKEN_STORAGE_KEY, 'refresh-0');
    (fetch as Mock).mockResolvedValueOnce(
      fakeResponse(200, sessionBody({ accessToken: 'access-bootstrap' })),
    );

    await authClient.bootstrapSession();

    expect(fetch).toHaveBeenCalledTimes(1);
    expect(authClient.getAccessToken()).toBe('access-bootstrap');
    expect(authClient.getCurrentSession().isAuthenticated).toBe(true);
  });

  it('logout clears the session and notifies subscribers', () => {
    sessionStorage.setItem(REFRESH_TOKEN_STORAGE_KEY, 'refresh-0');

    let notified = false;
    const unsubscribe = authClient.subscribe(() => {
      notified = true;
    });

    authClient.logout();

    expect(authClient.getAccessToken()).toBeNull();
    expect(authClient.getCurrentSession().isAuthenticated).toBe(false);
    expect(sessionStorage.getItem(REFRESH_TOKEN_STORAGE_KEY)).toBeNull();
    expect(notified).toBe(true);

    unsubscribe();
  });
});
