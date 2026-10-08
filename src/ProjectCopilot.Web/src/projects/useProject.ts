import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { getProject } from './projectsApi';
import type { Project } from './types';

/** Query key for a single project by id — distinct from the list key (`PROJECTS_QUERY_KEY`). */
export function projectQueryKey(projectId: string) {
  return ['projects', projectId] as const;
}

export function useProject(projectId: string): UseQueryResult<Project> {
  return useQuery({
    queryKey: projectQueryKey(projectId),
    queryFn: () => getProject(projectId),
  });
}
