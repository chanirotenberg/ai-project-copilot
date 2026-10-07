import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { listProjects } from './projectsApi';
import type { Project } from './types';

/** Shared query key for the projects list, reused by `useCreateProject` to invalidate it. */
export const PROJECTS_QUERY_KEY = ['projects'] as const;

export function useProjects(): UseQueryResult<Project[]> {
  return useQuery({
    queryKey: PROJECTS_QUERY_KEY,
    queryFn: listProjects,
  });
}
