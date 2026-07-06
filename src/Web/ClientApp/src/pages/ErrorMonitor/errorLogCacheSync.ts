import type { InfiniteData, QueryClient } from '@tanstack/react-query';
import type { ErrorLogDetail, ErrorLogFilters, ErrorLogHistoryEntry, ErrorLogSummary, PagedResult } from '../../api/types';

// How long ErrorListRow's exit Collapse animation runs before a row actually leaves its array - kept
// here (not just in ErrorListRow) since removeFromMismatchedLists needs to wait the same amount
// before mutating the cache, or the row would vanish mid-animation instead of sliding away.
export const EXIT_ANIMATION_MS = 320;

function queryMatchesResolvedStatus(cachedFilters: ErrorLogFilters | undefined, resolved: boolean): boolean {
  if (!cachedFilters || cachedFilters.status === 'all') return true;
  return (cachedFilters.status === 'resolved') === resolved;
}

// Updates isResolved/resolvedOn on a summary row in-place across every cached errorLogs list -
// every status tab's cache gets patched, not just the currently visible one, so switching tabs
// later still shows the right state - and on the errorLogDetail cache if that error is open.
// Never touches totalCount/pages/removes anything, so this alone can never cause a visible
// "rebind" of any list.
export function patchResolvedFields(
  queryClient: QueryClient,
  id: string,
  resolved: boolean,
  resolvedOn: string | null,
) {
  queryClient.setQueriesData<InfiniteData<PagedResult<ErrorLogSummary>>>({ queryKey: ['errorLogs'] }, (data) => {
    if (!data) return data;
    return {
      ...data,
      pages: data.pages.map((page) => ({
        ...page,
        items: page.items.map((item) => (item.id === id ? { ...item, isResolved: resolved, resolvedOn } : item)),
      })),
    };
  });

  queryClient.setQueryData<ErrorLogDetail>(['errorLogDetail', id], (detail) =>
    detail ? { ...detail, isResolved: resolved, resolvedOn } : detail,
  );
}

// Prepends a new history entry to a cached errorLogDetail (history is stored newest-first) - a
// no-op if that error's detail isn't currently cached (e.g. nobody has it expanded).
export function appendHistoryEntry(queryClient: QueryClient, id: string, entry: ErrorLogHistoryEntry) {
  queryClient.setQueryData<ErrorLogDetail>(['errorLogDetail', id], (detail) =>
    detail ? { ...detail, history: [entry, ...detail.history] } : detail,
  );
}

// For every cached errorLogs list whose own status filter no longer matches `resolved` (e.g. an
// "Unresolved" tab after this row was just resolved), plays an exit animation (markLeaving) and
// only then - once that animation has had time to finish - actually drops the row from that
// list's cached pages. Lists whose filter still matches (e.g. "All", or "Resolved" after this
// exact change) are left untouched. This never calls invalidate/refetch, so any other list a user
// has open keeps its scroll position and doesn't refetch just because one row changed elsewhere.
export function removeFromMismatchedLists(
  queryClient: QueryClient,
  id: string,
  resolved: boolean,
  markLeaving: (id: string) => void,
  unmarkLeaving: (id: string) => void,
) {
  const queries = queryClient.getQueryCache().findAll({ queryKey: ['errorLogs'] });
  const mismatched = queries.filter((query) => {
    const cachedFilters = query.queryKey[1] as ErrorLogFilters | undefined;
    return !queryMatchesResolvedStatus(cachedFilters, resolved);
  });

  if (mismatched.length === 0) return;

  markLeaving(id);
  setTimeout(() => {
    for (const query of mismatched) {
      queryClient.setQueryData<InfiniteData<PagedResult<ErrorLogSummary>>>(query.queryKey, (data) => {
        if (!data) return data;
        return {
          ...data,
          pages: data.pages.map((page) => {
            const items = page.items.filter((item) => item.id !== id);
            return items.length === page.items.length ? page : { ...page, items, totalCount: Math.max(0, page.totalCount - 1) };
          }),
        };
      });
    }
    unmarkLeaving(id);
  }, EXIT_ANIMATION_MS);
}
