import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { updateTask } from './tasksApi';
import { tasksQueryKey } from './useTasks';
import type { Task } from './types';

/** Fields the caller supplies for a task update - `assignedUserId` is filled in by this hook, never by the caller. */
export type UpdateTaskFields = {
  title: string;
  description?: string;
  status: string;
  priority: string;
  dueDate?: string;
  isBlocked: boolean;
};

export interface UpdateTaskInput {
  id: string;
  request: UpdateTaskFields;
}

/**
 * Update-task mutation, scoped to one project. On success, invalidates only
 * that project's task list query, mirroring `useCreateTask`.
 *
 * `assignedUserId` is always forced to `null` here (never taken from the
 * caller-supplied fields) - there is no assignee picker in this UI, so no
 * caller should ever be able to set it to anything else.
 */
export function useUpdateTask(projectId: string): UseMutationResult<Task, Error, UpdateTaskInput> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, request }: UpdateTaskInput) =>
      updateTask(id, {
        ...request,
        assignedUserId: null,
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: tasksQueryKey(projectId) });
    },
  });
}
