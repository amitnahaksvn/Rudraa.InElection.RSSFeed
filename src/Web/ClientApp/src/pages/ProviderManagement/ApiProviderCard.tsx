import { useState } from 'react';
import Accordion from '@mui/material/Accordion';
import AccordionSummary from '@mui/material/AccordionSummary';
import AccordionDetails from '@mui/material/AccordionDetails';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import Divider from '@mui/material/Divider';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import { ProviderStatusChip } from './ProviderStatusChip';
import { ApiEndpointRow } from './ApiEndpointRow';
import type { ApiProviderSummary } from '../../api/providerTypes';

export function ApiProviderCard({ provider }: { provider: ApiProviderSummary }) {
  const [expanded, setExpanded] = useState(false);

  return (
    <Accordion expanded={expanded} onChange={(_, next) => setExpanded(next)} disableGutters>
      <AccordionSummary expandIcon={<ExpandMoreIcon />}>
        <Stack sx={{ width: '100%' }} gap={0.5}>
          <Stack direction="row" alignItems="center" gap={1} flexWrap="wrap">
            <Typography variant="subtitle1" fontWeight={700}>
              {provider.name}
            </Typography>
            <Chip size="small" label={provider.country} />
            <ProviderStatusChip enabled={provider.enabled} />
            {provider.cron && <Chip size="small" variant="outlined" label={provider.cron} />}
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
        </Stack>
      </AccordionSummary>
      <AccordionDetails>
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
