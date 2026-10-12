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
  /**
   * The task's CURRENT `assignedUserId`, round-tripped as-is into the PUT
   * body. This is not user-editable and never comes from a form field -
   * there is still no assignee picker in this UI. The caller must supply
   * it straight from the task being edited, so a full-update PUT never
   * silently clears an existing assignment.
   */
  currentAssignedUserId: string | null;
}

/**
 * Update-task mutation, scoped to one project. On success, invalidates only
 * that project's task list query, mirroring `useCreateTask`.
 *
 * PUT is a full update, so `assignedUserId` is always sent - as
 * `currentAssignedUserId`, supplied by the caller from the task's existing
 * value, never derived from the caller-editable `request` fields.
 */
export function useUpdateTask(projectId: string): UseMutationResult<Task, Error, UpdateTaskInput> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, request, currentAssignedUserId }: UpdateTaskInput) =>
      updateTask(id, {
        ...request,
        assignedUserId: currentAssignedUserId,
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: tasksQueryKey(projectId) });
    },
  });
}
