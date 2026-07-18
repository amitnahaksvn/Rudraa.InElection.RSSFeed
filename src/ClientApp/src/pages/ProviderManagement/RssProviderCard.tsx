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
import { RssFeedRow } from './RssFeedRow';
import { ScheduleEditor } from './ScheduleEditor';
import { getDomainFromUrl } from '../../utils/providerVisuals';
import type { RssProviderSummary } from '../../api/providerTypes';

export function RssProviderCard({ provider }: { provider: RssProviderSummary }) {
  const [expanded, setExpanded] = useState(false);

  const domain = useMemo(() => {
    const representative = provider.feeds.find((f) => f.enabled) ?? provider.feeds[0];
    return getDomainFromUrl(representative?.url);
  }, [provider.feeds]);

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
            </Stack>
            <Typography variant="body2" color="text.secondary">
              {provider.description}
            </Typography>
            <ScheduleEditor
              key={`${provider.enabled}-${provider.cron}-${provider.timeZone}`}
              pipeline="Rss"
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
            {provider.feeds.map((feed) => (
              <RssFeedRow key={feed.url} country={provider.country} providerName={provider.name} feed={feed} />
            ))}
          </Stack>
        )}
      </AccordionDetails>
    </Accordion>
  );
}
