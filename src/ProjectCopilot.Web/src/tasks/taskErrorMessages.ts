import { ApiError } from '../api/types';

const GENERIC_ERROR = 'Unable to save task. Please try again.';

const TASK_ERROR_FIELDS = ['Title', 'Description', 'Priority', 'DueDate'] as const;

/**
 * Maps a known backend validation message for a Task field to a clearer,
 * user-facing message. Matched by substring (not exact string), same
 * approach as `ProjectsPage`'s `toFriendlyFieldMessage` - a minor backend
 * wording change falls through to the raw message instead of breaking.
 *
 * Deliberately duplicated rather than shared with `ProjectsPage`'s version:
 * the fields/messages are different per screen, and the project explicitly
 * wants this scoped per-screen rather than built into shared i18n
 * infrastructure. Shared *within* the Tasks feature only (by `TasksSection`
 * for create and `TaskRow` for update), to avoid duplicating it a third
 * time for the same two Task operations.
 */
function toFriendlyTaskFieldMessage(field: string, rawMessage: string): string | undefined {
  switch (field) {
    case 'Title':
      if (/must not be empty/i.test(rawMessage)) {
        return 'Task title is required.';
      }
      if (/200 characters/i.test(rawMessage)) {
        return 'Task title must be 200 characters or fewer.';
      }
      return undefined;
    case 'Description':
      if (/2000 characters/i.test(rawMessage)) {
        return 'Description must be 2000 characters or fewer.';
      }
      return undefined;
    case 'Priority':
      // Already reasonably clear as written by the backend - pass it through
      // verbatim rather than inventing a reworded version that could drift.
      return rawMessage;
    case 'DueDate':
      // Backend: "Due date must not be before today." (create only - update
      // has no DueDate validation at all). Reuses the exact same phrasing
      // already used for Project.Deadline, for consistency.
      return "You can't choose a date in the past.";
    default:
      return undefined;
  }
}

/**
 * Resolves the message to show for a failed Task create/update submission.
 *
 * Prefers a friendly message for a known field error (read from the
 * backend's ValidationProblemDetails `errors` map, preserved on
 * `ApiError.body`). Falls back to the raw `detail`/`title`-derived
 * `ApiError.message` when there's no field-level error to map (e.g. a
 * generic 500, or a validation message this function doesn't recognize).
 */
export function getTaskErrorMessage(error: unknown): string {
  if (error instanceof ApiError && error.body?.errors) {
    for (const field of TASK_ERROR_FIELDS) {
      const messages = error.body.errors[field];
      const friendly = messages?.[0] ? toFriendlyTaskFieldMessage(field, messages[0]) : undefined;
      if (friendly) {
        return friendly;
      }
    }
  }

  return error instanceof ApiError ? error.message : GENERIC_ERROR;
}
