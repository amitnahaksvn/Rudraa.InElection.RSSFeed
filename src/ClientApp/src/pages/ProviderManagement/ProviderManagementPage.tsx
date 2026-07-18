import { useState } from 'react';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Tabs from '@mui/material/Tabs';
import Tab from '@mui/material/Tab';
import Typography from '@mui/material/Typography';
import HubIcon from '@mui/icons-material/Hub';
import { RssProvidersTab } from './RssProvidersTab';
import { ApiProvidersTab } from './ApiProvidersTab';

export function ProviderManagementPage() {
  const [tab, setTab] = useState<'rss' | 'api'>('rss');

  return (
    <Box sx={{ maxWidth: 1200, mx: 'auto' }}>
      <Stack direction="row" alignItems="center" gap={1.5} sx={{ mb: 0.5 }}>
        <HubIcon color="primary" fontSize="large" />
        <Typography variant="h5" fontWeight={700}>
          Provider Management
        </Typography>
      </Stack>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Review every configured RSS feed and API endpoint, grouped by country, and test them on demand.
      </Typography>

      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 2, borderBottom: 1, borderColor: 'divider' }}>
        <Tab label="RSS Feeds" value="rss" />
        <Tab label="APIs" value="api" />
      </Tabs>

      {tab === 'rss' ? <RssProvidersTab /> : <ApiProvidersTab />}
    </Box>
  );
}
