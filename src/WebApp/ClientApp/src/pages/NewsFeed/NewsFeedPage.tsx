import { useState } from 'react';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Tabs from '@mui/material/Tabs';
import Tab from '@mui/material/Tab';
import Typography from '@mui/material/Typography';
import DynamicFeedIcon from '@mui/icons-material/DynamicFeed';
import { NewsFeedTab } from './NewsFeedTab';

export function NewsFeedPage() {
  const [tab, setTab] = useState<'Rss' | 'Api'>('Rss');

  return (
    <Box sx={{ maxWidth: 900, mx: 'auto' }}>
      <Stack direction="row" alignItems="center" gap={1.5} sx={{ mb: 0.5 }}>
        <DynamicFeedIcon color="primary" fontSize="large" />
        <Typography variant="h5" fontWeight={700}>
          News Feed
        </Typography>
      </Stack>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Latest articles as they're crawled, newest first - filter by country, scroll for more.
      </Typography>

      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 2, borderBottom: 1, borderColor: 'divider' }}>
        <Tab label="RSS" value="Rss" />
        <Tab label="APIs" value="Api" />
      </Tabs>

      {/* key={tab} forces a remount on tab switch, so the country filter (local state inside
          NewsFeedTab) resets instead of carrying over a country that may not even apply to the
          other pipeline. */}
      <NewsFeedTab key={tab} sourceType={tab} />
    </Box>
  );
}
