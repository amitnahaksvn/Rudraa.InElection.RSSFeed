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
import { ApiEndpointRow } from './ApiEndpointRow';
import { ScheduleEditor } from './ScheduleEditor';
import { ApiEndpointFormDialog } from './ApiEndpointFormDialog';
import { useDeleteProvider } from './useDeleteProvider';
import { getDomainFromUrl } from '../../utils/providerVisuals';
import type { ApiProviderSummary } from '../../api/providerTypes';

export function ApiProviderCard({ provider }: { provider: ApiProviderSummary }) {
  const [expanded, setExpanded] = useState(false);
  const [addEndpointOpen, setAddEndpointOpen] = useState(false);
  const [confirmDeleteOpen, setConfirmDeleteOpen] = useState(false);
  const deleteProvider = useDeleteProvider('Api');
  const domain = useMemo(() => getDomainFromUrl(provider.baseUrl), [provider.baseUrl]);

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
              {provider.authType && <Chip size="small" variant="outlined" label={provider.authType} />}
              <Chip
                size="small"
                variant="outlined"
                label={`${provider.endpoints.length} endpoint${provider.endpoints.length === 1 ? '' : 's'}`}
              />
              <IconButton size="small" aria-label="Delete provider" onClick={(e) => { stop(e); setConfirmDeleteOpen(true); }}>
                <DeleteOutlineIcon fontSize="small" />
              </IconButton>
            </Stack>
            <Typography variant="body2" color="text.secondary">
              {provider.description}
            </Typography>
            {provider.baseUrl && (
              <Typography variant="caption" color="text.secondary" sx={{ wordBreak: 'break-all' }}>
                {provider.baseUrl}
              </Typography>
            )}
            <ScheduleEditor
              key={`${provider.enabled}-${provider.cron}-${provider.timeZone}`}
              pipeline="Api"
              provider={provider.name}
              country={provider.country}
              enabled={provider.enabled}
              cron={provider.cron}
              timeZone={provider.timeZone}
              otherFields={{
                baseUrl: provider.baseUrl,
                authType: provider.authType,
                authParamName: provider.authParamName,
                timeoutSeconds: provider.timeoutSeconds,
              }}
            />
          </Stack>
        </Stack>
      </AccordionSummary>
      <AccordionDetails sx={{ bgcolor: 'action.hover' }}>
        {expanded && (
          <Stack gap={1.5}>
            <Stack divider={<Divider />} gap={1.5}>
              {provider.endpoints.map((endpoint) => (
                <ApiEndpointRow key={endpoint.id} providerName={provider.name} country={provider.country} endpoint={endpoint} />
              ))}
            </Stack>
            <Button size="small" startIcon={<AddIcon fontSize="small" />} onClick={(e) => { stop(e); setAddEndpointOpen(true); }} sx={{ alignSelf: 'flex-start' }}>
              Add endpoint
            </Button>
          </Stack>
        )}
      </AccordionDetails>

      <ApiEndpointFormDialog open={addEndpointOpen} provider={provider.name} country={provider.country} onClose={() => setAddEndpointOpen(false)} />

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
