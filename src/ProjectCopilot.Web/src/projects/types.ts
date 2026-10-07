/**
 * Projects-specific request/response shapes.
 *
 * Generic error shapes (`ApiError`, `ApiErrorBody`) live in `src/api/types.ts`
 * and are reused here rather than duplicated.
 */

/** `Project` entity, as returned by both `GET /api/v1/projects` and `POST /api/v1/projects`. */
export interface Project {
  id: string;
  name: string;
  description: string | null;
  status: string;
  deadline: string | null;
  createdByUserId: string;
  createdAt: string;
  updatedAt: string;
}

/**
 * Body of `POST /api/v1/projects`.
 *
 * The backend derives the creator/owner from the JWT — there is no
 * `ownerId`/`userId`/`createdByUserId` field here, and none must be added.
 * `status` is also not client-settable (the backend always creates
 * `"Active"` projects), so it is intentionally absent.
 */
export interface CreateProjectRequest {
  name: string;
  description?: string;
  deadline?: string;
}
