import { apiFetch } from '../api/client';
import type { CreateDocumentRequest, Document, UpdateDocumentRequest } from './types';

/**
 * `GET /api/v1/documents?projectId={projectId}` - server-side membership
 * check on `projectId` happens before querying; a non-member gets a 404 (not
 * 403), matching the same anti-enumeration design as `GET /api/v1/tasks`
 * and `GET /api/v1/projects/{id}`.
 */
export function listDocuments(projectId: string): Promise<Document[]> {
  return apiFetch<Document[]>(`/api/v1/documents?projectId=${encodeURIComponent(projectId)}`);
}

/** `POST /api/v1/documents` - creates a document on the given project. */
export function createDocument(request: CreateDocumentRequest): Promise<Document> {
  return apiFetch<Document>('/api/v1/documents', {
    method: 'POST',
    body: request,
  });
}

/**
 * `PUT /api/v1/documents/{id}` - updates a document. Project membership is
 * resolved server-side from the document's own stored `ProjectId`, not from
 * anything sent here - the request body deliberately has no
 * `id`/`projectId` field.
 */
export function updateDocument(id: string, request: UpdateDocumentRequest): Promise<Document> {
  return apiFetch<Document>(`/api/v1/documents/${id}`, {
    method: 'PUT',
    body: request,
  });
}

/**
 * `DELETE /api/v1/documents/{id}` - deletes a document. Returns `204 No
 * Content` with an empty body; `apiFetch`'s shared response parsing already
 * resolves to `undefined` for a non-JSON response, so no special-casing is
 * needed here.
 */
export function deleteDocument(id: string): Promise<void> {
  return apiFetch<void>(`/api/v1/documents/${id}`, {
    method: 'DELETE',
  });
}
