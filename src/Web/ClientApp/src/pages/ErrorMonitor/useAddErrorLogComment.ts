import { useMutation, useQueryClient } from '@tanstack/react-query';
import { addErrorLogComment } from '../../api/errorLogs';

export function useAddErrorLogComment() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, comment }: { id: string; comment: string }) => addErrorLogComment(id, comment),
    onSuccess: (_data, { id }) => {
      // A standalone comment never changes isResolved, so it can't move the row out of the
      // current status-filtered tab - safe to invalidate immediately, unlike the resolve mutation.
      queryClient.invalidateQueries({ queryKey: ['errorLogDetail', id] });
    },
  });
}
