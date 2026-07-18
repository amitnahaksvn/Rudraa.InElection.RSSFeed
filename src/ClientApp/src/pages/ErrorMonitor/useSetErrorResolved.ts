import { useMutation, useQueryClient } from '@tanstack/react-query';
import { setErrorResolved } from '../../api/errorLogs';
import { usePendingTransitions } from './PendingTransitionContext';
import { appendHistoryEntry, patchResolvedFields, removeFromMismatchedLists } from './errorLogCacheSync';

export function useSetErrorResolved() {
  const queryClient = useQueryClient();
  const { startTransition, markLeaving, unmarkLeaving } = usePendingTransitions();

  return useMutation({
    mutationFn: ({ id, resolved, comment, description }: { id: string; resolved: boolean; comment: string; description: string }) =>
      setErrorResolved(id, resolved, comment, description),
    onSuccess: (_data, { id, resolved, comment, description }) => {
      const resolvedOn = resolved ? new Date().toISOString() : null;

      // Patches happen immediately and everywhere (every cached status tab, plus the open detail
      // view) - the switch/status/history reflect the change right away. Nothing here removes a
      // row from any list, so no list "rebinds"/refetches because of this.
      patchResolvedFields(queryClient, id, resolved, resolvedOn);
      appendHistoryEntry(queryClient, id, {
        comment,
        description: description || null,
        isResolved: resolved,
        createdOn: resolvedOn ?? new Date().toISOString(),
      });

      startTransition(
        resolved
          ? 'Marked as resolved - will move to the Resolved tab in 10 seconds.'
          : 'Marked as unresolved - will move to the Unresolved tab in 10 seconds.',
        () => removeFromMismatchedLists(queryClient, id, resolved, markLeaving, unmarkLeaving),
      );
    },
  });
}
