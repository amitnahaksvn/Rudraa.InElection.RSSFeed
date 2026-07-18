import { useState } from 'react';
import Stack from '@mui/material/Stack';
import FormControl from '@mui/material/FormControl';
import InputLabel from '@mui/material/InputLabel';
import Select from '@mui/material/Select';
import MenuItem from '@mui/material/MenuItem';
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup';
import ToggleButton from '@mui/material/ToggleButton';
import Tooltip from '@mui/material/Tooltip';
import IconButton from '@mui/material/IconButton';
import Checkbox from '@mui/material/Checkbox';
import Button from '@mui/material/Button';
import ArrowDownwardIcon from '@mui/icons-material/ArrowDownward';
import ArrowUpwardIcon from '@mui/icons-material/ArrowUpward';
import RefreshIcon from '@mui/icons-material/Refresh';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import CircularProgress from '@mui/material/CircularProgress';
import Alert from '@mui/material/Alert';
import Typography from '@mui/material/Typography';
import Box from '@mui/material/Box';
import type { ArticleSourceType, NewsFeedSortBy, NewsFeedSortDirection } from '../../api/newsTypes';
import { useNewsFeed } from './useNewsFeed';
import { useNewsFeedCount } from './useNewsFeedCount';
import { useNewsCountries } from './useNewsCountries';
import { useInfiniteScrollSentinel } from './useInfiniteScrollSentinel';
import { useDeleteArticles } from './useDeleteArticles';
import { ArticleCard } from './ArticleCard';
import { DeleteArticlesDialog } from './DeleteArticlesDialog';
import { getCountryFlagEmoji } from '../../utils/countryFlags';

export function NewsFeedTab({ sourceType }: { sourceType: ArticleSourceType }) {
  const [country, setCountry] = useState<string | null>(null);
  const [sortBy, setSortBy] = useState<NewsFeedSortBy>('PublishedAt');
  const [sortDirection, setSortDirection] = useState<NewsFeedSortDirection>('Descending');
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [pendingDeleteIds, setPendingDeleteIds] = useState<string[] | null>(null);

  const { data: countries } = useNewsCountries(sourceType);
  const { data: totalCount, refetch: refetchCount } = useNewsFeedCount(sourceType, country);
  const {
    data,
    isLoading,
    isError,
    isRefetching,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage,
    refetch,
  } = useNewsFeed(sourceType, country, sortBy, sortDirection);

  const deleteMutation = useDeleteArticles();

  const sentinelRef = useInfiniteScrollSentinel(() => fetchNextPage(), Boolean(hasNextPage) && !isFetchingNextPage);

  const articles = data?.pages.flat() ?? [];

  // Tracks whatever is currently loaded (infinite scroll only fetches a page at a time) - if more
  // articles load in afterwards, they start unselected and the checkbox naturally drops to its
  // indeterminate state rather than silently claiming everything is selected.
  const allLoadedSelected = articles.length > 0 && articles.every((a) => selectedIds.has(a.id));
  const someLoadedSelected = articles.some((a) => selectedIds.has(a.id));

  const toggleSelect = (id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  const toggleSelectAll = () => {
    setSelectedIds(allLoadedSelected ? new Set() : new Set(articles.map((a) => a.id)));
  };

  const handleConfirmDelete = () => {
    if (!pendingDeleteIds) return;
    deleteMutation.mutate(pendingDeleteIds, {
      onSuccess: () => {
        setSelectedIds((prev) => {
          const next = new Set(prev);
          pendingDeleteIds.forEach((id) => next.delete(id));
          return next;
        });
        setPendingDeleteIds(null);
      },
    });
  };

  return (
    <Stack gap={1.5}>
      <Stack direction="row" alignItems="center" justifyContent="space-between" gap={1.5} flexWrap="wrap">
        <Stack direction="row" alignItems="center" gap={1.5} flexWrap="wrap">
          <FormControl size="small" sx={{ minWidth: 220 }}>
            <InputLabel id={`country-filter-${sourceType}`}>Country</InputLabel>
            <Select
              labelId={`country-filter-${sourceType}`}
              label="Country"
              value={country ?? ''}
              onChange={(e) => {
                setCountry(e.target.value || null);
                setSelectedIds(new Set());
              }}
            >
              <MenuItem value="">All countries</MenuItem>
              {countries?.map((c) => (
                <MenuItem key={c} value={c}>
                  {getCountryFlagEmoji(c) ? `${getCountryFlagEmoji(c)} ${c}` : c}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          {/* onChange fires with `null` when clicking the already-selected button - ignored so
              exactly one of Published/Crawled is always active, matching a single sort order. */}
          <ToggleButtonGroup
            size="small"
            exclusive
            value={sortBy}
            onChange={(_, value: NewsFeedSortBy | null) => {
              if (!value) return;
              setSortBy(value);
              setSelectedIds(new Set());
            }}
            aria-label="Sort by"
          >
            <ToggleButton value="PublishedAt">Published</ToggleButton>
            <ToggleButton value="CrawledAt">Crawled</ToggleButton>
          </ToggleButtonGroup>

          <Tooltip title={sortDirection === 'Descending' ? 'Newest first - click for oldest first' : 'Oldest first - click for newest first'}>
            <IconButton
              size="small"
              aria-label="Toggle sort direction"
              onClick={() => {
                setSortDirection((prev) => (prev === 'Descending' ? 'Ascending' : 'Descending'));
                setSelectedIds(new Set());
              }}
            >
              {sortDirection === 'Descending' ? <ArrowDownwardIcon fontSize="small" /> : <ArrowUpwardIcon fontSize="small" />}
            </IconButton>
          </Tooltip>

          <Tooltip title="Refresh">
            <span>
              <IconButton
                size="small"
                aria-label="Refresh"
                disabled={isRefetching}
                onClick={() => {
                  refetch();
                  refetchCount();
                }}
              >
                {isRefetching ? <CircularProgress size={18} /> : <RefreshIcon fontSize="small" />}
              </IconButton>
            </span>
          </Tooltip>

          <Tooltip title={allLoadedSelected ? 'Deselect all' : 'Select all loaded articles'}>
            <span>
              <Checkbox
                size="small"
                checked={allLoadedSelected}
                indeterminate={someLoadedSelected && !allLoadedSelected}
                onChange={toggleSelectAll}
                disabled={articles.length === 0}
                aria-label={allLoadedSelected ? 'Deselect all' : 'Select all loaded articles'}
              />
            </span>
          </Tooltip>
        </Stack>

        {totalCount !== undefined && (
          <Typography variant="caption" color="text.secondary">
            {totalCount} article{totalCount === 1 ? '' : 's'} total
          </Typography>
        )}
      </Stack>

      {selectedIds.size > 0 && (
        <Stack
          direction="row"
          alignItems="center"
          gap={1.5}
          sx={{ px: 1.5, py: 1, borderRadius: 1, bgcolor: 'action.selected' }}
        >
          <Typography variant="body2" fontWeight={600}>
            {selectedIds.size} selected
          </Typography>
          <Button size="small" onClick={() => setSelectedIds(new Set())}>
            Clear
          </Button>
          <Button
            size="small"
            color="error"
            variant="outlined"
            startIcon={<DeleteOutlineIcon />}
            sx={{ ml: 'auto' }}
            onClick={() => setPendingDeleteIds([...selectedIds])}
          >
            Delete selected
          </Button>
        </Stack>
      )}

      {isLoading && (
        <Stack alignItems="center" sx={{ py: 6 }}>
          <CircularProgress />
        </Stack>
      )}

      {isError && <Alert severity="error">Failed to load the news feed.</Alert>}

      {!isLoading && !isError && articles.length === 0 && (
        <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center', py: 6 }}>
          No articles found for this filter yet.
        </Typography>
      )}

      <Stack gap={1.25}>
        {articles.map((article) => (
          <ArticleCard
            key={article.id}
            article={article}
            selected={selectedIds.has(article.id)}
            onToggleSelect={toggleSelect}
            onDelete={(id) => setPendingDeleteIds([id])}
          />
        ))}
      </Stack>

      <Box ref={sentinelRef} sx={{ height: 1 }} />

      {isFetchingNextPage && (
        <Stack alignItems="center" sx={{ py: 2 }}>
          <CircularProgress size={24} />
        </Stack>
      )}

      {!hasNextPage && articles.length > 0 && (
        <Typography variant="caption" color="text.secondary" sx={{ textAlign: 'center', py: 2 }}>
          You've reached the end.
        </Typography>
      )}

      <DeleteArticlesDialog
        open={pendingDeleteIds !== null}
        count={pendingDeleteIds?.length ?? 0}
        submitting={deleteMutation.isPending}
        errorMessage={deleteMutation.isError ? (deleteMutation.error as Error).message : null}
        onCancel={() => setPendingDeleteIds(null)}
        onConfirm={handleConfirmDelete}
      />
    </Stack>
  );
}
