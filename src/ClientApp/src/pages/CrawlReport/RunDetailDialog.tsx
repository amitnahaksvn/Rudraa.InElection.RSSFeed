import { useQuery } from '@tanstack/react-query';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import IconButton from '@mui/material/IconButton';
import CloseIcon from '@mui/icons-material/Close';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Divider from '@mui/material/Divider';
import Alert from '@mui/material/Alert';
import CircularProgress from '@mui/material/CircularProgress';
import Chip from '@mui/material/Chip';
import Box from '@mui/material/Box';
import { fetchCrawlRunById } from '../../api/crawl';
import { RunStatusChip } from './RunStatusChip';
import { formatAbsoluteTime } from '../../utils/formatDate';
import { formatFullNumber } from '../../utils/formatNumber';

function elapsedSeconds(start: string, end: string | null): string {
  if (!end) return '—';
  const seconds = (new Date(end).getTime() - new Date(start).getTime()) / 1000;
  return seconds < 60 ? `${seconds.toFixed(1)}s` : `${Math.floor(seconds / 60)}m ${Math.round(seconds % 60)}s`;
}

export function RunDetailDialog({ runId, onClose }: { runId: string | null; onClose: () => void }) {
  const { data: run, isLoading, isError } = useQuery({
    queryKey: ['crawlRun', runId],
    queryFn: () => fetchCrawlRunById(runId!),
    enabled: runId !== null,
  });

  return (
    <Dialog open={runId !== null} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        Crawl run detail
        <IconButton onClick={onClose} size="small">
          <CloseIcon fontSize="small" />
        </IconButton>
      </DialogTitle>
      <DialogContent dividers>
        {isLoading && (
          <Stack alignItems="center" sx={{ py: 4 }}>
            <CircularProgress size={28} />
          </Stack>
        )}
        {isError && <Alert severity="error">Failed to load this run.</Alert>}
        {run && (
          <Stack gap={1.5}>
            <Stack direction="row" alignItems="center" justifyContent="space-between">
              <Typography variant="body2" color="text.secondary">
                {run.id}
              </Typography>
              <RunStatusChip status={run.status} />
            </Stack>

            <Stack direction="row" flexWrap="wrap" gap={0.75}>
              {run.providers.map((p) => (
                <Chip key={p} size="small" label={p} />
              ))}
            </Stack>

            <Divider />

            <Stack direction="row" justifyContent="space-between">
              <Typography variant="body2" color="text.secondary">
                Started
              </Typography>
              <Typography variant="body2">{formatAbsoluteTime(run.startTime)}</Typography>
            </Stack>
            <Stack direction="row" justifyContent="space-between">
              <Typography variant="body2" color="text.secondary">
                Ended
              </Typography>
              <Typography variant="body2">{run.endTime ? formatAbsoluteTime(run.endTime) : '—'}</Typography>
            </Stack>
            <Stack direction="row" justifyContent="space-between">
              <Typography variant="body2" color="text.secondary">
                Duration
              </Typography>
              <Typography variant="body2">{elapsedSeconds(run.startTime, run.endTime)}</Typography>
            </Stack>

            <Divider />

            <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 1 }}>
              {[
                ['Feeds/endpoints', run.feedCount],
                ['New', run.newArticles],
              ].map(([label, value]) => (
                <Stack key={label as string} alignItems="center">
                  <Typography variant="h6" fontWeight={700}>
                    {formatFullNumber(value as number)}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {label}
                  </Typography>
                </Stack>
              ))}
            </Box>

            {run.error && <Alert severity="error">{run.error}</Alert>}

            {run.failedFeeds.length > 0 && (
              <>
                <Divider />
                <Typography variant="subtitle2">Failed feeds/endpoints ({run.failedFeeds.length})</Typography>
                <Stack gap={0.5}>
                  {run.failedFeeds.map((f) => (
                    <Typography key={f} variant="body2" sx={{ fontFamily: 'monospace', fontSize: 13 }}>
                      {f}
                    </Typography>
                  ))}
                </Stack>
              </>
            )}
          </Stack>
        )}
      </DialogContent>
    </Dialog>
  );
}
