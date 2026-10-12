import { useState } from 'react';
import { ApiError } from '../api/types';
import { getDocumentErrorMessage } from './documentErrorMessages';
import { useDeleteDocument } from './useDeleteDocument';
import { useUpdateDocument } from './useUpdateDocument';
import type { Document } from './types';

const DELETE_CONFIRM_MESSAGE = 'Delete this document? This cannot be undone.';
const GENERIC_DELETE_ERROR = 'Unable to delete document. Please try again.';

/**
 * One document's row (Slice 1.13): a read-only display with an "Edit"
 * button that swaps in an inline edit form - no modal, no new UI framework
 * - plus a "Delete" button gated by a plain native `window.confirm`
 * (proportional for this scope; no modal framework exists in this codebase
 * to reuse). Mirrors `TaskRow`'s inline-edit-toggle pattern.
 *
 * `projectId` is accepted so `useUpdateDocument`/`useDeleteDocument` can
 * invalidate the right project's document list query on success; it is
 * never sent in the PUT/DELETE requests themselves (the backend resolves
 * the document's project server-side from its own stored `ProjectId`).
 */
export function DocumentRow({ document, projectId }: { document: Document; projectId: string }) {
  const updateDocumentMutation = useUpdateDocument(projectId);
  const deleteDocumentMutation = useDeleteDocument(projectId);

  const [isEditing, setIsEditing] = useState(false);
  const [title, setTitle] = useState(document.title);
  const [description, setDescription] = useState(document.description ?? '');

  function startEditing() {
    // Re-seed the edit form from the current document prop every time
    // editing starts, so a previously cancelled edit never leaks stale
    // values in.
    setTitle(document.title);
    setDescription(document.description ?? '');
    updateDocumentMutation.reset();
    setIsEditing(true);
  }

  function cancelEditing() {
    setIsEditing(false);
    updateDocumentMutation.reset();
  }

  function handleSave() {
    const trimmedDescription = description.trim();

    updateDocumentMutation.mutate(
      {
        id: document.id,
        request: {
          title: title.trim(),
          description: trimmedDescription ? trimmedDescription : undefined,
        },
      },
      {
        onSuccess: () => {
          setIsEditing(false);
        },
        // On failure, deliberately stay in edit mode with the user's
        // in-progress edits intact - no reset of title/description.
      },
    );
  }

  function handleDelete() {
    if (!window.confirm(DELETE_CONFIRM_MESSAGE)) {
      return;
    }
    deleteDocumentMutation.mutate(document.id);
  }

  if (isEditing) {
    return (
      <li className="card card-editing">
        <div className="form-field">
          <label htmlFor={`document-${document.id}-title`}>Title</label>
          <input
            id={`document-${document.id}-title`}
            type="text"
            value={title}
            disabled={updateDocumentMutation.isPending}
            onChange={(event) => setTitle(event.target.value)}
          />
        </div>
        <div className="form-field">
          <label htmlFor={`document-${document.id}-description`}>Description</label>
          <textarea
            id={`document-${document.id}-description`}
            value={description}
            disabled={updateDocumentMutation.isPending}
            onChange={(event) => setDescription(event.target.value)}
          />
        </div>

        {updateDocumentMutation.isError ? (
          <p className="alert" role="alert">
            {getDocumentErrorMessage(updateDocumentMutation.error)}
          </p>
        ) : null}

        <div className="item-actions">
          <button
            type="button"
            className="btn btn-primary"
            onClick={handleSave}
            disabled={updateDocumentMutation.isPending}
          >
            {updateDocumentMutation.isPending ? 'Saving…' : 'Save'}
          </button>
          <button
            type="button"
            className="btn btn-secondary"
            onClick={cancelEditing}
            disabled={updateDocumentMutation.isPending}
          >
            Cancel
          </button>
        </div>
      </li>
    );
  }

  return (
    <li className="card">
      <strong className="item-title">{document.title}</strong>
      <p className={document.description ? 'item-description' : 'item-description text-muted'}>
        {document.description ? document.description : 'No description provided.'}
      </p>

      {deleteDocumentMutation.isError ? (
        <p className="alert" role="alert">
          {deleteDocumentMutation.error instanceof ApiError
            ? deleteDocumentMutation.error.message
            : GENERIC_DELETE_ERROR}
        </p>
      ) : null}

      <div className="item-actions">
        <button type="button" className="btn btn-secondary" onClick={startEditing}>
          Edit
        </button>
        <button
          type="button"
          className="btn btn-danger"
          onClick={handleDelete}
          disabled={deleteDocumentMutation.isPending}
        >
          {deleteDocumentMutation.isPending ? 'Deleting…' : 'Delete'}
        </button>
      </div>
    </li>
  );
}
