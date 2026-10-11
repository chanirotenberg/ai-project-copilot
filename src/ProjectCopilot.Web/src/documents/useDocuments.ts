import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { listDocuments } from './documentsApi';
import type { Document } from './types';

/** Query key for a project's document list - scoped per project, not a single global list. */
export function documentsQueryKey(projectId: string) {
  return ['documents', projectId] as const;
}

export function useDocuments(projectId: string): UseQueryResult<Document[]> {
  return useQuery({
    queryKey: documentsQueryKey(projectId),
    queryFn: () => listDocuments(projectId),
  });
}
