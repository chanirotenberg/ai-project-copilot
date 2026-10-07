import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { createProject } from './projectsApi';
import { PROJECTS_QUERY_KEY } from './useProjects';
import type { CreateProjectRequest, Project } from './types';

/**
 * Create-project mutation. On success, invalidates the projects list query
 * so it refetches and shows the new project without a manual page reload.
 */
export function useCreateProject(): UseMutationResult<Project, Error, CreateProjectRequest> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: createProject,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: PROJECTS_QUERY_KEY });
    },
  });
}
