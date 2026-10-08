import { cleanup, render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from 'vitest';
import { fakeResponse } from '../test/fakeResponse';
import { TasksSection } from './TasksSection';
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

function renderTasksSection(projectId = 'project-1') {
  // Fresh QueryClient per test, with `retry: false` so a failing query
  // resolves (and the error state renders) without retry delay/flake.
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

  render(
    <QueryClientProvider client={queryClient}>
      <TasksSection projectId={projectId} />
    </QueryClientProvider>,
  );
}

describe('TasksSection', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    // `test.globals` is deliberately off (see vite.config.ts), so
    // Testing Library's own auto-cleanup never registers itself; clean up
    // explicitly instead.
    cleanup();
    vi.unstubAllGlobals();
  });

  it('shows a loading indicator while the tasks query is pending', async () => {
    let resolveFetch!: (value: Response) => void;
    (fetch as Mock).mockReturnValueOnce(
      new Promise<Response>((resolve) => {
        resolveFetch = resolve;
      }),
    );

    renderTasksSection();

    expect(screen.getByText(/loading tasks/i)).toBeInTheDocument();

    resolveFetch(fakeResponse(200, []));
    await waitFor(() => expect(screen.queryByText(/loading tasks/i)).not.toBeInTheDocument());
  });

  it('shows an empty-state message when there are no tasks', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, []));

    renderTasksSection();

    expect(await screen.findByText('No tasks yet.')).toBeInTheDocument();
  });

  it('renders real task fields for a non-empty list', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, [sampleTask]));

    renderTasksSection();

    await screen.findByText('Write spec');
    // Scoped to the row itself - the create form's own Priority <select>
    // also contains a "High" option, so an unscoped query would be ambiguous.
    const row = screen.getByText('Write spec').closest('li') as HTMLElement;
    expect(within(row).getByText(/Todo/)).toBeInTheDocument();
    expect(within(row).getByText(/High/)).toBeInTheDocument();
  });

  it('formats a task due date as DD/MM/YYYY, never the raw ISO string', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, [sampleTask]));

    renderTasksSection();

    expect(await screen.findByText(/Due date: 07\/11\/2026/)).toBeInTheDocument();
    expect(screen.queryByText(/2026-11-07T00:00:00Z/)).not.toBeInTheDocument();
  });

  it('shows "No due date" when a task has no due date', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, [{ ...sampleTask, dueDate: null }]));

    renderTasksSection();

    expect(await screen.findByText(/Due date: No due date/)).toBeInTheDocument();
  });

  it('shows a blocked indicator when isBlocked is true', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, [{ ...sampleTask, isBlocked: true }]));

    renderTasksSection();

    expect(await screen.findByText(/\(Blocked\)/)).toBeInTheDocument();
  });

  it('does not show a blocked indicator when isBlocked is false', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, [sampleTask]));

    renderTasksSection();

    await screen.findByText('Write spec');
    expect(screen.queryByText(/\(Blocked\)/)).not.toBeInTheDocument();
  });

  it('shows a generic error message when the tasks query fails', async () => {
    (fetch as Mock).mockResolvedValueOnce(
      fakeResponse(500, { status: 500, title: 'Server error.' }),
    );

    renderTasksSection();

    expect(await screen.findByRole('alert')).toHaveTextContent('Server error.');
  });

  it('blocks submission and does not call the backend when the title is empty', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, []));

    renderTasksSection();
    await screen.findByText('No tasks yet.');

    fireEvent.click(screen.getByRole('button', { name: /create task/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/title is required/i);
    // Only the initial list fetch happened — no create request was sent.
    expect(fetch).toHaveBeenCalledTimes(1);
  });

  it('creating a task sends a real POST with projectId, assignedUserId: null, and no extra fields', async () => {
    (fetch as Mock)
      .mockResolvedValueOnce(fakeResponse(200, [])) // initial list
      .mockResolvedValueOnce(fakeResponse(201, sampleTask)) // create
      .mockResolvedValueOnce(fakeResponse(200, [sampleTask])); // refetch after invalidation

    renderTasksSection('project-1');
    await screen.findByText('No tasks yet.');

    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Write spec' } });
    fireEvent.click(screen.getByRole('button', { name: /create task/i }));

    await waitFor(() => expect(screen.getAllByText('Write spec').length).toBeGreaterThan(0));

    const createCall = (fetch as Mock).mock.calls[1] as [string, RequestInit];
    const [, createOptions] = createCall;
    expect(createOptions.method).toBe('POST');
    const requestBody = JSON.parse(createOptions.body as string) as Record<string, unknown>;
    expect(requestBody).toEqual({
      title: 'Write spec',
      priority: 'Medium',
      projectId: 'project-1',
      assignedUserId: null,
    });
  });

  it('shows a friendly message for a backend DueDate validation error, not a generic failure message', async () => {
    (fetch as Mock)
      .mockResolvedValueOnce(fakeResponse(200, [])) // initial list
      .mockResolvedValueOnce(
        fakeResponse(400, {
          title: 'Validation failed.',
          status: 400,
          errors: { DueDate: ['Due date must not be before today.'] },
        }),
      );

    renderTasksSection();
    await screen.findByText('No tasks yet.');

    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Write spec' } });
    fireEvent.click(screen.getByRole('button', { name: /create task/i }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent("You can't choose a date in the past.");
    expect(alert).not.toHaveTextContent('Validation failed.');
  });

  it('shows a generic fallback message for a non-field create error', async () => {
    (fetch as Mock)
      .mockResolvedValueOnce(fakeResponse(200, [])) // initial list
      .mockResolvedValueOnce(fakeResponse(500, { status: 500, title: 'Server error.' }));

    renderTasksSection();
    await screen.findByText('No tasks yet.');

    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Write spec' } });
    fireEvent.click(screen.getByRole('button', { name: /create task/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Server error.');
  });

  it('a successful create clears the form fields and the list updates via refetch', async () => {
    (fetch as Mock)
      .mockResolvedValueOnce(fakeResponse(200, [])) // initial list
      .mockResolvedValueOnce(fakeResponse(201, sampleTask)) // create
      .mockResolvedValueOnce(fakeResponse(200, [sampleTask])); // refetch after invalidation

    renderTasksSection();
    await screen.findByText('No tasks yet.');

    const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
    fireEvent.change(titleInput, { target: { value: 'Write spec' } });
    fireEvent.click(screen.getByRole('button', { name: /create task/i }));

    await waitFor(() => expect(screen.getAllByText('Write spec').length).toBeGreaterThan(0));

    // Form field cleared after success.
    expect(titleInput.value).toBe('');
  });

  it('editing and saving a task updates the list via refetch (no manual merge)', async () => {
    const updatedTask: Task = { ...sampleTask, title: 'Write spec (updated)' };

    (fetch as Mock)
      .mockResolvedValueOnce(fakeResponse(200, [sampleTask])) // initial list
      .mockResolvedValueOnce(fakeResponse(200, updatedTask)) // PUT response
      .mockResolvedValueOnce(fakeResponse(200, [updatedTask])); // refetch after invalidation

    renderTasksSection();
    await screen.findByText('Write spec');

    // Scoped to the task row itself - the create form above it has its own
    // "Title" input/label, so an unscoped query would be ambiguous once the
    // row's edit form (with its own "Title" field) is open.
    const row = screen.getByText('Write spec').closest('li') as HTMLElement;
    fireEvent.click(within(row).getByRole('button', { name: /edit/i }));
    fireEvent.change(within(row).getByLabelText('Title'), {
      target: { value: 'Write spec (updated)' },
    });
    fireEvent.click(within(row).getByRole('button', { name: /save/i }));

    expect(await screen.findByText('Write spec (updated)')).toBeInTheDocument();
    expect(screen.queryByText('Write spec')).not.toBeInTheDocument();
  });
});
