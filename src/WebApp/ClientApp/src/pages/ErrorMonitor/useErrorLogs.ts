import { useInfiniteQuery } from '@tanstack/react-query';
import { fetchErrorLogs } from '../../api/errorLogs';
import type { ErrorLogFilters } from '../../api/types';

export function useErrorLogs(filters: ErrorLogFilters) {
  return useInfiniteQuery({
    queryKey: ['errorLogs', filters],
    queryFn: ({ pageParam }) => fetchErrorLogs(pageParam, filters),
    initialPageParam: 1,
    getNextPageParam: (lastPage) => (lastPage.hasMore ? lastPage.page + 1 : undefined),
    // A resolve/unresolve mutation invalidates this query (see useSetErrorResolved) - refetching
    // from page 1 rather than trying to patch one item across however many pages are loaded,
    // simplest correct behavior given rows can move between the unresolved/resolved groups.
  });
}
