import { useEffect, useRef, useState } from 'react';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import CircularProgress from '@mui/material/CircularProgress';
import Alert from '@mui/material/Alert';
import Divider from '@mui/material/Divider';
import { useTheme } from '@mui/material/styles';
import useMediaQuery from '@mui/material/useMediaQuery';
import { FilterBar } from './FilterBar';
import { ErrorListRow } from './ErrorListRow';
import { ErrorDetailPane } from './ErrorDetailPane';
import { StatusSidebar } from './StatusSidebar';
import { useErrorLogs } from './useErrorLogs';
import { useErrorLogCounts } from './useErrorLogCounts';
import { useErrorLogRealtime } from './useErrorLogRealtime';
import { PendingTransitionProvider } from './PendingTransitionContext';
import { PendingTransitionToasts } from './PendingTransitionToasts';
import type { ErrorLogFilters } from '../../api/types';

const DEFAULT_FILTERS: ErrorLogFilters = {
  status: 'unresolved',
  category: null,
  provider: '',
  country: '',
  source: '',
  search: '',
};

const SIDEBAR_WIDTH = 220;
const LIST_WIDTH = 380;

// useErrorLogRealtime needs PendingTransitionContext (to animate a remotely-changed row out of a
// now-mismatched status filter the same way a local resolve does), so it has to run inside
// PendingTransitionProvider rather than in ErrorMonitorPage itself, which is what renders that
// provider - this thin wrapper is that inner boundary.
export function ErrorMonitorPage() {
  return (
    <PendingTransitionProvider>
      <ErrorMonitorPageContent />
    </PendingTransitionProvider>
  );
}

function ErrorMonitorPageContent() {
  useErrorLogRealtime();
  const [filters, setFilters] = useState<ErrorLogFilters>(DEFAULT_FILTERS);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const { data, fetchNextPage, hasNextPage, isFetchingNextPage, isLoading, isError, refetch } = useErrorLogs(filters);
  const counts = useErrorLogCounts(filters);
  const sentinelRef = useRef<HTMLDivElement | null>(null);
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));

  // Infinite scroll: load the next 20 once the sentinel div at the bottom of the list scrolls
  // into view, instead of a "load more" button - matches "as scroll down it shows more record".
  useEffect(() => {
    const sentinel = sentinelRef.current;
    if (!sentinel) return;

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0].isIntersecting && hasNextPage && !isFetchingNextPage) {
          fetchNextPage();
        }
      },
      { rootMargin: '200px' },
    );

    observer.observe(sentinel);
    return () => observer.disconnect();
  }, [hasNextPage, isFetchingNextPage, fetchNextPage]);

  const errors = data?.pages.flatMap((page) => page.items) ?? [];
  const totalCount = data?.pages[0]?.totalCount;

  // Switching status/category is "going to a different folder" - whatever was selected in the
  // old one no longer applies, so the detail pane resets to its empty state rather than keep
  // showing a row that may not even be in the new view.
  const handleSidebarChange = (next: ErrorLogFilters) => {
    setFilters(next);
    setSelectedId(null);
  };

  const listPane = (
    <Stack sx={{ height: '100%' }}>
      <Stack direction="row" alignItems="baseline" justifyContent="space-between" sx={{ px: 1.5, pt: 1.5 }} gap={1}>
        <Typography variant="h6" fontWeight={700}>
          Errors
        </Typography>
        {totalCount !== undefined && (
          <Typography variant="caption" color="text.secondary">
            {totalCount} matching
          </Typography>
        )}
      </Stack>

      <FilterBar filters={filters} onChange={setFilters} />
      <Divider />

      <Box sx={{ flex: 1, overflow: 'auto' }}>
        {isLoading && (
          <Stack alignItems="center" sx={{ py: 6 }}>
            <CircularProgress />
          </Stack>
        )}

        {isError && (
          <Alert severity="error" sx={{ m: 1.5 }} action={<a onClick={() => refetch()} style={{ cursor: 'pointer' }}>Retry</a>}>
            Failed to load errors.
          </Alert>
        )}

        {!isLoading && !isError && errors.length === 0 && (
          <Stack alignItems="center" sx={{ py: 8 }} gap={1}>
            <Typography variant="h6">Nothing here</Typography>
            <Typography variant="body2" color="text.secondary">
              No errors match the current filters.
            </Typography>
          </Stack>
        )}

        {errors.map((error) => (
          <ErrorListRow key={error.id} error={error} selected={error.id === selectedId} onSelect={() => setSelectedId(error.id)} />
        ))}

        <Box ref={sentinelRef} sx={{ height: 1 }} />

        {isFetchingNextPage && (
          <Stack alignItems="center" sx={{ py: 3 }}>
            <CircularProgress size={24} />
          </Stack>
        )}

        {!hasNextPage && errors.length > 0 && (
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', textAlign: 'center', py: 2 }}>
            You've reached the end.
          </Typography>
        )}
      </Box>
    </Stack>
  );

  return (
    <Box sx={{ height: 'calc(100vh - 96px)', display: 'flex', flexDirection: 'column' }}>
      {isMobile && <StatusSidebar direction="row" filters={filters} counts={counts.data} onChange={handleSidebarChange} />}

      <Box sx={{ flex: 1, display: 'flex', minHeight: 0, border: 1, borderColor: 'divider', borderRadius: 1, overflow: 'hidden' }}>
        {!isMobile && (
          <>
            <Box sx={{ width: SIDEBAR_WIDTH, flexShrink: 0, overflow: 'auto' }}>
              <StatusSidebar filters={filters} counts={counts.data} onChange={handleSidebarChange} />
            </Box>
            <Divider orientation="vertical" flexItem />
          </>
        )}

        {(!isMobile || selectedId === null) && (
          <Box sx={{ width: isMobile ? '100%' : LIST_WIDTH, flexShrink: 0, overflow: 'hidden' }}>{listPane}</Box>
        )}

        {!isMobile && <Divider orientation="vertical" flexItem />}

        {!isMobile && (
          <Box sx={{ flex: 1, minWidth: 0, overflow: 'hidden' }}>
            {selectedId ? (
              <ErrorDetailPane errorId={selectedId} />
            ) : (
              <Stack alignItems="center" justifyContent="center" sx={{ height: '100%', color: 'text.secondary' }} gap={1}>
                <Typography variant="body2">Select an error to view its details</Typography>
              </Stack>
            )}
          </Box>
        )}

        {isMobile && selectedId !== null && (
          <Box sx={{ width: '100%', overflow: 'hidden' }}>
            <ErrorDetailPane errorId={selectedId} onBack={() => setSelectedId(null)} />
          </Box>
        )}
      </Box>

      <PendingTransitionToasts />
    </Box>
  );
}
