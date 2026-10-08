import { useState } from 'react';
import { formatDate } from '../projects/formatDate';
import { getTaskErrorMessage } from './taskErrorMessages';
import { useUpdateTask } from './useUpdateTask';
import type { Task } from './types';

const STATUSES = ['Todo', 'InProgress', 'Done'] as const;
const PRIORITIES = ['Low', 'Medium', 'High', 'Critical'] as const;

/** Converts an ISO date/datetime string to the `YYYY-MM-DD` shape a native `<input type="date">` expects. */
function toDateInputValue(iso: string): string {
  const d = new Date(iso);
  const yyyy = d.getUTCFullYear();
  const mm = String(d.getUTCMonth() + 1).padStart(2, '0');
  const dd = String(d.getUTCDate()).padStart(2, '0');
  return `${yyyy}-${mm}-${dd}`;
}

/**
 * One task's row (Slice 1.11): a read-only display with an "Edit" button
 * that swaps in an inline edit form - no modal, no new UI framework.
 *
 * `projectId` is accepted so `useUpdateTask` can invalidate the right
 * project's task list query on success; it is never sent in the PUT body
 * itself (the backend resolves the task's project server-side from the
 * task's own stored `ProjectId`).
 */
export function TaskRow({ task, projectId }: { task: Task; projectId: string }) {
  const updateTaskMutation = useUpdateTask(projectId);

  const [isEditing, setIsEditing] = useState(false);
  const [title, setTitle] = useState(task.title);
  const [description, setDescription] = useState(task.description ?? '');
  const [status, setStatus] = useState<string>(task.status);
  const [priority, setPriority] = useState<string>(task.priority);
  const [dueDate, setDueDate] = useState(task.dueDate ? toDateInputValue(task.dueDate) : '');
  const [isBlocked, setIsBlocked] = useState(task.isBlocked);

  function startEditing() {
    // Re-seed the edit form from the current task prop every time editing
    // starts, so a previously cancelled edit never leaks stale values in.
    setTitle(task.title);
    setDescription(task.description ?? '');
    setStatus(task.status);
    setPriority(task.priority);
    setDueDate(task.dueDate ? toDateInputValue(task.dueDate) : '');
    setIsBlocked(task.isBlocked);
    updateTaskMutation.reset();
    setIsEditing(true);
  }

  function cancelEditing() {
    setIsEditing(false);
    updateTaskMutation.reset();
  }

  function handleSave() {
    const trimmedDescription = description.trim();

    updateTaskMutation.mutate(
      {
        id: task.id,
        request: {
          title: title.trim(),
          description: trimmedDescription ? trimmedDescription : undefined,
          status,
          priority,
          dueDate: dueDate ? dueDate : undefined,
          isBlocked,
        },
      },
      {
        onSuccess: () => {
          setIsEditing(false);
        },
        // On failure, deliberately stay in edit mode with the user's
        // in-progress edits intact - no reset of title/description/etc.
      },
    );
  }

  if (isEditing) {
    return (
      <li>
        <div>
          <label htmlFor={`task-${task.id}-title`}>Title</label>
          <input
            id={`task-${task.id}-title`}
            type="text"
            value={title}
            disabled={updateTaskMutation.isPending}
            onChange={(event) => setTitle(event.target.value)}
          />
        </div>
        <div>
          <label htmlFor={`task-${task.id}-description`}>Description</label>
          <textarea
            id={`task-${task.id}-description`}
            value={description}
            disabled={updateTaskMutation.isPending}
            onChange={(event) => setDescription(event.target.value)}
          />
        </div>
        <div>
          <label htmlFor={`task-${task.id}-status`}>Status</label>
          <select
            id={`task-${task.id}-status`}
            value={status}
            disabled={updateTaskMutation.isPending}
            onChange={(event) => setStatus(event.target.value)}
          >
            {STATUSES.map((value) => (
              <option key={value} value={value}>
                {value}
              </option>
            ))}
          </select>
        </div>
        <div>
          <label htmlFor={`task-${task.id}-priority`}>Priority</label>
          <select
            id={`task-${task.id}-priority`}
            value={priority}
            disabled={updateTaskMutation.isPending}
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
          <label htmlFor={`task-${task.id}-due-date`}>Due Date</label>
          <input
            id={`task-${task.id}-due-date`}
            type="date"
            value={dueDate}
            disabled={updateTaskMutation.isPending}
            onChange={(event) => setDueDate(event.target.value)}
          />
        </div>
        <div>
          <label htmlFor={`task-${task.id}-blocked`}>Blocked</label>
          <input
            id={`task-${task.id}-blocked`}
            type="checkbox"
            checked={isBlocked}
            disabled={updateTaskMutation.isPending}
            onChange={(event) => setIsBlocked(event.target.checked)}
          />
        </div>

        {updateTaskMutation.isError ? (
          <p role="alert">{getTaskErrorMessage(updateTaskMutation.error)}</p>
        ) : null}

        <button type="button" onClick={handleSave} disabled={updateTaskMutation.isPending}>
          {updateTaskMutation.isPending ? 'Saving…' : 'Save'}
        </button>
        <button type="button" onClick={cancelEditing} disabled={updateTaskMutation.isPending}>
          Cancel
        </button>
      </li>
    );
  }

  return (
    <li>
      <strong>{task.title}</strong> — {task.status} — {task.priority}
      {task.isBlocked ? <span> (Blocked)</span> : null}
      <p>Due date: {task.dueDate ? formatDate(task.dueDate) : 'No due date'}</p>
      <p>{task.description ? task.description : 'No description provided.'}</p>
      <button type="button" onClick={startEditing}>
        Edit
      </button>
    </li>
  );
}
