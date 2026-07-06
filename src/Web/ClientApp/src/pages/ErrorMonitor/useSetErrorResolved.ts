import { useMutation, useQueryClient, type InfiniteData } from '@tanstack/react-query';
import { setErrorResolved } from '../../api/errorLogs';
import { usePendingTransitions } from './PendingTransitionContext';
import type { ErrorLogSummary, PagedResult } from '../../api/types';

export function useSetErrorResolved() {
  const queryClient = useQueryClient();
  const { startTransition } = usePendingTransitions();

  return useMutation({
    mutationFn: ({ id, resolved, comment }: { id: string; resolved: boolean; comment: string }) =>
      setErrorResolved(id, resolved, comment),
    onSuccess: (_data, { id, resolved }) => {
      const resolvedOn = resolved ? new Date().toISOString() : null;

      // Patch every cached errorLogs page in place so the switch/status flips immediately
      // without the row being removed or reordered yet - the actual invalidation (which is what
      // would drop it out of a status-filtered tab) is deferred to the timer below, giving the
      // "will move to the Resolved tab in 10 seconds" grace period instead of an instant jump.
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

      queryClient.setQueryData(['errorLogDetail', id], (detail: unknown) =>
        detail && typeof detail === 'object' ? { ...detail, isResolved: resolved, resolvedOn } : detail,
      );

      startTransition(
        resolved
          ? 'Marked as resolved - will move to the Resolved tab in 10 seconds.'
          : 'Marked as unresolved - will move to the Unresolved tab in 10 seconds.',
        () => {
          queryClient.invalidateQueries({ queryKey: ['errorLogs'] });
          queryClient.invalidateQueries({ queryKey: ['errorLogDetail', id] });
        },
      );
    },
  });
}
