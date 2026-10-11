import { cleanup, render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from 'vitest';
import { fakeResponse } from '../test/fakeResponse';
import { DocumentRow } from './DocumentRow';
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

function renderDocumentRow(document: Document = sampleDocument, projectId = 'project-1') {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

  render(
    <QueryClientProvider client={queryClient}>
      <ul>
        <DocumentRow document={document} projectId={projectId} />
      </ul>
    </QueryClientProvider>,
  );
}

function lastCallWithMethod(method: string): [string, RequestInit] {
  const calls = (fetch as Mock).mock.calls as [string, RequestInit][];
  const call = calls.find(([, options]) => options?.method === method);
  if (!call) {
    throw new Error(`No ${method} call was made`);
  }
  return call;
}

describe('DocumentRow', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it('clicking Edit opens the inline edit form pre-populated with the document current values', () => {
    renderDocumentRow();

    fireEvent.click(screen.getByRole('button', { name: /edit/i }));

    expect(screen.getByLabelText('Title')).toHaveValue('Architecture Notes');
    expect(screen.getByLabelText('Description')).toHaveValue('Notes from the design review');
  });

  it('saving sends a PUT to the correct document id with exactly the editable fields, no projectId/id/createdByUserId', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, sampleDocument));

    renderDocumentRow();
    fireEvent.click(screen.getByRole('button', { name: /edit/i }));
    fireEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(1));

    const [url, options] = lastCallWithMethod('PUT');
    expect(url).toContain('/api/v1/documents/document-1');
    expect(options.method).toBe('PUT');

    const requestBody = JSON.parse(options.body as string) as Record<string, unknown>;
    expect(requestBody).toEqual({
      title: 'Architecture Notes',
      description: 'Notes from the design review',
    });
    expect(requestBody.projectId).toBeUndefined();
    expect(requestBody.id).toBeUndefined();
    expect(requestBody.createdByUserId).toBeUndefined();
  });

  it('changing Title and saving sends the new title value', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, sampleDocument));

    renderDocumentRow();
    fireEvent.click(screen.getByRole('button', { name: /edit/i }));
    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Updated Title' } });
    fireEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(1));

    const [, options] = lastCallWithMethod('PUT');
    const requestBody = JSON.parse(options.body as string) as Record<string, unknown>;
    expect(requestBody.title).toBe('Updated Title');
  });

  it('clearing the description omits it from the update request', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, sampleDocument));

    renderDocumentRow();
    fireEvent.click(screen.getByRole('button', { name: /edit/i }));
    fireEvent.change(screen.getByLabelText('Description'), { target: { value: '' } });
    fireEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(1));

    const [, options] = lastCallWithMethod('PUT');
    const requestBody = JSON.parse(options.body as string) as Record<string, unknown>;
    expect('description' in requestBody).toBe(false);
  });

  it('a successful update exits edit mode', async () => {
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(200, sampleDocument));

    renderDocumentRow();
    fireEvent.click(screen.getByRole('button', { name: /edit/i }));
    fireEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => expect(screen.queryByLabelText('Description')).not.toBeInTheDocument());
    expect(screen.getByRole('button', { name: /edit/i })).toBeInTheDocument();
  });

  it('a validation error during update shows a specific friendly message and keeps the edit form open with edits intact', async () => {
    (fetch as Mock).mockResolvedValueOnce(
      fakeResponse(400, {
        title: 'Validation failed.',
        status: 400,
        errors: { Title: ['Title must not be empty.'] },
      }),
    );

    renderDocumentRow();
    fireEvent.click(screen.getByRole('button', { name: /edit/i }));
    fireEvent.change(screen.getByLabelText('Title'), { target: { value: '' } });
    fireEvent.click(screen.getByRole('button', { name: /save/i }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Document title is required.');
    expect(alert).not.toHaveTextContent('Validation failed.');

    // The edit form is still open, with the user's in-progress edit intact.
    expect(screen.getByLabelText('Title')).toHaveValue('');
    expect(screen.getByLabelText('Description')).toBeInTheDocument();
  });

  it('the Cancel button exits edit mode without sending any request', () => {
    renderDocumentRow();

    fireEvent.click(screen.getByRole('button', { name: /edit/i }));
    fireEvent.change(screen.getByLabelText('Title'), {
      target: { value: 'Changed but discarded' },
    });
    fireEvent.click(screen.getByRole('button', { name: /cancel/i }));

    expect(screen.queryByLabelText('Title')).not.toBeInTheDocument();
    expect(screen.getByText('Architecture Notes')).toBeInTheDocument();
    expect(fetch).not.toHaveBeenCalled();
  });

  it('clicking Delete and declining the confirmation does not send a request', () => {
    vi.spyOn(window, 'confirm').mockReturnValue(false);

    renderDocumentRow();
    fireEvent.click(screen.getByRole('button', { name: /delete/i }));

    expect(window.confirm).toHaveBeenCalled();
    expect(fetch).not.toHaveBeenCalled();
  });

  it('clicking Delete and confirming sends a DELETE to the correct document id', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    (fetch as Mock).mockResolvedValueOnce(fakeResponse(204, undefined));

    renderDocumentRow();
    fireEvent.click(screen.getByRole('button', { name: /delete/i }));

    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(1));

    const [url, options] = (fetch as Mock).mock.calls[0] as [string, RequestInit];
    expect(url).toContain('/api/v1/documents/document-1');
    expect(options.method).toBe('DELETE');
  });

  it('a delete error is shown and the item stays in the list', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    (fetch as Mock).mockResolvedValueOnce(
      fakeResponse(500, { status: 500, title: 'Server error.' }),
    );

    renderDocumentRow();
    fireEvent.click(screen.getByRole('button', { name: /delete/i }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Server error.');
    expect(screen.getByText('Architecture Notes')).toBeInTheDocument();
  });
});
