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

const CREATE_PROJECT_FIELDS = ['Name', 'Description', 'Deadline'] as const;

/**
 * Maps a known backend validation message for one Create Project field to a
 * clearer, user-facing message. Matched by substring (not exact string) so a
 * minor backend wording change doesn't silently break the mapping - it just
 * falls through to the raw backend message instead (see
 * `getCreateProjectErrorMessage`'s fallback).
 *
 * Deliberately narrow: only the Create Project fields/messages that exist
 * today. Not a general validation-translation framework - no i18n
 * infrastructure, no generic 401/404/500 handling here by design.
 */
function toFriendlyFieldMessage(field: string, rawMessage: string): string | undefined {
  switch (field) {
    case 'Deadline':
      // Backend: "Deadline must not be before today."
      return "You can't choose a date in the past.";
    case 'Name':
      if (/must not be empty/i.test(rawMessage)) {
        return 'Project name is required.';
      }
      if (/200 characters/i.test(rawMessage)) {
        return 'Project name must be 200 characters or fewer.';
      }
      if (/already exists/i.test(rawMessage)) {
        // Already clear as written by the backend - pass it through verbatim
        // rather than inventing a reworded version that could drift from it.
        return rawMessage;
      }
      return undefined;
    case 'Description':
      if (/2000 characters/i.test(rawMessage)) {
        return 'Description must be 2000 characters or fewer.';
      }
      return undefined;
    default:
      return undefined;
  }
}

/**
 * Resolves the message to show for a failed Create Project submission.
 *
 * Prefers a friendly message for a known Create-Project field error (read
 * from the backend's ValidationProblemDetails `errors` map, preserved on
 * `ApiError.body`). Falls back to the raw `detail`/`title`-derived
 * `ApiError.message` when there's no field-level error to map (e.g. a
 * generic 500, or a validation message this function doesn't recognize) -
 * this is the same fallback behavior as before this change, not a new gap.
 */
function getCreateProjectErrorMessage(error: unknown): string {
  if (error instanceof ApiError && error.body?.errors) {
    for (const field of CREATE_PROJECT_FIELDS) {
      const messages = error.body.errors[field];
      const friendly = messages?.[0] ? toFriendlyFieldMessage(field, messages[0]) : undefined;
      if (friendly) {
        return friendly;
      }
    }
  }

  return error instanceof ApiError ? error.message : GENERIC_CREATE_ERROR;
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
          <p role="alert">{getCreateProjectErrorMessage(createProjectMutation.error)}</p>
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
