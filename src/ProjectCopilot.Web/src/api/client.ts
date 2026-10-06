import { getAccessToken, refreshSession } from '../auth/authClient';
import { parseJsonResponseOrThrow, resolveApiBaseUrl } from './httpCore';

export type ApiFetchOptions = Omit<RequestInit, 'body'> & {
  body?: unknown;
};

/**
 * Generic, typed fetch helper for talking to the backend API.
 *
 * - Resolves `path` against `VITE_API_BASE_URL`.
 * - Attaches `Authorization: Bearer <token>` when a session exists.
 * - Serializes a JSON `body` (if provided) and sets the matching header.
 * - On a 401 from a non-auth endpoint, refreshes the session via
 *   `authClient.refreshSession()` (the shared single-flight coordinator)
 *   and retries the request exactly once with the new token.
 * - Parses a JSON response body (if any) as `T`.
 * - Throws `ApiError` for any non-OK (non 2xx) response.
 */
export async function apiFetch<T>(path: string, options: ApiFetchOptions = {}): Promise<T> {
  return performFetch<T>(path, options, false);
}

async function performFetch<T>(
  path: string,
  options: ApiFetchOptions,
  isRetry: boolean,
): Promise<T> {
  const baseUrl = resolveApiBaseUrl();

  const { body, headers, ...rest } = options;
  const accessToken = getAccessToken();

  const response = await fetch(`${baseUrl}${path}`, {
    ...rest,
    headers: {
      Accept: 'application/json',
      ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
      ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      ...headers,
    },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });

  // Exactly one retry, scoped to this call via the `isRetry` parameter
  // (never a module-level/global flag). A second 401 after a
  // successful-refresh retry falls through to the normal error path below.
  if (response.status === 401 && !isRetry) {
    await refreshSession();
    return performFetch<T>(path, options, true);
  }

  return parseJsonResponseOrThrow<T>(
    response,
    `Request to ${path} failed with status ${response.status}`,
  );
}
