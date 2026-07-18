import { useMutation, useQueryClient } from '@tanstack/react-query';
import { addErrorLogComment } from '../../api/errorLogs';
import { appendHistoryEntry } from './errorLogCacheSync';
import type { ErrorLogDetail } from '../../api/types';

export function useAddErrorLogComment() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, comment, description }: { id: string; comment: string; description: string }) =>
      addErrorLogComment(id, comment, description),
    onSuccess: (_data, { id, comment, description }) => {
      // A standalone comment never changes isResolved, so it can't move the row out of a
      // status-filtered list - patch the new entry straight into the cache instead of a refetch.
      const current = queryClient.getQueryData<ErrorLogDetail>(['errorLogDetail', id]);
      appendHistoryEntry(queryClient, id, {
        comment,
        description: description || null,
        isResolved: current?.isResolved ?? false,
        createdOn: new Date().toISOString(),
      });
    },
  });
}
