import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { deleteDocument } from './documentsApi';
import { documentsQueryKey } from './useDocuments';

/**
 * Delete-document mutation, scoped to one project. On success, invalidates
 * only that project's document list query, mirroring `useCreateDocument`/
 * `useUpdateDocument`. No Tasks precedent for this hook - Tasks has no
 * delete endpoint.
 */
export function useDeleteDocument(projectId: string): UseMutationResult<void, Error, string> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => deleteDocument(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: documentsQueryKey(projectId) });
    },
  });
}
