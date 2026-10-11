import { ApiError } from '../api/types';

const GENERIC_ERROR = 'Unable to save document. Please try again.';

const DOCUMENT_ERROR_FIELDS = ['Title', 'Description'] as const;

/**
 * Maps a known backend validation message for a Document field to a
 * clearer, user-facing message. Matched by substring (not exact string),
 * same approach as `ProjectsPage`'s `toFriendlyFieldMessage` - a minor
 * backend wording change falls through to the raw message instead of
 * breaking.
 *
 * Deliberately duplicated rather than shared with Tasks/Projects' versions:
 * the fields/messages are different per screen, and the project explicitly
 * wants this scoped per-screen rather than built into shared i18n
 * infrastructure.
 */
function toFriendlyDocumentFieldMessage(field: string, rawMessage: string): string | undefined {
  switch (field) {
    case 'Title':
      if (/must not be empty/i.test(rawMessage)) {
        return 'Document title is required.';
      }
      if (/200 characters/i.test(rawMessage)) {
        return 'Document title must be 200 characters or fewer.';
      }
      return undefined;
    case 'Description':
      if (/2000 characters/i.test(rawMessage)) {
        return 'Description must be 2000 characters or fewer.';
      }
      return undefined;
    default:
      return undefined;
  }
}

/**
 * Resolves the message to show for a failed Document create/update/delete
 * submission.
 *
 * Prefers a friendly message for a known field error (read from the
 * backend's ValidationProblemDetails `errors` map, preserved on
 * `ApiError.body`). Falls back to the raw `detail`/`title`-derived
 * `ApiError.message` when there's no field-level error to map (e.g. a
 * generic 500, or a validation message this function doesn't recognize).
 */
export function getDocumentErrorMessage(error: unknown): string {
  if (error instanceof ApiError && error.body?.errors) {
    for (const field of DOCUMENT_ERROR_FIELDS) {
      const messages = error.body.errors[field];
      const friendly = messages?.[0]
        ? toFriendlyDocumentFieldMessage(field, messages[0])
        : undefined;
      if (friendly) {
        return friendly;
      }
    }
  }

  return error instanceof ApiError ? error.message : GENERIC_ERROR;
}
