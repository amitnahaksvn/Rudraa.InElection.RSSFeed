import { useState } from 'react';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import CircularProgress from '@mui/material/CircularProgress';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogActions from '@mui/material/DialogActions';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import EditIcon from '@mui/icons-material/Edit';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import { ProviderStatusChip } from './ProviderStatusChip';
import { TestResultPanel } from './TestResultPanel';
import { useTestRssFeed } from './useTestRssFeed';
import { useDeleteFeed } from './useCrawlFeedMutations';
import { RssFeedFormDialog } from './RssFeedFormDialog';
import type { RssFeedSummary } from '../../api/providerTypes';

export function RssFeedRow({ providerName, country, feed }: { providerName: string; country: string; feed: RssFeedSummary }) {
  const testFeed = useTestRssFeed();
  const deleteFeed = useDeleteFeed('Rss');
  const [editOpen, setEditOpen] = useState(false);
  const [confirmDeleteOpen, setConfirmDeleteOpen] = useState(false);

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
          onClick={() => testFeed.mutate(feed.id)}
        >
          Test
        </Button>
        <IconButton size="small" aria-label="Edit feed" onClick={() => setEditOpen(true)}>
          <EditIcon fontSize="small" />
        </IconButton>
        <IconButton size="small" aria-label="Delete feed" onClick={() => setConfirmDeleteOpen(true)}>
          <DeleteOutlineIcon fontSize="small" />
        </IconButton>
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

      <RssFeedFormDialog open={editOpen} provider={providerName} country={country} feed={feed} onClose={() => setEditOpen(false)} />

      <Dialog open={confirmDeleteOpen} onClose={() => setConfirmDeleteOpen(false)}>
        <DialogTitle>Delete feed "{feed.name}"?</DialogTitle>
        <DialogActions>
          <Button onClick={() => setConfirmDeleteOpen(false)}>Cancel</Button>
          <Button
            color="error"
            variant="contained"
            disabled={deleteFeed.isPending}
            onClick={() => deleteFeed.mutate(feed.id, { onSuccess: () => setConfirmDeleteOpen(false) })}
          >
            Delete
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
}
