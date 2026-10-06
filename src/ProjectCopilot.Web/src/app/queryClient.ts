import { QueryClient } from '@tanstack/react-query';

/**
 * Single shared TanStack Query client for the app.
 *
 * No queries/mutations are defined yet in this slice; feature hooks are
 * added in later slices (Projects, Tasks, Documents, ...).
 */
export const queryClient = new QueryClient();
