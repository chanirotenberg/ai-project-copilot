import { cleanup, render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from 'vitest';
import { fakeResponse } from '../test/fakeResponse';
import { ProjectsPage } from './ProjectsPage';
import type { Project } from './types';

const sampleProject: Project = {
  id: 'project-1',
  name: 'Demo Project',
  description: 'A demo project',
  status: 'Active',
  deadline: null,
  createdByUserId: 'user-1',
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
};

function renderProjectsPage() {
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
      <ProjectsPage />
    </QueryClientProvider>,
  );
}

describe('ProjectsPage', () => {
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

  it('renders the list of projects returned by the backend', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, [sampleProject]));

    renderProjectsPage();

    expect(await screen.findByText('Demo Project')).toBeInTheDocument();
  });

  it('renders a project deadline as DD/MM/YYYY regardless of the raw ISO format', async () => {
    const projectWithDeadline: Project = {
      ...sampleProject,
      id: 'project-2',
      name: 'Deadline Project',
      deadline: '2026-11-07T00:00:00Z',
    };
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, [projectWithDeadline]));

    renderProjectsPage();

    expect(await screen.findByText('Deadline Project')).toBeInTheDocument();
    expect(screen.getByText(/\(due 07\/11\/2026\)/)).toBeInTheDocument();
    expect(screen.queryByText(/2026-11-07T00:00:00Z/)).not.toBeInTheDocument();
  });

  it('shows a loading indicator while the projects query is pending', async () => {
    let resolveFetch!: (value: Response) => void;
    (fetch as Mock).mockReturnValueOnce(
      new Promise<Response>((resolve) => {
        resolveFetch = resolve;
      }),
    );

    renderProjectsPage();

    expect(screen.getByText(/loading projects/i)).toBeInTheDocument();

    resolveFetch(fakeResponse(200, []));
    await waitFor(() => expect(screen.queryByText(/loading projects/i)).not.toBeInTheDocument());
  });

  it('shows an error message when the projects query fails', async () => {
    (fetch as Mock).mockResolvedValueOnce(
      fakeResponse(500, { status: 500, title: 'Server error.' }),
    );

    renderProjectsPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('Server error.');
  });

  it('shows an empty-state message when there are no projects', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, []));

    renderProjectsPage();

    expect(await screen.findByText(/no projects yet/i)).toBeInTheDocument();
  });

  it('blocks submission and does not call the backend when the name is empty', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, []));

    renderProjectsPage();
    await screen.findByText(/no projects yet/i);

    fireEvent.click(screen.getByRole('button', { name: /create project/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/name is required/i);
    // Only the initial list fetch happened — no create request was sent.
    expect(fetch).toHaveBeenCalledTimes(1);
  });

  it('creating a project sends a real POST (with no client-supplied owner id) and the list updates via refetch', async () => {
    (fetch as Mock)
      .mockResolvedValueOnce(fakeResponse(200, [])) // initial list
      .mockResolvedValueOnce(fakeResponse(201, sampleProject)) // create
      .mockResolvedValueOnce(fakeResponse(200, [sampleProject])); // refetch after invalidation

    renderProjectsPage();
    await screen.findByText(/no projects yet/i);

    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Demo Project' } });
    fireEvent.click(screen.getByRole('button', { name: /create project/i }));

    await waitFor(() => expect(screen.getByText('Demo Project')).toBeInTheDocument());

    const createCall = (fetch as Mock).mock.calls[1] as [string, RequestInit];
    const [, createOptions] = createCall;
    expect(createOptions.method).toBe('POST');
    const requestBody = JSON.parse(createOptions.body as string) as Record<string, unknown>;
    expect(requestBody).toEqual({ name: 'Demo Project' });
    expect(requestBody.ownerId).toBeUndefined();
    expect(requestBody.userId).toBeUndefined();
    expect(requestBody.createdByUserId).toBeUndefined();
  });

  it('shows a generic error and keeps the page usable when create fails', async () => {
    (fetch as Mock)
      .mockResolvedValueOnce(fakeResponse(200, [])) // initial list
      .mockResolvedValueOnce(fakeResponse(400, { status: 400, title: 'Validation failed.' }));

    renderProjectsPage();
    await screen.findByText(/no projects yet/i);

    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Demo Project' } });
    fireEvent.click(screen.getByRole('button', { name: /create project/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Validation failed.');
    expect(screen.getByRole('button', { name: /create project/i })).toBeInTheDocument();
  });

  it('clears a stale server-side error once a new client-side validation error replaces it', async () => {
    (fetch as Mock)
      .mockResolvedValueOnce(fakeResponse(200, [])) // initial list
      .mockResolvedValueOnce(fakeResponse(400, { status: 400, title: 'Validation failed.' })); // failed create

    renderProjectsPage();
    await screen.findByText(/no projects yet/i);

    // First attempt: valid name, but the server rejects it -> mutation error renders.
    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Demo Project' } });
    fireEvent.click(screen.getByRole('button', { name: /create project/i }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Validation failed.');

    // Second attempt: clear the name and resubmit -> only the client-side
    // validation error should render, not both the old and the new one.
    fireEvent.change(screen.getByLabelText('Name'), { target: { value: '' } });
    fireEvent.click(screen.getByRole('button', { name: /create project/i }));

    const alerts = await screen.findAllByRole('alert');
    expect(alerts).toHaveLength(1);
    expect(alerts[0]).toHaveTextContent(/name is required/i);

    // No additional request was sent for the blocked second submission.
    expect(fetch).toHaveBeenCalledTimes(2);
  });
});
