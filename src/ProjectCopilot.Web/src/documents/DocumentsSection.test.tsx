import { cleanup, render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from 'vitest';
import { fakeResponse } from '../test/fakeResponse';
import { DocumentsSection } from './DocumentsSection';
import type { Document } from './types';

const sampleDocument: Document = {
  id: 'document-1',
  projectId: 'project-1',
  title: 'Architecture Notes',
  description: 'Notes from the design review',
  createdByUserId: 'user-1',
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
};

function renderDocumentsSection(projectId = 'project-1') {
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
      <DocumentsSection projectId={projectId} />
    </QueryClientProvider>,
  );
}

describe('DocumentsSection', () => {
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

  it('shows a loading indicator while the documents query is pending', async () => {
    let resolveFetch!: (value: Response) => void;
    (fetch as Mock).mockReturnValueOnce(
      new Promise<Response>((resolve) => {
        resolveFetch = resolve;
      }),
    );

    renderDocumentsSection();

    expect(screen.getByText(/loading documents/i)).toBeInTheDocument();

    resolveFetch(fakeResponse(200, []));
    await waitFor(() => expect(screen.queryByText(/loading documents/i)).not.toBeInTheDocument());
  });

  it('shows an empty-state message when there are no documents', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, []));

    renderDocumentsSection();

    expect(await screen.findByText('No documents yet.')).toBeInTheDocument();
  });

  it('renders real document fields for a non-empty list', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, [sampleDocument]));

    renderDocumentsSection();

    await screen.findByText('Architecture Notes');
    expect(screen.getByText('Notes from the design review')).toBeInTheDocument();
  });

  it('shows a friendly placeholder when description is null', async () => {
    (fetch as Mock).mockResolvedValueOnce(
      fakeResponse(200, [{ ...sampleDocument, description: null }]),
    );

    renderDocumentsSection();

    expect(await screen.findByText('No description provided.')).toBeInTheDocument();
  });

  it('shows a friendly placeholder when description is an empty string', async () => {
    (fetch as Mock).mockResolvedValueOnce(
      fakeResponse(200, [{ ...sampleDocument, description: '' }]),
    );

    renderDocumentsSection();

    expect(await screen.findByText('No description provided.')).toBeInTheDocument();
  });

  it('shows a generic error message when the documents query fails', async () => {
    (fetch as Mock).mockResolvedValueOnce(
      fakeResponse(500, { status: 500, title: 'Server error.' }),
    );

    renderDocumentsSection();

    expect(await screen.findByRole('alert')).toHaveTextContent('Server error.');
  });

  it('blocks submission and does not call the backend when the title is empty', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, []));

    renderDocumentsSection();
    await screen.findByText('No documents yet.');

    fireEvent.click(screen.getByRole('button', { name: /create document/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/title is required/i);
    // Only the initial list fetch happened — no create request was sent.
    expect(fetch).toHaveBeenCalledTimes(1);
  });

  it('creating a document sends a real POST with projectId and no extra fields, and never sends createdByUserId', async () => {
    (fetch as Mock)
      .mockResolvedValueOnce(fakeResponse(200, [])) // initial list
      .mockResolvedValueOnce(fakeResponse(201, sampleDocument)) // create
      .mockResolvedValueOnce(fakeResponse(200, [sampleDocument])); // refetch after invalidation

    renderDocumentsSection('project-1');
    await screen.findByText('No documents yet.');

    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Architecture Notes' } });
    fireEvent.click(screen.getByRole('button', { name: /create document/i }));

    await waitFor(() =>
      expect(screen.getAllByText('Architecture Notes').length).toBeGreaterThan(0),
    );

    const createCall = (fetch as Mock).mock.calls[1] as [string, RequestInit];
    const [, createOptions] = createCall;
    expect(createOptions.method).toBe('POST');
    const requestBody = JSON.parse(createOptions.body as string) as Record<string, unknown>;
    expect(requestBody).toEqual({
      title: 'Architecture Notes',
      projectId: 'project-1',
    });
    expect(requestBody.createdByUserId).toBeUndefined();
  });

  it('shows a friendly message for a backend Title-too-long validation error', async () => {
    (fetch as Mock)
      .mockResolvedValueOnce(fakeResponse(200, [])) // initial list
      .mockResolvedValueOnce(
        fakeResponse(400, {
          title: 'Validation failed.',
          status: 400,
          errors: { Title: ['Title must be 200 characters or fewer.'] },
        }),
      );

    renderDocumentsSection();
    await screen.findByText('No documents yet.');

    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Architecture Notes' } });
    fireEvent.click(screen.getByRole('button', { name: /create document/i }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Document title must be 200 characters or fewer.');
    expect(alert).not.toHaveTextContent('Validation failed.');
  });

  it('shows a friendly message for a backend Description-too-long validation error', async () => {
    (fetch as Mock)
      .mockResolvedValueOnce(fakeResponse(200, [])) // initial list
      .mockResolvedValueOnce(
        fakeResponse(400, {
          title: 'Validation failed.',
          status: 400,
          errors: { Description: ['Description must be 2000 characters or fewer.'] },
        }),
      );

    renderDocumentsSection();
    await screen.findByText('No documents yet.');

    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Architecture Notes' } });
    fireEvent.click(screen.getByRole('button', { name: /create document/i }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Description must be 2000 characters or fewer.');
    expect(alert).not.toHaveTextContent('Validation failed.');
  });

  it('shows a generic fallback message for a non-field create error', async () => {
    (fetch as Mock)
      .mockResolvedValueOnce(fakeResponse(200, [])) // initial list
      .mockResolvedValueOnce(fakeResponse(500, { status: 500, title: 'Server error.' }));

    renderDocumentsSection();
    await screen.findByText('No documents yet.');

    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Architecture Notes' } });
    fireEvent.click(screen.getByRole('button', { name: /create document/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Server error.');
  });

  it('a successful create clears the form fields and the list updates via refetch', async () => {
    (fetch as Mock)
      .mockResolvedValueOnce(fakeResponse(200, [])) // initial list
      .mockResolvedValueOnce(fakeResponse(201, sampleDocument)) // create
      .mockResolvedValueOnce(fakeResponse(200, [sampleDocument])); // refetch after invalidation

    renderDocumentsSection();
    await screen.findByText('No documents yet.');

    const titleInput = screen.getByLabelText('Title') as HTMLInputElement;
    fireEvent.change(titleInput, { target: { value: 'Architecture Notes' } });
    fireEvent.click(screen.getByRole('button', { name: /create document/i }));

    await waitFor(() =>
      expect(screen.getAllByText('Architecture Notes').length).toBeGreaterThan(0),
    );

    // Form field cleared after success.
    expect(titleInput.value).toBe('');
  });
});
