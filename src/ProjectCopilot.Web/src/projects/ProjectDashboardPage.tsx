import { Link, useParams } from 'react-router-dom';
import { ApiError } from '../api/types';
import { DocumentsSection } from '../documents/DocumentsSection';
import { TasksSection } from '../tasks/TasksSection';
import { formatDate } from './formatDate';
import { useProject } from './useProject';

const GENERIC_LOAD_ERROR = 'Unable to load project. Please try again.';

// A nonexistent project and a project the user isn't a member of both return 404
// identically (see projectsApi.getProject) - this message is deliberately generic
// and never distinguishes the two, matching that anti-enumeration design.
const NOT_FOUND_MESSAGE = 'Project not found or unavailable.';

/**
 * Project Dashboard (Slice 1.10/1.11/1.13): project detail for a single project, by
 * id from the route, plus its Tasks and Documents sections. Project fields
 * themselves are read-only here (no edit/delete/members UI - later slices).
 */
export function ProjectDashboardPage() {
  const { projectId } = useParams<{ projectId: string }>();
  // react-router guarantees `projectId` is defined here - the route is only ever
  // matched as "/projects/:projectId", never without the param.
  const projectQuery = useProject(projectId!);

  return (
    <section>
      <p>
        <Link to="/" className="link-subtle">
          &larr; Back to Projects
        </Link>
      </p>

      {projectQuery.isPending ? <p className="text-muted">Loading project…</p> : null}

      {projectQuery.isError ? (
        <p className="alert" role="alert">
          {projectQuery.error instanceof ApiError && projectQuery.error.status === 404
            ? NOT_FOUND_MESSAGE
            : GENERIC_LOAD_ERROR}
        </p>
      ) : null}

      {projectQuery.data ? (
        <article className="card">
          <h2 className="card-title">{projectQuery.data.name}</h2>
          <p className="item-meta">
            Status:{' '}
            <span className={`badge badge-status-${projectQuery.data.status.toLowerCase()}`}>
              {projectQuery.data.status}
            </span>
          </p>
          <p className="item-meta">
            Deadline:{' '}
            {projectQuery.data.deadline ? formatDate(projectQuery.data.deadline) : 'No deadline'}
          </p>
          <p
            className={
              projectQuery.data.description ? 'item-description' : 'item-description text-muted'
            }
          >
            {projectQuery.data.description
              ? projectQuery.data.description
              : 'No description provided.'}
          </p>
        </article>
      ) : null}

      {projectQuery.data ? <TasksSection projectId={projectId!} /> : null}
      {projectQuery.data ? <DocumentsSection projectId={projectId!} /> : null}
    </section>
  );
}
