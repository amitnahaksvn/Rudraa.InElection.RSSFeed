import { useState } from 'react';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TablePagination from '@mui/material/TablePagination';
import TableRow from '@mui/material/TableRow';
import Typography from '@mui/material/Typography';
import Tooltip from '@mui/material/Tooltip';
import CircularProgress from '@mui/material/CircularProgress';
import IconButton from '@mui/material/IconButton';
import Stack from '@mui/material/Stack';
import RefreshIcon from '@mui/icons-material/Refresh';
import Box from '@mui/material/Box';
import type { JobExecutionLog, JobExecutionStatusName } from '../../api/jobReportTypes';
import { useJobExecutionLogs } from './useJobExecutionLogs';
import { JobExecutionStatusChip } from './JobExecutionStatusChip';
import { formatAbsoluteTime, formatRelativeTime } from '../../utils/formatDate';

const PAGE_SIZE_OPTIONS = [10, 25, 50];

function elapsed(start: string, end: string | null): string {
  if (!end) return '—';
  const seconds = (new Date(end).getTime() - new Date(start).getTime()) / 1000;
  return seconds < 60 ? `${seconds.toFixed(1)}s` : `${Math.floor(seconds / 60)}m ${Math.round(seconds % 60)}s`;
}

export function JobExecutionLogsTable({
  jobId,
  status,
}: {
  jobId: string | null;
  status: JobExecutionStatusName | null;
}) {
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(25);

  const { data, isLoading, isFetching, refetch } = useJobExecutionLogs(jobId, status, page, pageSize);
  const logs: JobExecutionLog[] = data ?? [];

  return (
    <Box sx={{ position: 'relative' }}>
      <Stack direction="row" justifyContent="flex-end" sx={{ px: 1, pt: 1 }}>
        <Tooltip title="Refresh">
          <span>
            <IconButton size="small" onClick={() => refetch()} disabled={isFetching} aria-label="Refresh job executions">
              <RefreshIcon
                fontSize="small"
                sx={{
                  animation: isFetching ? 'jobReportRefreshSpin 1s linear infinite' : 'none',
                  '@keyframes jobReportRefreshSpin': {
                    from: { transform: 'rotate(0deg)' },
                    to: { transform: 'rotate(360deg)' },
                  },
                }}
              />
            </IconButton>
          </span>
        </Tooltip>
      </Stack>

      <TableContainer sx={{ maxHeight: 560, opacity: isFetching && !isLoading ? 0.6 : 1, transition: 'opacity 0.15s' }}>
        <Table size="small" stickyHeader>
          <TableHead>
            <TableRow>
              <TableCell>Job</TableCell>
              <TableCell>Started</TableCell>
              <TableCell>Ended</TableCell>
              <TableCell>Duration</TableCell>
              <TableCell>Status</TableCell>
              <TableCell>Error</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {logs.map((log) => (
              <TableRow key={log.id} hover>
                <TableCell>
                  <Typography variant="body2">{log.jobName}</Typography>
                  <Typography variant="caption" color="text.secondary">
                    {log.jobId}
                  </Typography>
                </TableCell>
                <TableCell>
                  <Tooltip title={formatAbsoluteTime(log.startedAt)}>
                    <span>{formatRelativeTime(log.startedAt)}</span>
                  </Tooltip>
                </TableCell>
                <TableCell>{log.completedAt ? formatAbsoluteTime(log.completedAt) : '—'}</TableCell>
                <TableCell sx={{ fontVariantNumeric: 'tabular-nums' }}>{elapsed(log.startedAt, log.completedAt)}</TableCell>
                <TableCell>
                  <JobExecutionStatusChip status={log.status} />
                </TableCell>
                <TableCell sx={{ maxWidth: 320 }}>
                  {log.errorMessage ? (
                    <Tooltip title={log.errorMessage}>
                      <Typography variant="body2" color="error.main" noWrap>
                        {log.errorMessage}
                      </Typography>
                    </Tooltip>
                  ) : (
                    '—'
                  )}
                </TableCell>
              </TableRow>
            ))}
            {!isLoading && logs.length === 0 && (
              <TableRow>
                <TableCell colSpan={6} align="center">
                  <Typography variant="body2" color="text.secondary" sx={{ py: 3 }}>
                    No job executions recorded yet.
                  </Typography>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>

      {isLoading && (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 3 }}>
          <CircularProgress size={24} />
        </Box>
      )}

      <TablePagination
        component="div"
        count={-1}
        rowsPerPageOptions={PAGE_SIZE_OPTIONS}
        page={page}
        rowsPerPage={pageSize}
        labelDisplayedRows={({ from: f, to: t }) => `${f}–${t}`}
        onPageChange={(_, newPage) => setPage(newPage)}
        onRowsPerPageChange={(e) => {
          setPageSize(parseInt(e.target.value, 10));
          setPage(0);
        }}
        // No total-count endpoint - same "disable next once a page comes back short" convention as
        // CrawlReport's RecentRunsTable.
        slotProps={{ actions: { nextButton: { disabled: logs.length < pageSize } } }}
      />
    </Box>
  );
}
