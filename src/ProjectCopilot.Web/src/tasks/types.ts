/**
 * Tasks-specific request/response shapes.
 *
 * Generic error shapes (`ApiError`, `ApiErrorBody`) live in `src/api/types.ts`
 * and are reused here rather than duplicated.
 */

/** `TaskItem` entity, as returned by `GET /api/v1/tasks`, `POST /api/v1/tasks` and `PUT /api/v1/tasks/{id}`. */
export interface Task {
  id: string;
  projectId: string;
  title: string;
  description: string | null;
  status: string;
  priority: string;
  assignedUserId: string | null;
  dueDate: string | null;
  isBlocked: boolean;
  createdAt: string;
  updatedAt: string;
}

/**
 * Body of `POST /api/v1/tasks`.
 *
 * `assignedUserId` is typed as the literal `null` (not `string | null`): the
 * frontend has no assignee picker (there is no backend endpoint to resolve a
 * user id to a display name), so it never sends anything else for this
 * field. This is a compile-time guarantee against accidentally wiring up a
 * client-chosen value later without updating this type deliberately.
 *
 * `status` is not client-settable (the backend always creates `"Todo"`
 * tasks), so it is intentionally absent.
 */
export interface CreateTaskRequest {
  projectId: string;
  title: string;
  description?: string;
  priority: string;
  assignedUserId: null;
  dueDate?: string;
}

/**
 * Body of `PUT /api/v1/tasks/{id}`.
 *
 * No `id`/`projectId` here - the task id comes from the route, and project
 * membership is resolved server-side from the task's own stored
 * `ProjectId`, never from anything the client sends.
 *
 * PUT is a full update of the editable task fields - there is no
 * omitted-means-preserve semantics. `assignedUserId` must therefore always
 * be sent, and is typed `string | null` (not the literal `null`) because the
 * caller always round-trips the task's current `assignedUserId` value
 * (including `null`, which is itself a valid "unassigned" value) rather
 * than send a client-chosen one - there is still no assignee picker in this
 * UI.
 */
export interface UpdateTaskRequest {
  title: string;
  description?: string;
  status: string;
  priority: string;
  assignedUserId: string | null;
  dueDate?: string;
  isBlocked: boolean;
}
