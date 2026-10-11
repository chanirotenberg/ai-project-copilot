import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { updateDocument } from './documentsApi';
import { documentsQueryKey } from './useDocuments';
import type { Document } from './types';

/** Fields the caller supplies for a document update. */
export type UpdateDocumentFields = {
  title: string;
  description?: string;
};

export interface UpdateDocumentInput {
  id: string;
  request: UpdateDocumentFields;
}

/**
 * Update-document mutation, scoped to one project. On success, invalidates
 * only that project's document list query, mirroring `useCreateDocument`.
 */
export function useUpdateDocument(
  projectId: string,
): UseMutationResult<Document, Error, UpdateDocumentInput> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, request }: UpdateDocumentInput) => updateDocument(id, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: documentsQueryKey(projectId) });
    },
  });
}
