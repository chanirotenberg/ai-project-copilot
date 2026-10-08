import { apiFetch } from '../api/client';
import type { CreateTaskRequest, Task, UpdateTaskRequest } from './types';

/**
 * `GET /api/v1/tasks?projectId={projectId}` - server-side membership check on
 * `projectId` happens before fetching; a non-member gets a 404 (not 403),
 * matching the same anti-enumeration design as `GET /api/v1/projects/{id}`.
 */
export function listTasks(projectId: string): Promise<Task[]> {
  return apiFetch<Task[]>(`/api/v1/tasks?projectId=${encodeURIComponent(projectId)}`);
}

/** `POST /api/v1/tasks` - creates a task on the given project. */
export function createTask(request: CreateTaskRequest): Promise<Task> {
  return apiFetch<Task>('/api/v1/tasks', {
    method: 'POST',
    body: request,
  });
}

/**
 * `PUT /api/v1/tasks/{id}` - updates a task. Project membership is resolved
 * server-side from the task's own stored `ProjectId`, not from anything sent
 * here - the request body deliberately has no `id`/`projectId` field.
 */
export function updateTask(id: string, request: UpdateTaskRequest): Promise<Task> {
  return apiFetch<Task>(`/api/v1/tasks/${id}`, {
    method: 'PUT',
    body: request,
  });
}
