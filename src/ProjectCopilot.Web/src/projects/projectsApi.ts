import { apiFetch } from '../api/client';
import type { CreateProjectRequest, Project } from './types';

/** `GET /api/v1/projects` — server-side scoped to the authenticated user's memberships. */
export function listProjects(): Promise<Project[]> {
  return apiFetch<Project[]>('/api/v1/projects');
}

/** `POST /api/v1/projects` — creates a project (and its owning membership) for the authenticated user. */
export function createProject(request: CreateProjectRequest): Promise<Project> {
  return apiFetch<Project>('/api/v1/projects', {
    method: 'POST',
    body: request,
  });
}
