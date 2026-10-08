import { cleanup, render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from 'vitest';
import { fakeResponse } from '../test/fakeResponse';
import { TaskRow } from './TaskRow';
import type { Task } from './types';

const sampleTask: Task = {
  id: 'task-1',
  projectId: 'project-1',
  title: 'Write spec',
  description: 'Draft the SDD section',
  status: 'Todo',
  priority: 'High',
  assignedUserId: null,
  dueDate: '2026-11-07T00:00:00Z',
  isBlocked: false,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
};

function renderTaskRow(task: Task = sampleTask, projectId = 'project-1') {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

  render(
    <QueryClientProvider client={queryClient}>
      <ul>
        <TaskRow task={task} projectId={projectId} />
      </ul>
    </QueryClientProvider>,
  );
}

function lastPutCall(): [string, RequestInit] {
  const calls = (fetch as Mock).mock.calls as [string, RequestInit][];
  const putCall = calls.find(([, options]) => options?.method === 'PUT');
  if (!putCall) {
    throw new Error('No PUT call was made');
  }
  return putCall;
}

describe('TaskRow', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it('clicking Edit opens the inline edit form pre-populated with the task current values', () => {
    renderTaskRow();

    fireEvent.click(screen.getByRole('button', { name: /edit/i }));

    expect(screen.getByLabelText('Title')).toHaveValue('Write spec');
    expect(screen.getByLabelText('Description')).toHaveValue('Draft the SDD section');
    expect(screen.getByLabelText('Status')).toHaveValue('Todo');
    expect(screen.getByLabelText('Priority')).toHaveValue('High');
    expect(screen.getByLabelText('Due Date')).toHaveValue('2026-11-07');
    expect(screen.getByLabelText('Blocked')).not.toBeChecked();
  });

  it('saving sends a PUT to the correct task id with exactly the editable fields, no projectId and no client-chosen assignedUserId', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, sampleTask));

    renderTaskRow();
    fireEvent.click(screen.getByRole('button', { name: /edit/i }));
    fireEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(1));

    const [url, options] = lastPutCall();
    expect(url).toContain('/api/v1/tasks/task-1');
    expect(options.method).toBe('PUT');

    const requestBody = JSON.parse(options.body as string) as Record<string, unknown>;
    expect(requestBody).toEqual({
      title: 'Write spec',
      description: 'Draft the SDD section',
      status: 'Todo',
      priority: 'High',
      assignedUserId: null,
      dueDate: '2026-11-07',
      isBlocked: false,
    });
    expect(requestBody.projectId).toBeUndefined();
    expect(requestBody.id).toBeUndefined();
  });

  it('changing Status and saving sends the new status value', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, sampleTask));

    renderTaskRow();
    fireEvent.click(screen.getByRole('button', { name: /edit/i }));
    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'Done' } });
    fireEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(1));

    const [, options] = lastPutCall();
    const requestBody = JSON.parse(options.body as string) as Record<string, unknown>;
    expect(requestBody.status).toBe('Done');
  });

  it('changing Priority and saving sends the new priority value', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, sampleTask));

    renderTaskRow();
    fireEvent.click(screen.getByRole('button', { name: /edit/i }));
    fireEvent.change(screen.getByLabelText('Priority'), { target: { value: 'Critical' } });
    fireEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(1));

    const [, options] = lastPutCall();
    const requestBody = JSON.parse(options.body as string) as Record<string, unknown>;
    expect(requestBody.priority).toBe('Critical');
  });

  it('toggling IsBlocked and saving sends the new boolean value', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, sampleTask));

    renderTaskRow();
    fireEvent.click(screen.getByRole('button', { name: /edit/i }));
    fireEvent.click(screen.getByLabelText('Blocked'));
    fireEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(1));

    const [, options] = lastPutCall();
    const requestBody = JSON.parse(options.body as string) as Record<string, unknown>;
    expect(requestBody.isBlocked).toBe(true);
  });

  it('updating the due date to a new value sends that new value', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, sampleTask));

    renderTaskRow();
    fireEvent.click(screen.getByRole('button', { name: /edit/i }));
    fireEvent.change(screen.getByLabelText('Due Date'), { target: { value: '2026-12-25' } });
    fireEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(1));

    const [, options] = lastPutCall();
    const requestBody = JSON.parse(options.body as string) as Record<string, unknown>;
    expect(requestBody.dueDate).toBe('2026-12-25');
  });

  it('clearing the due date omits it from the update request', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, sampleTask));

    renderTaskRow();
    fireEvent.click(screen.getByRole('button', { name: /edit/i }));
    fireEvent.change(screen.getByLabelText('Due Date'), { target: { value: '' } });
    fireEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(1));

    const [, options] = lastPutCall();
    const requestBody = JSON.parse(options.body as string) as Record<string, unknown>;
    expect('dueDate' in requestBody).toBe(false);
  });

  it('a successful update exits edit mode', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, sampleTask));

    renderTaskRow();
    fireEvent.click(screen.getByRole('button', { name: /edit/i }));
    fireEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => expect(screen.queryByLabelText('Status')).not.toBeInTheDocument());
    expect(screen.getByRole('button', { name: /edit/i })).toBeInTheDocument();
  });

  it('a validation error during update shows a specific friendly message and keeps the edit form open', async () => {
    (fetch as Mock).mockResolvedValueOnce(
      fakeResponse(400, {
        title: 'Validation failed.',
        status: 400,
        errors: { Title: ['Title must not be empty.'] },
      }),
    );

    renderTaskRow();
    fireEvent.click(screen.getByRole('button', { name: /edit/i }));
    fireEvent.change(screen.getByLabelText('Title'), { target: { value: '' } });
    fireEvent.click(screen.getByRole('button', { name: /save/i }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Task title is required.');
    expect(alert).not.toHaveTextContent('Validation failed.');

    // The edit form is still open, with the user's in-progress edit intact.
    expect(screen.getByLabelText('Title')).toHaveValue('');
    expect(screen.getByLabelText('Description')).toBeInTheDocument();
  });

  it('the Cancel button exits edit mode without sending any request', () => {
    renderTaskRow();

    fireEvent.click(screen.getByRole('button', { name: /edit/i }));
    fireEvent.change(screen.getByLabelText('Title'), {
      target: { value: 'Changed but discarded' },
    });
    fireEvent.click(screen.getByRole('button', { name: /cancel/i }));

    expect(screen.queryByLabelText('Title')).not.toBeInTheDocument();
    expect(screen.getByText('Write spec')).toBeInTheDocument();
    expect(fetch).not.toHaveBeenCalled();
  });
});
