import { useState, type FormEvent } from 'react';
import { ApiError } from '../api/types';
import { TaskRow } from './TaskRow';
import { getTaskErrorMessage } from './taskErrorMessages';
import { useCreateTask } from './useCreateTask';
import { useTasks } from './useTasks';

const GENERIC_LOAD_ERROR = 'Unable to load tasks. Please try again.';

const PRIORITIES = ['Low', 'Medium', 'High', 'Critical'] as const;

/**
 * Tasks section (Slice 1.11): real list + create for a single project's
 * tasks, embedded in the Project Dashboard. Editing an existing task is
 * handled per-row by `TaskRow`.
 *
 * `status`/`isBlocked`/`assignedUserId` are never exposed as create inputs -
 * the backend hardcodes/ignores them on creation (status always `"Todo"`,
 * no assignee picker exists), matching the same "only real data sources"
 * rule `ProjectsPage` follows for `status`.
 */
export function TasksSection({ projectId }: { projectId: string }) {
  const tasksQuery = useTasks(projectId);
  const createTaskMutation = useCreateTask(projectId);

  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [priority, setPriority] = useState<string>('Medium');
  const [dueDate, setDueDate] = useState('');
  const [validationError, setValidationError] = useState<string | null>(null);

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const trimmedTitle = title.trim();
    if (!trimmedTitle) {
      // Clear any stale server-side error from a previous failed attempt so
      // it can't render alongside this new client-side validation error.
      createTaskMutation.reset();
      setValidationError('Task title is required.');
      return;
    }

    setValidationError(null);

    const trimmedDescription = description.trim();

    createTaskMutation.mutate(
      {
        title: trimmedTitle,
        description: trimmedDescription ? trimmedDescription : undefined,
        priority,
        dueDate: dueDate ? dueDate : undefined,
      },
      {
        onSuccess: () => {
          setTitle('');
          setDescription('');
          setPriority('Medium');
          setDueDate('');
        },
      },
    );
  }

  return (
    <section>
      <h3>Tasks</h3>

      <form onSubmit={handleSubmit} noValidate>
        <div>
          <label htmlFor="task-title">Title</label>
          <input
            id="task-title"
            name="title"
            type="text"
            value={title}
            disabled={createTaskMutation.isPending}
            onChange={(event) => setTitle(event.target.value)}
          />
        </div>
        <div>
          <label htmlFor="task-description">Description</label>
          <textarea
            id="task-description"
            name="description"
            value={description}
            disabled={createTaskMutation.isPending}
            onChange={(event) => setDescription(event.target.value)}
          />
        </div>
        <div>
          <label htmlFor="task-priority">Priority</label>
          <select
            id="task-priority"
            name="priority"
            value={priority}
            disabled={createTaskMutation.isPending}
            onChange={(event) => setPriority(event.target.value)}
          >
            {PRIORITIES.map((value) => (
              <option key={value} value={value}>
                {value}
              </option>
            ))}
          </select>
        </div>
        <div>
          <label htmlFor="task-due-date">Due Date</label>
          <input
            id="task-due-date"
            name="dueDate"
            type="date"
            value={dueDate}
            disabled={createTaskMutation.isPending}
            onChange={(event) => setDueDate(event.target.value)}
          />
        </div>

        {validationError ? <p role="alert">{validationError}</p> : null}
        {createTaskMutation.isError ? (
          <p role="alert">{getTaskErrorMessage(createTaskMutation.error)}</p>
        ) : null}

        <button type="submit" disabled={createTaskMutation.isPending}>
          {createTaskMutation.isPending ? 'Creating…' : 'Create task'}
        </button>
      </form>

      {tasksQuery.isPending ? <p>Loading tasks…</p> : null}

      {tasksQuery.isError ? (
        <p role="alert">
          {tasksQuery.error instanceof ApiError ? tasksQuery.error.message : GENERIC_LOAD_ERROR}
        </p>
      ) : null}

      {!tasksQuery.isPending && !tasksQuery.isError && tasksQuery.data ? (
        tasksQuery.data.length === 0 ? (
          <p>No tasks yet.</p>
        ) : (
          <ul>
            {tasksQuery.data.map((task) => (
              <TaskRow key={task.id} task={task} projectId={projectId} />
            ))}
          </ul>
        )
      ) : null}
    </section>
  );
}
