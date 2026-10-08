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
 *
 * `errors` is present on a FluentValidation failure (ASP.NET Core's
 * ValidationProblemDetails shape: field name -> list of messages). It has
 * no top-level `detail`, so callers that want field-specific feedback must
 * read `errors` directly rather than relying on `detail`/`title` alone.
 */
export interface ApiErrorBody {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
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
