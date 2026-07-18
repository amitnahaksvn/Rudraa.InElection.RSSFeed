import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import { ProviderStatusChip } from './ProviderStatusChip';
import { TestResultPanel } from './TestResultPanel';
import { useTestRssFeed } from './useTestRssFeed';
import type { RssFeedSummary } from '../../api/providerTypes';

export function RssFeedRow({ country, providerName, feed }: { country: string; providerName: string; feed: RssFeedSummary }) {
  const testFeed = useTestRssFeed();

  return (
    <Stack gap={0.75}>
      <Stack direction="row" alignItems="center" gap={1} flexWrap="wrap">
        <Typography variant="body2" fontWeight={600} sx={{ minWidth: 120 }}>
          {feed.name}
        </Typography>
        <ProviderStatusChip enabled={feed.enabled} />
        {feed.category && <Chip size="small" variant="outlined" label={feed.category} />}
        {feed.language && <Chip size="small" variant="outlined" label={feed.language} />}
        <Box sx={{ flexGrow: 1 }} />
        <Button
          size="small"
          variant="outlined"
          startIcon={testFeed.isPending ? <CircularProgress size={14} /> : <PlayArrowIcon />}
          disabled={testFeed.isPending}
          onClick={() => testFeed.mutate({ country, provider: providerName, feedUrl: feed.url })}
        >
          Test
        </Button>
      </Stack>
      <Typography variant="caption" color="text.secondary" sx={{ wordBreak: 'break-all' }}>
        {feed.url}
      </Typography>
      {testFeed.isError && (
        <Typography variant="body2" color="error">
          {(testFeed.error as Error).message}
        </Typography>
      )}
      {testFeed.data && <TestResultPanel result={testFeed.data} onClose={() => testFeed.reset()} />}
    </Stack>
  );
}
