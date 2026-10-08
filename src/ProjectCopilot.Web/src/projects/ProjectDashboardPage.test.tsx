import { cleanup, render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from 'vitest';
import { fakeResponse } from '../test/fakeResponse';
import { ProjectDashboardPage } from './ProjectDashboardPage';
import type { Project } from './types';

const sampleProject: Project = {
  id: 'project-1',
  name: 'Demo Project',
  description: 'A real description',
  status: 'Active',
  deadline: '2026-11-07T00:00:00Z',
  createdByUserId: 'user-1',
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
};

function renderDashboard(projectId = 'project-1') {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
    },
  });

  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[`/projects/${projectId}`]}>
        <Routes>
          <Route path="/projects/:projectId" element={<ProjectDashboardPage />} />
          <Route path="/" element={<p>Projects List Stub</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('ProjectDashboardPage', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it('shows a loading indicator while the project query is pending', async () => {
    let resolveFetch!: (value: Response) => void;
    (fetch as Mock).mockReturnValueOnce(
      new Promise<Response>((resolve) => {
        resolveFetch = resolve;
      }),
    );

    renderDashboard();

    expect(screen.getByText(/loading project/i)).toBeInTheDocument();

    resolveFetch(fakeResponse(200, sampleProject));
    await waitFor(() => expect(screen.queryByText(/loading project/i)).not.toBeInTheDocument());
  });

  it('fetches the project id from the route and renders the real fields returned by the backend', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, sampleProject));

    renderDashboard('project-1');

    expect(await screen.findByText('Demo Project')).toBeInTheDocument();
    expect(screen.getByText(/Status: Active/)).toBeInTheDocument();
    expect(screen.getByText('A real description')).toBeInTheDocument();

    // GET goes through the existing apiFetch (Authorization header etc.) at exactly the
    // route's project id - no new fetch wrapper, no client-supplied id substitution.
    const [calledUrl] = (fetch as Mock).mock.calls[0] as [string];
    expect(calledUrl).toContain('/api/v1/projects/project-1');
  });

  it('formats the deadline as DD/MM/YYYY, never the raw ISO string', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, sampleProject));

    renderDashboard();

    expect(await screen.findByText(/Deadline: 07\/11\/2026/)).toBeInTheDocument();
    expect(screen.queryByText(/2026-11-07T00:00:00Z/)).not.toBeInTheDocument();
  });

  it('shows a friendly "No deadline" state when deadline is null', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, { ...sampleProject, deadline: null }));

    renderDashboard();

    expect(await screen.findByText(/Deadline: No deadline/)).toBeInTheDocument();
  });

  it('shows a friendly placeholder when description is null', async () => {
    (fetch as Mock).mockResolvedValueOnce(
      fakeResponse(200, { ...sampleProject, description: null }),
    );

    renderDashboard();

    expect(await screen.findByText('No description provided.')).toBeInTheDocument();
    expect(screen.queryByText('undefined')).not.toBeInTheDocument();
  });

  it('shows a friendly placeholder when description is an empty string', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, { ...sampleProject, description: '' }));

    renderDashboard();

    expect(await screen.findByText('No description provided.')).toBeInTheDocument();
  });

  it('shows a generic "not found or unavailable" message on 404, without distinguishing why', async () => {
    (fetch as Mock).mockResolvedValueOnce(
      fakeResponse(404, {
        status: 404,
        title: 'Resource not found.',
        detail: "Project with id 'project-1' was not found.",
      }),
    );

    renderDashboard();

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Project not found or unavailable.');
    // Never echo the backend's own detail - that would reintroduce the exact
    // existence-vs-permission distinction the 404 contract deliberately hides.
    expect(alert).not.toHaveTextContent('was not found');
  });

  it('shows a generic error message on a non-404 failure', async () => {
    (fetch as Mock).mockResolvedValueOnce(
      fakeResponse(500, { status: 500, title: 'Server error.' }),
    );

    renderDashboard();

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Unable to load project. Please try again.');
  });

  it('navigates back to the projects list via the back link', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, sampleProject));

    renderDashboard();
    await screen.findByText('Demo Project');

    fireEvent.click(screen.getByRole('link', { name: /back to projects/i }));

    expect(await screen.findByText('Projects List Stub')).toBeInTheDocument();
  });
});
