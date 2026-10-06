import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from 'vitest';
import { apiFetch } from './client';
import { __resetForTests, REFRESH_TOKEN_STORAGE_KEY } from '../auth/authClient';
import { fakeResponse } from '../test/fakeResponse';

/**
 * Exercises the REAL `authClient` (not mocked), proving the whole chain —
 * `apiFetch` on a 401 delegates to `authClient.refreshSession()`, whose
 * single-flight coordinator is shared by all concurrent callers — only
 * issues one `/auth/refresh` call even when several unrelated requests
 * hit a 401 at the same time.
 */

describe('apiFetch + authClient.refreshSession single-flight coordination', () => {
  beforeEach(() => {
    __resetForTests();
    sessionStorage.setItem(REFRESH_TOKEN_STORAGE_KEY, 'refresh-0');
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    sessionStorage.clear();
    vi.unstubAllGlobals();
  });

  it('coordinates exactly one /auth/refresh call for several concurrent 401s, and every caller succeeds', async () => {
    const callCountByUrl = new Map<string, number>();

    (fetch as Mock).mockImplementation((url: string) => {
      if (url.endsWith('/api/v1/auth/refresh')) {
        return Promise.resolve(
          fakeResponse(200, {
            accessToken: 'access-new',
            expiresAtUtc: '2030-01-01T00:00:00Z',
            userId: 'user-1',
            email: 'demo@example.com',
            refreshToken: 'refresh-new',
          }),
        );
      }

      const count = (callCountByUrl.get(url) ?? 0) + 1;
      callCountByUrl.set(url, count);

      // First call per data endpoint (pre-refresh, stale/absent token) -> 401.
      // Retried call (post-refresh) -> 200.
      return Promise.resolve(count === 1 ? fakeResponse(401, {}) : fakeResponse(200, { url }));
    });

    const results = await Promise.all([
      apiFetch('/api/v1/a'),
      apiFetch('/api/v1/b'),
      apiFetch('/api/v1/c'),
    ]);

    expect(results).toEqual([
      { url: 'http://localhost:5015/api/v1/a' },
      { url: 'http://localhost:5015/api/v1/b' },
      { url: 'http://localhost:5015/api/v1/c' },
    ]);

    const refreshCalls = (fetch as Mock).mock.calls.filter(([url]) =>
      String(url).endsWith('/api/v1/auth/refresh'),
    );
    expect(refreshCalls).toHaveLength(1);
  });

  it('when the shared refresh fails, every waiting caller is rejected and the session is cleared exactly once', async () => {
    (fetch as Mock).mockImplementation((url: string) => {
      if (url.endsWith('/api/v1/auth/refresh')) {
        return Promise.resolve(fakeResponse(401, { title: 'Authentication failed.' }));
      }
      return Promise.resolve(fakeResponse(401, {}));
    });

    const outcomes = await Promise.allSettled([
      apiFetch('/api/v1/a'),
      apiFetch('/api/v1/b'),
      apiFetch('/api/v1/c'),
    ]);

    expect(outcomes.every((outcome) => outcome.status === 'rejected')).toBe(true);

    const refreshCalls = (fetch as Mock).mock.calls.filter(([url]) =>
      String(url).endsWith('/api/v1/auth/refresh'),
    );
    expect(refreshCalls).toHaveLength(1);
    expect(sessionStorage.getItem(REFRESH_TOKEN_STORAGE_KEY)).toBeNull();
  });
});
