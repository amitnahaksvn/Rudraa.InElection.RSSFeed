import { useEffect, useRef, useState } from 'react';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import CircularProgress from '@mui/material/CircularProgress';
import Alert from '@mui/material/Alert';
import Chip from '@mui/material/Chip';
import { FilterBar } from './FilterBar';
import { ErrorRow } from './ErrorRow';
import { useErrorLogs } from './useErrorLogs';
import { PendingTransitionProvider } from './PendingTransitionContext';
import { PendingTransitionToasts } from './PendingTransitionToasts';
import type { ErrorLogFilters } from '../../api/types';

const DEFAULT_FILTERS: ErrorLogFilters = {
  status: 'unresolved',
  provider: '',
  country: '',
  source: '',
  search: '',
};

export function ErrorMonitorPage() {
  const [filters, setFilters] = useState<ErrorLogFilters>(DEFAULT_FILTERS);
  const { data, fetchNextPage, hasNextPage, isFetchingNextPage, isLoading, isError, refetch } = useErrorLogs(filters);
  const sentinelRef = useRef<HTMLDivElement | null>(null);

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

  return (
    <PendingTransitionProvider>
      <Box sx={{ maxWidth: 1100, mx: 'auto' }}>
        <Stack direction="row" alignItems="baseline" justifyContent="space-between" sx={{ mb: 2 }} flexWrap="wrap" gap={1}>
          <Typography variant="h5" fontWeight={700}>
            Errors
          </Typography>
          {totalCount !== undefined && <Chip size="small" label={`${totalCount} matching`} />}
        </Stack>

        <FilterBar filters={filters} onChange={setFilters} />

        {isLoading && (
          <Stack alignItems="center" sx={{ py: 6 }}>
            <CircularProgress />
          </Stack>
        )}

        {isError && (
          <Alert severity="error" action={<a onClick={() => refetch()} style={{ cursor: 'pointer' }}>Retry</a>}>
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

        <Stack gap={1.25}>
          {errors.map((error) => (
            <ErrorRow key={error.id} error={error} />
          ))}
        </Stack>

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

        <PendingTransitionToasts />
      </Box>
    </PendingTransitionProvider>
  );
}
