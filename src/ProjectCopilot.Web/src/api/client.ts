import { ApiError, type ApiErrorBody } from './types';

/**
 * Base URL for the backend API, read from Vite env configuration.
 * Must never be hardcoded (see CLAUDE.md §17).
 */
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

export type ApiFetchOptions = Omit<RequestInit, 'body'> & {
  body?: unknown;
};

/**
 * Generic, typed fetch helper for talking to the backend API.
 *
 * - Resolves `path` against `VITE_API_BASE_URL`.
 * - Serializes a JSON `body` (if provided) and sets the matching header.
 * - Parses a JSON response body (if any) as `T`.
 * - Throws `ApiError` for any non-OK (non 2xx) response.
 *
 * This slice only provides the generic helper. Endpoint-specific
 * functions (e.g. `getProjects`, `login`) are added in later slices.
 */
export async function apiFetch<T>(path: string, options: ApiFetchOptions = {}): Promise<T> {
  if (!API_BASE_URL) {
    throw new Error(
      'VITE_API_BASE_URL is not configured. Set it in your .env file (see .env.example).',
    );
  }

  const { body, headers, ...rest } = options;

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...rest,
    headers: {
      Accept: 'application/json',
      ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
      ...headers,
    },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });

  const contentType = response.headers.get('content-type');
  const hasJsonBody = contentType?.includes('application/json') ?? false;
  const parsedBody = hasJsonBody ? await response.json() : undefined;

  if (!response.ok) {
    const errorBody = parsedBody as ApiErrorBody | undefined;
    throw new ApiError(
      response.status,
      errorBody?.detail ??
        errorBody?.title ??
        `Request to ${path} failed with status ${response.status}`,
      errorBody,
    );
  }

  return parsedBody as T;
}
