import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from 'vitest';

const getAccessToken = vi.fn<() => string | null>();
const refreshSession = vi.fn<() => Promise<void>>();

// `client.ts` must depend on `authClient.ts`, never the reverse. Mocking it
// here isolates apiFetch's retry/header logic from authClient's own
// single-flight implementation (covered separately in authClient.test.ts
// and in client.singleFlight.test.ts, which exercises the real coordinator).
vi.mock('../auth/authClient', () => ({
  getAccessToken: () => getAccessToken(),
  refreshSession: () => refreshSession(),
}));

// `vi.mock` calls are hoisted above imports by vitest, so this static
// import already resolves against the mocked `authClient` module.
import { apiFetch } from './client';
import { fakeResponse } from '../test/fakeResponse';

describe('apiFetch', () => {
  beforeEach(() => {
    getAccessToken.mockReset();
    refreshSession.mockReset();
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('attaches the Authorization header when a session exists', async () => {
    getAccessToken.mockReturnValue('token-abc');
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, { ok: true }));

    await apiFetch('/api/v1/projects');

    const [, init] = (fetch as Mock).mock.calls[0] as [string, RequestInit];
    expect((init.headers as Record<string, string>).Authorization).toBe('Bearer token-abc');
  });

  it('omits the Authorization header when there is no session', async () => {
    getAccessToken.mockReturnValue(null);
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, { ok: true }));

    await apiFetch('/api/v1/projects');

    const [, init] = (fetch as Mock).mock.calls[0] as [string, RequestInit];
    expect((init.headers as Record<string, string>).Authorization).toBeUndefined();
  });

  it('on a 401, refreshes exactly once and retries the same request with the new token', async () => {
    getAccessToken.mockReturnValueOnce('expired').mockReturnValueOnce('fresh');
    refreshSession.mockResolvedValueOnce(undefined);
    (fetch as Mock)
      .mockResolvedValueOnce(fakeResponse(401, { title: 'Unauthorized' }))
      .mockResolvedValueOnce(fakeResponse(200, { ok: true }));

    const result = await apiFetch<{ ok: boolean }>('/api/v1/projects');

    expect(result).toEqual({ ok: true });
    expect(refreshSession).toHaveBeenCalledTimes(1);
    expect(fetch).toHaveBeenCalledTimes(2);
    const [, retryInit] = (fetch as Mock).mock.calls[1] as [string, RequestInit];
    expect((retryInit.headers as Record<string, string>).Authorization).toBe('Bearer fresh');
  });

  it('propagates the error when refresh itself fails, without a second refresh attempt', async () => {
    getAccessToken.mockReturnValue('expired');
    refreshSession.mockRejectedValueOnce(new Error('refresh failed'));
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(401, { title: 'Unauthorized' }));

    await expect(apiFetch('/api/v1/projects')).rejects.toThrow('refresh failed');
    expect(refreshSession).toHaveBeenCalledTimes(1);
    expect(fetch).toHaveBeenCalledTimes(1);
  });

  it('propagates a second 401 after a successful-refresh retry, without refreshing again', async () => {
    getAccessToken.mockReturnValue('expired');
    refreshSession.mockResolvedValueOnce(undefined);
    (fetch as Mock)
      .mockResolvedValueOnce(fakeResponse(401, { title: 'Unauthorized' }))
      .mockResolvedValueOnce(fakeResponse(401, { title: 'Unauthorized' }));

    await expect(apiFetch('/api/v1/projects')).rejects.toMatchObject({ status: 401 });
    expect(refreshSession).toHaveBeenCalledTimes(1);
    expect(fetch).toHaveBeenCalledTimes(2);
  });

  it('propagates a non-401 error on the retried request immediately, without refreshing again', async () => {
    getAccessToken.mockReturnValue('expired');
    refreshSession.mockResolvedValueOnce(undefined);
    (fetch as Mock)
      .mockResolvedValueOnce(fakeResponse(401, { title: 'Unauthorized' }))
      .mockResolvedValueOnce(fakeResponse(403, { title: 'Forbidden', detail: 'Not allowed.' }));

    await expect(apiFetch('/api/v1/projects')).rejects.toMatchObject({ status: 403 });
    expect(refreshSession).toHaveBeenCalledTimes(1);
    expect(fetch).toHaveBeenCalledTimes(2);
  });
});
