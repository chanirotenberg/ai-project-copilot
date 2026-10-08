import { Link, useParams } from 'react-router-dom';
import { ApiError } from '../api/types';
import { formatDeadline } from './formatDeadline';
import { useProject } from './useProject';

const GENERIC_LOAD_ERROR = 'Unable to load project. Please try again.';

// A nonexistent project and a project the user isn't a member of both return 404
// identically (see projectsApi.getProject) - this message is deliberately generic
// and never distinguishes the two, matching that anti-enumeration design.
const NOT_FOUND_MESSAGE = 'Project not found or unavailable.';

/**
 * Project Dashboard (Slice 1.10): real project detail for a single project, by id
 * from the route. Read-only - no edit/delete/members/tasks UI here (later slices).
 */
export function ProjectDashboardPage() {
  const { projectId } = useParams<{ projectId: string }>();
  // react-router guarantees `projectId` is defined here - the route is only ever
  // matched as "/projects/:projectId", never without the param.
  const projectQuery = useProject(projectId!);

  return (
    <section>
      <p>
        <Link to="/">&larr; Back to Projects</Link>
      </p>

      {projectQuery.isPending ? <p>Loading project…</p> : null}

      {projectQuery.isError ? (
        <p role="alert">
          {projectQuery.error instanceof ApiError && projectQuery.error.status === 404
            ? NOT_FOUND_MESSAGE
            : GENERIC_LOAD_ERROR}
        </p>
      ) : null}

      {projectQuery.data ? (
        <article>
          <h2>{projectQuery.data.name}</h2>
          <p>Status: {projectQuery.data.status}</p>
          <p>
            Deadline:{' '}
            {projectQuery.data.deadline
              ? formatDeadline(projectQuery.data.deadline)
              : 'No deadline'}
          </p>
          <p>
            {projectQuery.data.description
              ? projectQuery.data.description
              : 'No description provided.'}
          </p>
        </article>
      ) : null}
    </section>
  );
}
