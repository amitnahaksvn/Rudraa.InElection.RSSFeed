import { useState } from 'react';
import Stack from '@mui/material/Stack';
import FormControl from '@mui/material/FormControl';
import InputLabel from '@mui/material/InputLabel';
import Select from '@mui/material/Select';
import MenuItem from '@mui/material/MenuItem';
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup';
import ToggleButton from '@mui/material/ToggleButton';
import CircularProgress from '@mui/material/CircularProgress';
import Alert from '@mui/material/Alert';
import Typography from '@mui/material/Typography';
import Box from '@mui/material/Box';
import type { ArticleSourceType, NewsFeedSortBy } from '../../api/newsTypes';
import { useNewsFeed } from './useNewsFeed';
import { useNewsFeedCount } from './useNewsFeedCount';
import { useNewsCountries } from './useNewsCountries';
import { useInfiniteScrollSentinel } from './useInfiniteScrollSentinel';
import { ArticleCard } from './ArticleCard';
import { getCountryFlagEmoji } from '../../utils/countryFlags';

export function NewsFeedTab({ sourceType }: { sourceType: ArticleSourceType }) {
  const [country, setCountry] = useState<string | null>(null);
  const [sortBy, setSortBy] = useState<NewsFeedSortBy>('PublishedAt');

  const { data: countries } = useNewsCountries(sourceType);
  const { data: totalCount } = useNewsFeedCount(sourceType, country);
  const {
    data,
    isLoading,
    isError,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage,
  } = useNewsFeed(sourceType, country, sortBy);

  const sentinelRef = useInfiniteScrollSentinel(() => fetchNextPage(), Boolean(hasNextPage) && !isFetchingNextPage);

  const articles = data?.pages.flat() ?? [];

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
              onChange={(e) => setCountry(e.target.value || null)}
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
            onChange={(_, value: NewsFeedSortBy | null) => value && setSortBy(value)}
            aria-label="Sort by"
          >
            <ToggleButton value="PublishedAt">Published</ToggleButton>
            <ToggleButton value="CrawledAt">Crawled</ToggleButton>
          </ToggleButtonGroup>
        </Stack>

        {totalCount !== undefined && (
          <Typography variant="caption" color="text.secondary">
            {totalCount} article{totalCount === 1 ? '' : 's'} total
          </Typography>
        )}
      </Stack>

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
          <ArticleCard key={article.id} article={article} />
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
    </Stack>
  );
}
