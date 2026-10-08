import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { listTasks } from './tasksApi';
import type { Task } from './types';

/** Query key for a project's task list - scoped per project, not a single global list. */
export function tasksQueryKey(projectId: string) {
  return ['tasks', projectId] as const;
}

export function useTasks(projectId: string): UseQueryResult<Task[]> {
  return useQuery({
    queryKey: tasksQueryKey(projectId),
    queryFn: () => listTasks(projectId),
  });
}
