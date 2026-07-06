import { useEffect } from 'react';
import * as signalR from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';
import { ERROR_LOG_CLIENT_ID } from '../../api/errorLogClientId';
import type { ErrorLogDetail } from '../../api/types';
import { usePendingTransitions } from './PendingTransitionContext';
import { appendHistoryEntry, patchResolvedFields, removeFromMismatchedLists } from './errorLogCacheSync';

interface ResolvedChangedPayload {
  id: string;
  resolved: boolean;
  resolvedOn: string | null;
  comment: string;
  description: string | null;
  createdOn: string;
  originClientId: string | null;
}

interface CommentAddedPayload {
  id: string;
  comment: string;
  description: string | null;
  createdOn: string;
  originClientId: string | null;
}

// Live-updates this tab's cache when someone else (a different browser tab or user) resolves or
// comments on an error - "Facebook comment"-style, without a manual refresh. Broadcasts that
// originated from this same tab (originClientId === ERROR_LOG_CLIENT_ID) are skipped, since the
// mutation hook that made the change already applied it locally (and, for a resolve, is running
// its own 10-second grace period before removal - re-applying the broadcast would cut that short).
export function useErrorLogRealtime() {
  const queryClient = useQueryClient();
  const { markLeaving, unmarkLeaving } = usePendingTransitions();

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/errorlogs')
      .withAutomaticReconnect()
      .build();

    connection.on('errorResolvedChanged', (payload: ResolvedChangedPayload) => {
      if (payload.originClientId === ERROR_LOG_CLIENT_ID) return;

      patchResolvedFields(queryClient, payload.id, payload.resolved, payload.resolvedOn);
      appendHistoryEntry(queryClient, payload.id, {
        comment: payload.comment,
        description: payload.description,
        isResolved: payload.resolved,
        createdOn: payload.createdOn,
      });
      removeFromMismatchedLists(queryClient, payload.id, payload.resolved, markLeaving, unmarkLeaving);
    });

    connection.on('errorCommentAdded', (payload: CommentAddedPayload) => {
      if (payload.originClientId === ERROR_LOG_CLIENT_ID) return;

      const current = queryClient.getQueryData<ErrorLogDetail>(['errorLogDetail', payload.id]);
      appendHistoryEntry(queryClient, payload.id, {
        comment: payload.comment,
        description: payload.description,
        isResolved: current?.isResolved ?? false,
        createdOn: payload.createdOn,
      });
    });

    connection.start().catch((error) => console.error('Error-monitor live updates failed to connect', error));

    return () => {
      connection.stop();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);
}
