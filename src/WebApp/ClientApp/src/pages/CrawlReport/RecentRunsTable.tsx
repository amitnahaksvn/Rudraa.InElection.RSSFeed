import { useState } from 'react';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TablePagination from '@mui/material/TablePagination';
import TableRow from '@mui/material/TableRow';
import Typography from '@mui/material/Typography';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import VisibilityIcon from '@mui/icons-material/Visibility';
import CircularProgress from '@mui/material/CircularProgress';
import Box from '@mui/material/Box';
import type { CrawlHistoryRun, CrawlPipelineName } from '../../api/crawlTypes';
import { useCrawlHistory } from './useCrawlHistory';
import { RunStatusChip } from './RunStatusChip';
import { formatAbsoluteTime, formatRelativeTime } from '../../utils/formatDate';
import { formatFullNumber } from '../../utils/formatNumber';
import { RunDetailDialog } from './RunDetailDialog';

const PAGE_SIZE_OPTIONS = [10, 25, 50];

export function RecentRunsTable({ pipeline, from, to }: { pipeline: CrawlPipelineName; from: string; to: string }) {
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(25);
  const [selectedRunId, setSelectedRunId] = useState<string | null>(null);

  const { data, isLoading, isFetching } = useCrawlHistory(pipeline, from, to, page, pageSize);
  const runs: CrawlHistoryRun[] = data ?? [];

  return (
    <Box sx={{ position: 'relative' }}>
      <TableContainer sx={{ maxHeight: 480, opacity: isFetching && !isLoading ? 0.6 : 1, transition: 'opacity 0.15s' }}>
        <Table size="small" stickyHeader>
          <TableHead>
            <TableRow>
              <TableCell>Started</TableCell>
              <TableCell>Provider(s)</TableCell>
              <TableCell>Status</TableCell>
              <TableCell align="right">Feeds/Endpoints</TableCell>
              <TableCell align="right">New</TableCell>
              <TableCell align="right">Failed</TableCell>
              <TableCell align="center">Detail</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {runs.map((run) => (
              <TableRow key={run.id} hover>
                <TableCell>
                  <Tooltip title={formatAbsoluteTime(run.startTime)}>
                    <span>{formatRelativeTime(run.startTime)}</span>
                  </Tooltip>
                </TableCell>
                <TableCell>
                  <Typography variant="body2">{run.providers.join(', ') || '—'}</Typography>
                </TableCell>
                <TableCell>
                  <RunStatusChip status={run.status} />
                </TableCell>
                <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums' }}>
                  {formatFullNumber(run.feedCount)}
                </TableCell>
                <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums' }}>
                  {formatFullNumber(run.newArticles)}
                </TableCell>
                <TableCell
                  align="right"
                  sx={{ fontVariantNumeric: 'tabular-nums', color: run.failedFeeds.length > 0 ? 'error.main' : undefined }}
                >
                  {formatFullNumber(run.failedFeeds.length)}
                </TableCell>
                <TableCell align="center">
                  <IconButton size="small" onClick={() => setSelectedRunId(run.id)} aria-label="View run detail">
                    <VisibilityIcon fontSize="small" />
                  </IconButton>
                </TableCell>
              </TableRow>
            ))}
            {!isLoading && runs.length === 0 && (
              <TableRow>
                <TableCell colSpan={7} align="center">
                  <Typography variant="body2" color="text.secondary" sx={{ py: 3 }}>
                    No runs recorded in this window.
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
        // We don't have a total-count endpoint - the "next page" arrow simply disables once a
        // page comes back short of a full page, since that means there's nothing further.
        slotProps={{ actions: { nextButton: { disabled: runs.length < pageSize } } }}
      />

      <RunDetailDialog runId={selectedRunId} onClose={() => setSelectedRunId(null)} />
    </Box>
  );
}
