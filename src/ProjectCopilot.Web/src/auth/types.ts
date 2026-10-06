/**
 * Auth-specific request/response and session shapes.
 *
 * Generic error shapes (`ApiError`, `ApiErrorBody`) live in `src/api/types.ts`
 * and are reused here rather than duplicated.
 */

/** Body of `POST /api/v1/auth/login`. */
export interface LoginRequest {
  email: string;
  password: string;
}

/** Body of `POST /api/v1/auth/refresh`. */
export interface RefreshRequest {
  refreshToken: string;
}

/**
 * Response shape shared by `/api/v1/auth/login` and `/api/v1/auth/refresh`:
 * a new access token plus a rotated refresh token.
 */
export interface AuthSessionResponse {
  accessToken: string;
  expiresAtUtc: string;
  userId: string;
  email: string;
  refreshToken: string;
}

/** The authenticated user's identity, as known to the frontend. */
export interface SessionUser {
  userId: string;
  email: string;
}

/** Synchronous, UI-facing snapshot of the current session state. */
export interface SessionSnapshot {
  isAuthenticated: boolean;
  user: SessionUser | null;
}
