import { useState, type FormEvent } from 'react';
import { ApiError } from '../api/types';
import { useCreateProject } from './useCreateProject';
import { useProjects } from './useProjects';

const GENERIC_CREATE_ERROR = 'Unable to create project. Please try again.';
const GENERIC_LOAD_ERROR = 'Unable to load projects. Please try again.';

/**
 * Formats an ISO deadline as DD/MM/YYYY, always, regardless of browser locale.
 *
 * Uses UTC getters (not local getters) deliberately: the deadline represents a calendar
 * day, not a precise instant, and local-timezone getters could shift the displayed day
 * depending on the viewer's timezone offset relative to the stored UTC-midnight value.
 */
function formatDeadline(iso: string): string {
  const d = new Date(iso);
  const dd = String(d.getUTCDate()).padStart(2, '0');
  const mm = String(d.getUTCMonth() + 1).padStart(2, '0');
  const yyyy = d.getUTCFullYear();
  return `${dd}/${mm}/${yyyy}`;
}

/**
 * Projects screen (Slice 1.9): real list + create, against the real API.
 *
 * Only data the backend returns is shown (no client-side filtering/faking).
 * `status` is never exposed as an input — the backend hardcodes it on
 * creation and it is not client-settable.
 */
export function ProjectsPage() {
  const projectsQuery = useProjects();
  const createProjectMutation = useCreateProject();

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [deadline, setDeadline] = useState('');
  const [validationError, setValidationError] = useState<string | null>(null);

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const trimmedName = name.trim();
    if (!trimmedName) {
      // Clear any stale server-side error from a previous failed attempt so
      // it can't render alongside this new client-side validation error.
      createProjectMutation.reset();
      setValidationError('Project name is required.');
      return;
    }

    setValidationError(null);

    const trimmedDescription = description.trim();

    createProjectMutation.mutate(
      {
        name: trimmedName,
        description: trimmedDescription ? trimmedDescription : undefined,
        deadline: deadline ? deadline : undefined,
      },
      {
        onSuccess: () => {
          setName('');
          setDescription('');
          setDeadline('');
        },
      },
    );
  }

  return (
    <section>
      <h2>Projects</h2>

      <form onSubmit={handleSubmit} noValidate>
        <div>
          <label htmlFor="project-name">Name</label>
          <input
            id="project-name"
            name="name"
            type="text"
            value={name}
            disabled={createProjectMutation.isPending}
            onChange={(event) => setName(event.target.value)}
          />
        </div>
        <div>
          <label htmlFor="project-description">Description</label>
          <textarea
            id="project-description"
            name="description"
            value={description}
            disabled={createProjectMutation.isPending}
            onChange={(event) => setDescription(event.target.value)}
          />
        </div>
        <div>
          <label htmlFor="project-deadline">Deadline</label>
          <input
            id="project-deadline"
            name="deadline"
            type="date"
            value={deadline}
            disabled={createProjectMutation.isPending}
            onChange={(event) => setDeadline(event.target.value)}
          />
        </div>

        {validationError ? <p role="alert">{validationError}</p> : null}
        {createProjectMutation.isError ? (
          <p role="alert">
            {createProjectMutation.error instanceof ApiError
              ? createProjectMutation.error.message
              : GENERIC_CREATE_ERROR}
          </p>
        ) : null}

        <button type="submit" disabled={createProjectMutation.isPending}>
          {createProjectMutation.isPending ? 'Creating…' : 'Create project'}
        </button>
      </form>

      {projectsQuery.isPending ? <p>Loading projects…</p> : null}

      {projectsQuery.isError ? (
        <p role="alert">
          {projectsQuery.error instanceof ApiError
            ? projectsQuery.error.message
            : GENERIC_LOAD_ERROR}
        </p>
      ) : null}

      {!projectsQuery.isPending && !projectsQuery.isError && projectsQuery.data ? (
        projectsQuery.data.length === 0 ? (
          <p>No projects yet. Create one above to get started.</p>
        ) : (
          <ul>
            {projectsQuery.data.map((project) => (
              <li key={project.id}>
                <strong>{project.name}</strong> — {project.status}
                {project.deadline ? <span> (due {formatDeadline(project.deadline)})</span> : null}
              </li>
            ))}
          </ul>
        )
      ) : null}
    </section>
  );
}
