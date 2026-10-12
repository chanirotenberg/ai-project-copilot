import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { deleteTask } from './tasksApi';
import { tasksQueryKey } from './useTasks';

/**
 * Delete-task mutation, scoped to one project. On success, invalidates only
 * that project's task list query, mirroring `useDeleteDocument`.
 */
export function useDeleteTask(projectId: string): UseMutationResult<void, Error, string> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => deleteTask(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: tasksQueryKey(projectId) });
    },
  });
}
