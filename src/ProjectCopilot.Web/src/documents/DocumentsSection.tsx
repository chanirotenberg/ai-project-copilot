import { useState, type FormEvent } from 'react';
import { ApiError } from '../api/types';
import { DocumentRow } from './DocumentRow';
import { getDocumentErrorMessage } from './documentErrorMessages';
import { useCreateDocument } from './useCreateDocument';
import { useDocuments } from './useDocuments';

const GENERIC_LOAD_ERROR = 'Unable to load documents. Please try again.';

/**
 * Documents section (Slice 1.13): real list + create for a single project's
 * document metadata, embedded in the Project Dashboard. Editing/deleting an
 * existing document is handled per-row by `DocumentRow`.
 *
 * Metadata-only - no file upload, storage, preview, or download. Mirrors
 * `TasksSection` in structure, scaled down to Documents' simpler shape
 * (Title/Description only).
 */
export function DocumentsSection({ projectId }: { projectId: string }) {
  const documentsQuery = useDocuments(projectId);
  const createDocumentMutation = useCreateDocument(projectId);

  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [validationError, setValidationError] = useState<string | null>(null);

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const trimmedTitle = title.trim();
    if (!trimmedTitle) {
      // Clear any stale server-side error from a previous failed attempt so
      // it can't render alongside this new client-side validation error.
      createDocumentMutation.reset();
      setValidationError('Document title is required.');
      return;
    }

    setValidationError(null);

    const trimmedDescription = description.trim();

    createDocumentMutation.mutate(
      {
        title: trimmedTitle,
        description: trimmedDescription ? trimmedDescription : undefined,
      },
      {
        onSuccess: () => {
          setTitle('');
          setDescription('');
        },
      },
    );
  }

  return (
    <section className="section">
      <h3 className="section-title">Documents</h3>

      <form className="card" onSubmit={handleSubmit} noValidate>
        <div className="form-field">
          <label htmlFor="document-title">Title</label>
          <input
            id="document-title"
            name="title"
            type="text"
            value={title}
            disabled={createDocumentMutation.isPending}
            onChange={(event) => setTitle(event.target.value)}
          />
        </div>
        <div className="form-field">
          <label htmlFor="document-description">Description</label>
          <textarea
            id="document-description"
            name="description"
            value={description}
            disabled={createDocumentMutation.isPending}
            onChange={(event) => setDescription(event.target.value)}
          />
        </div>

        {validationError ? (
          <p className="alert" role="alert">
            {validationError}
          </p>
        ) : null}
        {createDocumentMutation.isError ? (
          <p className="alert" role="alert">
            {getDocumentErrorMessage(createDocumentMutation.error)}
          </p>
        ) : null}

        <button
          type="submit"
          className="btn btn-primary"
          disabled={createDocumentMutation.isPending}
        >
          {createDocumentMutation.isPending ? 'Creating…' : 'Create document'}
        </button>
      </form>

      {documentsQuery.isPending ? <p className="text-muted">Loading documents…</p> : null}

      {documentsQuery.isError ? (
        <p className="alert" role="alert">
          {documentsQuery.error instanceof ApiError
            ? documentsQuery.error.message
            : GENERIC_LOAD_ERROR}
        </p>
      ) : null}

      {!documentsQuery.isPending && !documentsQuery.isError && documentsQuery.data ? (
        documentsQuery.data.length === 0 ? (
          <p className="empty-state">No documents yet.</p>
        ) : (
          <ul className="list">
            {documentsQuery.data.map((document) => (
              <DocumentRow key={document.id} document={document} projectId={projectId} />
            ))}
          </ul>
        )
      ) : null}
    </section>
  );
}
