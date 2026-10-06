/**
 * Generic shapes for the typed API client.
 *
 * This slice intentionally defines only the generic error/response
 * contract. Endpoint-specific request/response types (projects, tasks,
 * auth, ...) are added in later slices when the corresponding features
 * are implemented.
 */

/**
 * Shape of an error response body returned by the backend's
 * ExceptionHandlingMiddleware (validation failures, not-found, etc.).
 */
export interface ApiErrorBody {
  title?: string;
  detail?: string;
  status?: number;
  [key: string]: unknown;
}

/**
 * Error thrown by `apiFetch` for any non-OK HTTP response.
 */
export class ApiError extends Error {
  readonly status: number;
  readonly body: ApiErrorBody | undefined;

  constructor(status: number, message: string, body: ApiErrorBody | undefined) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.body = body;
  }
}
