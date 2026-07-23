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
import { useTestApiEndpoint } from './useTestApiEndpoint';
import { useDeleteFeed } from './useCrawlFeedMutations';
import { ApiEndpointFormDialog } from './ApiEndpointFormDialog';
import type { ApiEndpointSummary } from '../../api/providerTypes';

export function ApiEndpointRow({ providerName, country, endpoint }: { providerName: string; country: string; endpoint: ApiEndpointSummary }) {
  const testEndpoint = useTestApiEndpoint();
  const deleteEndpoint = useDeleteFeed('Api');
  const [editOpen, setEditOpen] = useState(false);
  const [confirmDeleteOpen, setConfirmDeleteOpen] = useState(false);

  return (
    <Stack gap={0.75}>
      <Stack direction="row" alignItems="center" gap={1} flexWrap="wrap">
        <Typography variant="body2" fontWeight={600} sx={{ minWidth: 120 }}>
          {endpoint.name}
        </Typography>
        <ProviderStatusChip enabled={endpoint.enabled} />
        {endpoint.category && <Chip size="small" variant="outlined" label={endpoint.category} />}
        {endpoint.language && <Chip size="small" variant="outlined" label={endpoint.language} />}
        <Box sx={{ flexGrow: 1 }} />
        <Button
          size="small"
          variant="outlined"
          startIcon={testEndpoint.isPending ? <CircularProgress size={14} /> : <PlayArrowIcon />}
          disabled={testEndpoint.isPending}
          onClick={() => testEndpoint.mutate(endpoint.id)}
        >
          Test
        </Button>
        <IconButton size="small" aria-label="Edit endpoint" onClick={() => setEditOpen(true)}>
          <EditIcon fontSize="small" />
        </IconButton>
        <IconButton size="small" aria-label="Delete endpoint" onClick={() => setConfirmDeleteOpen(true)}>
          <DeleteOutlineIcon fontSize="small" />
        </IconButton>
      </Stack>
      <Typography variant="caption" color="text.secondary" sx={{ wordBreak: 'break-all' }}>
        {endpoint.url}
      </Typography>
      {testEndpoint.isError && (
        <Typography variant="body2" color="error">
          {(testEndpoint.error as Error).message}
        </Typography>
      )}
      {testEndpoint.data && <TestResultPanel result={testEndpoint.data} onClose={() => testEndpoint.reset()} />}

      <ApiEndpointFormDialog open={editOpen} provider={providerName} country={country} endpoint={endpoint} onClose={() => setEditOpen(false)} />

      <Dialog open={confirmDeleteOpen} onClose={() => setConfirmDeleteOpen(false)}>
        <DialogTitle>Delete endpoint "{endpoint.name}"?</DialogTitle>
        <DialogActions>
          <Button onClick={() => setConfirmDeleteOpen(false)}>Cancel</Button>
          <Button
            color="error"
            variant="contained"
            disabled={deleteEndpoint.isPending}
            onClick={() => deleteEndpoint.mutate(endpoint.id, { onSuccess: () => setConfirmDeleteOpen(false) })}
          >
            Delete
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
}
