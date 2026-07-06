import { useState } from 'react';
import Box from '@mui/material/Box';
import Tabs from '@mui/material/Tabs';
import Tab from '@mui/material/Tab';
import Typography from '@mui/material/Typography';
import { RssProvidersTab } from './RssProvidersTab';
import { ApiProvidersTab } from './ApiProvidersTab';

export function ProviderManagementPage() {
  const [tab, setTab] = useState<'rss' | 'api'>('rss');

  return (
    <Box sx={{ maxWidth: 1100, mx: 'auto' }}>
      <Typography variant="h5" fontWeight={700} sx={{ mb: 2 }}>
        Provider Management
      </Typography>

      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 2, borderBottom: 1, borderColor: 'divider' }}>
        <Tab label="RSS Feeds" value="rss" />
        <Tab label="APIs" value="api" />
      </Tabs>

      {tab === 'rss' ? <RssProvidersTab /> : <ApiProvidersTab />}
    </Box>
  );
}
