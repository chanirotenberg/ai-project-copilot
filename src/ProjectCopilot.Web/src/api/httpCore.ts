import { ApiError, type ApiErrorBody } from './types';

/**
 * Shared low-level HTTP plumbing used by BOTH the typed API client
 * (`api/client.ts`) and the auth client's bare fetch helper
 * (`auth/authClient.ts`). Keeps the base-URL guard and the
 * parse-then-throw tail identical in both places instead of duplicated.
 *
 * Header injection, 401-retry logic (`client.ts`) and the
 * no-interceptor bare fetch (`authClient.ts`) stay in their own modules —
 * only this shared tail moves here.
 */

/**
 * Resolves the backend base URL from Vite env configuration.
 * Must never be hardcoded (see CLAUDE.md §17).
 */
export function resolveApiBaseUrl(): string {
  const baseUrl = import.meta.env.VITE_API_BASE_URL;
  if (!baseUrl) {
    throw new Error(
      'VITE_API_BASE_URL is not configured. Set it in your .env file (see .env.example).',
    );
  }
  return baseUrl;
}

/**
 * Parses a fetch `Response` as JSON (when it declares a JSON content-type)
 * and throws `ApiError` for any non-OK (non 2xx) response.
 */
export async function parseJsonResponseOrThrow<T>(
  response: Response,
  fallbackMessage: string,
): Promise<T> {
  const contentType = response.headers.get('content-type');
  const hasJsonBody = contentType?.includes('application/json') ?? false;
  const parsedBody = hasJsonBody ? await response.json() : undefined;

  if (!response.ok) {
    const errorBody = parsedBody as ApiErrorBody | undefined;
    throw new ApiError(
      response.status,
      errorBody?.detail ?? errorBody?.title ?? fallbackMessage,
      errorBody,
    );
  }

  return parsedBody as T;
}
