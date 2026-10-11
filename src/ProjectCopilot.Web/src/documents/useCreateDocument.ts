import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { createDocument } from './documentsApi';
import { documentsQueryKey } from './useDocuments';
import type { Document } from './types';

/** Fields the caller supplies for a new document - `projectId` is filled in by this hook, never by the caller. */
export type CreateDocumentFields = {
  title: string;
  description?: string;
};

/**
 * Create-document mutation, scoped to one project. On success, invalidates
 * only that project's document list query (not the tasks list, not the
 * projects list, not any global invalidation) so it refetches and shows the
 * new document without a manual page reload.
 */
export function useCreateDocument(
  projectId: string,
): UseMutationResult<Document, Error, CreateDocumentFields> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (fields: CreateDocumentFields) =>
      createDocument({
        ...fields,
        projectId,
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: documentsQueryKey(projectId) });
    },
  });
}
