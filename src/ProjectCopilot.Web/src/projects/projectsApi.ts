import { apiFetch } from '../api/client';
import type { CreateProjectRequest, Project } from './types';

/** `GET /api/v1/projects` — server-side scoped to the authenticated user's memberships. */
export function listProjects(): Promise<Project[]> {
  return apiFetch<Project[]>('/api/v1/projects');
}

/**
 * `GET /api/v1/projects/{id}` — a non-existent project and a project the
 * authenticated user isn't a member of both return 404 identically (by
 * design, to avoid leaking which case it is). Callers should show one
 * generic "not found or unavailable" message for 404, not try to
 * distinguish the two.
 */
export function getProject(id: string): Promise<Project> {
  return apiFetch<Project>(`/api/v1/projects/${id}`);
}

/** `POST /api/v1/projects` — creates a project (and its owning membership) for the authenticated user. */
export function createProject(request: CreateProjectRequest): Promise<Project> {
  return apiFetch<Project>('/api/v1/projects', {
    method: 'POST',
    body: request,
  });
}
