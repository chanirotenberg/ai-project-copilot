/**
 * Documents-specific request/response shapes.
 *
 * Generic error shapes (`ApiError`, `ApiErrorBody`) live in `src/api/types.ts`
 * and are reused here rather than duplicated.
 */

/** `DocumentMetadata` entity, as returned by `GET /api/v1/documents`, `POST /api/v1/documents` and `PUT /api/v1/documents/{id}`. */
export interface Document {
  id: string;
  projectId: string;
  title: string;
  description: string | null;
  createdByUserId: string;
  createdAt: string;
  updatedAt: string;
}

/**
 * Body of `POST /api/v1/documents`.
 *
 * `createdByUserId` is never sent by the client - the backend derives it
 * from the caller's JWT - so there is deliberately no field for it here.
 */
export interface CreateDocumentRequest {
  projectId: string;
  title: string;
  description?: string;
}

/**
 * Body of `PUT /api/v1/documents/{id}`.
 *
 * No `id`/`projectId` here - the document id comes from the route, and
 * project membership is resolved server-side from the document's own
 * stored `ProjectId`, never from anything the client sends.
 */
export interface UpdateDocumentRequest {
  title: string;
  description?: string;
}
