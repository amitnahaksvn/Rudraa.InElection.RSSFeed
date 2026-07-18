import { useState } from 'react';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Tabs from '@mui/material/Tabs';
import Tab from '@mui/material/Tab';
import Typography from '@mui/material/Typography';
import AssessmentIcon from '@mui/icons-material/Assessment';
import { CrawlPipelineReport } from './CrawlPipelineReport';

export function CrawlReportPage() {
  const [tab, setTab] = useState<'Rss' | 'Api'>('Rss');

  return (
    <Box sx={{ maxWidth: 1200, mx: 'auto' }}>
      <Stack direction="row" alignItems="center" gap={1.5} sx={{ mb: 0.5 }}>
        <AssessmentIcon color="primary" fontSize="large" />
        <Typography variant="h5" fontWeight={700}>
          Crawl Report
        </Typography>
      </Stack>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Schedule, success rate, and article volume for every RSS feed and JSON API endpoint - any time, for any date range.
      </Typography>

      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 2, borderBottom: 1, borderColor: 'divider' }}>
        <Tab label="RSS" value="Rss" />
        <Tab label="APIs" value="Api" />
      </Tabs>

      {tab === 'Rss' ? <CrawlPipelineReport pipeline="Rss" /> : <CrawlPipelineReport pipeline="Api" />}
    </Box>
  );
}
