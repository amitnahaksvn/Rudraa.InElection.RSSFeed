import { useMemo, useState } from 'react';
import Accordion from '@mui/material/Accordion';
import AccordionSummary from '@mui/material/AccordionSummary';
import AccordionDetails from '@mui/material/AccordionDetails';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import Divider from '@mui/material/Divider';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import { ProviderLogo } from './ProviderLogo';
import { ApiEndpointRow } from './ApiEndpointRow';
import { ScheduleEditor } from './ScheduleEditor';
import { getDomainFromUrl } from '../../utils/providerVisuals';
import type { ApiProviderSummary } from '../../api/providerTypes';

export function ApiProviderCard({ provider }: { provider: ApiProviderSummary }) {
  const [expanded, setExpanded] = useState(false);
  const domain = useMemo(() => getDomainFromUrl(provider.baseUrl), [provider.baseUrl]);

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
              enabled={provider.enabled}
              cron={provider.cron}
              timeZone={provider.timeZone}
            />
          </Stack>
        </Stack>
      </AccordionSummary>
      <AccordionDetails sx={{ bgcolor: 'action.hover' }}>
        {expanded && (
          <Stack divider={<Divider />} gap={1.5}>
            {provider.endpoints.map((endpoint) => (
              <ApiEndpointRow key={endpoint.name} country={provider.country} providerName={provider.name} endpoint={endpoint} />
            ))}
          </Stack>
        )}
      </AccordionDetails>
    </Accordion>
  );
}
