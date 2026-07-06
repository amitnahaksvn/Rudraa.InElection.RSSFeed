import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import Divider from '@mui/material/Divider';
import Switch from '@mui/material/Switch';
import FormControlLabel from '@mui/material/FormControlLabel';
import CircularProgress from '@mui/material/CircularProgress';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import CommentIcon from '@mui/icons-material/Comment';
import { fetchErrorLogDetail } from '../../api/errorLogs';
import { formatAbsoluteTime } from '../../utils/formatDate';
import { useSetErrorResolved } from './useSetErrorResolved';
import { useAddErrorLogComment } from './useAddErrorLogComment';
import { CommentDialog } from './CommentDialog';
import { HistoryTimeline } from './HistoryTimeline';

function DetailField({ label, value }: { label: string; value: string | number | null | undefined }) {
  if (value === null || value === undefined || value === '') return null;
  return (
    <Box sx={{ minWidth: 0 }}>
      <Typography variant="caption" color="text.secondary" display="block">
        {label}
      </Typography>
      <Typography variant="body2" sx={{ wordBreak: 'break-word' }}>
        {value}
      </Typography>
    </Box>
  );
}

function CodeBlock({ label, content }: { label: string; content: string | null | undefined }) {
  if (!content) return null;
  return (
    <Box>
      <Typography variant="caption" color="text.secondary" display="block" sx={{ mb: 0.5 }}>
        {label}
      </Typography>
      <Box
        component="pre"
        sx={{
          m: 0,
          p: 1.5,
          borderRadius: 1.5,
          bgcolor: (theme) => (theme.palette.mode === 'dark' ? 'rgba(255,255,255,0.04)' : 'rgba(0,0,0,0.04)'),
          fontFamily: 'ui-monospace, Consolas, monospace',
          fontSize: 12.5,
          maxHeight: 320,
          overflow: 'auto',
          whiteSpace: 'pre-wrap',
          wordBreak: 'break-word',
        }}
      >
        {content}
      </Box>
    </Box>
  );
}

interface ErrorDetailPaneProps {
  errorId: string;
  onBack?: () => void;
}

// The "reading pane" of the Outlook-style layout - full detail for whichever error is selected in
// ErrorListRow, including the resolve toggle, standalone comments, and the history timeline.
export function ErrorDetailPane({ errorId, onBack }: ErrorDetailPaneProps) {
  const [pendingResolvedTarget, setPendingResolvedTarget] = useState<boolean | null>(null);
  const [addCommentOpen, setAddCommentOpen] = useState(false);
  const setResolved = useSetErrorResolved();
  const addComment = useAddErrorLogComment();

  const detailQuery = useQuery({
    queryKey: ['errorLogDetail', errorId],
    queryFn: () => fetchErrorLogDetail(errorId),
  });

  const detail = detailQuery.data;

  const closeResolveDialog = () => {
    setPendingResolvedTarget(null);
    setResolved.reset();
  };

  const confirmResolve = (comment: string, description: string) => {
    if (pendingResolvedTarget === null) return;
    setResolved.mutate(
      { id: errorId, resolved: pendingResolvedTarget, comment, description },
      { onSuccess: () => setPendingResolvedTarget(null) },
    );
  };

  const closeAddCommentDialog = () => {
    setAddCommentOpen(false);
    addComment.reset();
  };

  return (
    <Box sx={{ height: '100%', overflow: 'auto' }}>
      {detailQuery.isLoading && (
        <Stack direction="row" alignItems="center" gap={1} sx={{ p: 2 }}>
          <CircularProgress size={16} />
          <Typography variant="body2" color="text.secondary">
            Loading details...
          </Typography>
        </Stack>
      )}

      {detailQuery.isError && (
        <Typography variant="body2" color="error" sx={{ p: 2 }}>
          Failed to load details.
        </Typography>
      )}

      {detail && (
        <Stack gap={2} sx={{ p: { xs: 2, md: 3 } }}>
          <Stack direction="row" alignItems="flex-start" gap={1}>
            {onBack && (
              <IconButton onClick={onBack} size="small" sx={{ mt: 0.25 }}>
                <ArrowBackIcon fontSize="small" />
              </IconButton>
            )}
            <Box sx={{ minWidth: 0 }}>
              <Typography variant="h6" sx={{ wordBreak: 'break-word' }}>
                {detail.exceptionType}
              </Typography>
              <Typography variant="body2" color="text.secondary" sx={{ whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>
                {detail.message}
              </Typography>
            </Box>
          </Stack>

          <Stack direction="row" justifyContent="space-between" alignItems="center" flexWrap="wrap" gap={1}>
            <FormControlLabel
              control={
                <Stack direction="row" alignItems="center" gap={0.5}>
                  <Switch
                    checked={detail.isResolved}
                    disabled={setResolved.isPending}
                    onChange={(e) => setPendingResolvedTarget(e.target.checked)}
                  />
                  {setResolved.isPending && <CircularProgress size={14} />}
                </Stack>
              }
              label={detail.isResolved ? 'Marked resolved' : 'Mark as resolved'}
            />
            {detail.resolvedOn && (
              <Typography variant="caption" color="text.secondary">
                Resolved {formatAbsoluteTime(detail.resolvedOn)}
              </Typography>
            )}
          </Stack>

          {setResolved.isError && (
            <Alert severity="error" onClose={() => setResolved.reset()}>
              {(setResolved.error as Error).message}
            </Alert>
          )}

          <Stack direction="row" gap={0.75} flexWrap="wrap">
            {detail.provider && <Chip size="small" label={`Provider: ${detail.provider}`} />}
            {detail.country && <Chip size="small" label={`Country: ${detail.country}`} />}
            {detail.feedOrApiName && <Chip size="small" label={detail.feedOrApiName} />}
            <Chip size="small" label={detail.source} />
            <Chip size="small" variant="outlined" label={detail.applicationName} />
            {detail.httpStatusCode && <Chip size="small" variant="outlined" color="warning" label={`HTTP ${detail.httpStatusCode}`} />}
          </Stack>

          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fill, minmax(160px, 1fr))',
              gap: 1.5,
            }}
          >
            <DetailField label="Error Code" value={detail.errorCode} />
            <DetailField label="Environment" value={detail.environment} />
            <DetailField label="Service" value={detail.serviceName} />
            <DetailField label="Machine" value={detail.machineName} />
            <DetailField label="Assembly Version" value={detail.assemblyVersion} />
            <DetailField label="Request Path" value={detail.requestPath} />
            <DetailField label="HTTP Method" value={detail.httpMethod} />
            <DetailField label="Trace Id" value={detail.traceId} />
            <DetailField label="Correlation Id" value={detail.correlationId} />
            <DetailField label="Hangfire Job Id" value={detail.hangfireJobId} />
            <DetailField label="IP Address" value={detail.ipAddress} />
            <DetailField label="Execution Duration" value={detail.executionDuration} />
            <DetailField label="Created" value={formatAbsoluteTime(detail.createdOn)} />
          </Box>

          <CodeBlock label="Source URL / Query String" content={[detail.sourceUrl, detail.queryString].filter(Boolean).join('\n')} />
          <CodeBlock label="Inner Exception" content={detail.innerException} />
          <CodeBlock label="Stack Trace" content={detail.stackTrace} />
          <CodeBlock label="Request Body" content={detail.requestBody} />
          <CodeBlock label="Response Body" content={detail.responseBody} />
          <CodeBlock label="Additional Data" content={detail.additionalData} />

          <Divider />

          <Stack direction="row" justifyContent="space-between" alignItems="center">
            <Typography variant="subtitle2">History</Typography>
            <Button size="small" startIcon={<CommentIcon />} onClick={() => setAddCommentOpen(true)}>
              Add comment
            </Button>
          </Stack>
          <HistoryTimeline history={detail.history} />
        </Stack>
      )}

      <CommentDialog
        open={pendingResolvedTarget !== null}
        title={pendingResolvedTarget ? 'Mark as resolved' : 'Mark as unresolved'}
        helperText="A comment is required and will be recorded in this error's history."
        confirmLabel={pendingResolvedTarget ? 'Mark resolved' : 'Mark unresolved'}
        submitting={setResolved.isPending}
        errorMessage={setResolved.isError ? (setResolved.error as Error).message : null}
        onCancel={closeResolveDialog}
        onConfirm={confirmResolve}
      />

      <CommentDialog
        open={addCommentOpen}
        title="Add comment"
        confirmLabel="Add comment"
        submitting={addComment.isPending}
        errorMessage={addComment.isError ? (addComment.error as Error).message : null}
        onCancel={closeAddCommentDialog}
        onConfirm={(comment, description) =>
          addComment.mutate({ id: errorId, comment, description }, { onSuccess: () => setAddCommentOpen(false) })
        }
      />
    </Box>
  );
}
