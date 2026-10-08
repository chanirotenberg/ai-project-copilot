import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { createTask } from './tasksApi';
import { tasksQueryKey } from './useTasks';
import type { Task } from './types';

/** Fields the caller supplies for a new task - `projectId`/`assignedUserId` are filled in by this hook, never by the caller. */
export type CreateTaskFields = {
  title: string;
  description?: string;
  priority: string;
  dueDate?: string;
};

/**
 * Create-task mutation, scoped to one project. On success, invalidates only
 * that project's task list query (not the projects list, not any global
 * invalidation) so it refetches and shows the new task without a manual
 * page reload.
 */
export function useCreateTask(projectId: string): UseMutationResult<Task, Error, CreateTaskFields> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (fields: CreateTaskFields) =>
      createTask({
        ...fields,
        projectId,
        assignedUserId: null,
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: tasksQueryKey(projectId) });
    },
  });
}
