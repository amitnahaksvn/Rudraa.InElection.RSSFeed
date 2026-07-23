import { useMemo, useState, type SyntheticEvent } from 'react';
import Accordion from '@mui/material/Accordion';
import AccordionSummary from '@mui/material/AccordionSummary';
import AccordionDetails from '@mui/material/AccordionDetails';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import Divider from '@mui/material/Divider';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogActions from '@mui/material/DialogActions';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import AddIcon from '@mui/icons-material/Add';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import { ProviderLogo } from './ProviderLogo';
import { RssFeedRow } from './RssFeedRow';
import { ScheduleEditor } from './ScheduleEditor';
import { RssFeedFormDialog } from './RssFeedFormDialog';
import { useDeleteProvider } from './useDeleteProvider';
import { getDomainFromUrl } from '../../utils/providerVisuals';
import type { RssProviderSummary } from '../../api/providerTypes';

export function RssProviderCard({ provider }: { provider: RssProviderSummary }) {
  const [expanded, setExpanded] = useState(false);
  const [addFeedOpen, setAddFeedOpen] = useState(false);
  const [confirmDeleteOpen, setConfirmDeleteOpen] = useState(false);
  const deleteProvider = useDeleteProvider('Rss');

  const domain = useMemo(() => {
    const representative = provider.feeds.find((f) => f.enabled) ?? provider.feeds[0];
    return getDomainFromUrl(representative?.url);
  }, [provider.feeds]);

  const stop = (e: SyntheticEvent) => e.stopPropagation();

  return (
    <Accordion
      expanded={expanded}
      onChange={(_, next) => setExpanded(next)}
      disableGutters
      sx={{
        borderRadius: 2,
        overflow: 'hidden',
        borderLeft: 4,
        borderColor: provider.enabled ? 'success.main' : 'divider',
        boxShadow: 1,
        transition: 'box-shadow 0.15s ease',
        '&:hover': { boxShadow: 3 },
        '&::before': { display: 'none' },
      }}
    >
      <AccordionSummary expandIcon={<ExpandMoreIcon />} sx={{ py: 1 }}>
        <Stack direction="row" alignItems="flex-start" gap={1.5} sx={{ width: '100%' }}>
          <ProviderLogo name={provider.name} domain={domain} />
          <Stack sx={{ width: '100%', minWidth: 0 }} gap={0.5}>
            <Stack direction="row" alignItems="center" gap={1} flexWrap="wrap">
              <Typography variant="subtitle1" fontWeight={700}>
                {provider.name}
              </Typography>
              <Chip
                size="small"
                variant="outlined"
                label={`${provider.feeds.length} feed${provider.feeds.length === 1 ? '' : 's'}`}
              />
              <IconButton size="small" aria-label="Delete provider" onClick={(e) => { stop(e); setConfirmDeleteOpen(true); }}>
                <DeleteOutlineIcon fontSize="small" />
              </IconButton>
            </Stack>
            <Typography variant="body2" color="text.secondary">
              {provider.description}
            </Typography>
            <ScheduleEditor
              key={`${provider.enabled}-${provider.cron}-${provider.timeZone}`}
              pipeline="Rss"
              provider={provider.name}
              country={provider.country}
              enabled={provider.enabled}
              cron={provider.cron}
              timeZone={provider.timeZone}
              otherFields={{ saveRawResponses: provider.saveRawResponses }}
            />
          </Stack>
        </Stack>
      </AccordionSummary>
      <AccordionDetails sx={{ bgcolor: 'action.hover' }}>
        {expanded && (
          <Stack gap={1.5}>
            <Stack divider={<Divider />} gap={1.5}>
              {provider.feeds.map((feed) => (
                <RssFeedRow key={feed.id} providerName={provider.name} country={provider.country} feed={feed} />
              ))}
            </Stack>
            <Button size="small" startIcon={<AddIcon fontSize="small" />} onClick={(e) => { stop(e); setAddFeedOpen(true); }} sx={{ alignSelf: 'flex-start' }}>
              Add feed
            </Button>
          </Stack>
        )}
      </AccordionDetails>

      <RssFeedFormDialog open={addFeedOpen} provider={provider.name} country={provider.country} onClose={() => setAddFeedOpen(false)} />

      <Dialog open={confirmDeleteOpen} onClose={() => setConfirmDeleteOpen(false)}>
        <DialogTitle>Delete provider "{provider.name}" ({provider.country}) and its recurring job?</DialogTitle>
        <DialogActions>
          <Button onClick={() => setConfirmDeleteOpen(false)}>Cancel</Button>
          <Button
            color="error"
            variant="contained"
            disabled={deleteProvider.isPending}
            onClick={() => deleteProvider.mutate({ provider: provider.name, country: provider.country }, { onSuccess: () => setConfirmDeleteOpen(false) })}
          >
            Delete
          </Button>
        </DialogActions>
      </Dialog>
    </Accordion>
  );
}
