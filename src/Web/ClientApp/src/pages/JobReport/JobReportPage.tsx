import { useState } from 'react';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import FormControl from '@mui/material/FormControl';
import InputLabel from '@mui/material/InputLabel';
import Select, { type SelectChangeEvent } from '@mui/material/Select';
import MenuItem from '@mui/material/MenuItem';
import Paper from '@mui/material/Paper';
import FactCheckIcon from '@mui/icons-material/FactCheck';
import { JobExecutionLogsTable } from './JobExecutionLogsTable';
import type { JobExecutionStatusName } from '../../api/jobReportTypes';

// The generic (non-crawl) recurring jobs this page reports on - see HangfireJobIds.cs for the
// authoritative id list. Crawl jobs (RSS/API providers) have their own dedicated Crawl Report page.
const KNOWN_JOBS = [
  { id: 'keep-alive-ping', name: 'Keep-alive self-ping' },
  { id: 'cleanup-raw-responses', name: 'Cleanup raw responses' },
  { id: 'dispatch-error-notifications', name: 'Dispatch pending error notifications' },
];

export function JobReportPage() {
  const [jobId, setJobId] = useState<string>('');
  const [status, setStatus] = useState<string>('');

  return (
    <Box sx={{ maxWidth: 1200, mx: 'auto' }}>
      <Stack direction="row" alignItems="center" gap={1.5} sx={{ mb: 0.5 }}>
        <FactCheckIcon color="primary" fontSize="large" />
        <Typography variant="h5" fontWeight={700}>
          Job Report
        </Typography>
      </Stack>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Start time, end time, and outcome for every execution of a generic recurring job - the
        keep-alive self-ping, raw-response cleanup, and error-notification dispatch. RSS/API crawl
        runs have their own Crawl Report page.
      </Typography>

      <Stack direction="row" gap={2} sx={{ mb: 2 }}>
        <FormControl size="small" sx={{ minWidth: 240 }}>
          <InputLabel id="job-report-job-filter-label">Job</InputLabel>
          <Select
            labelId="job-report-job-filter-label"
            label="Job"
            value={jobId}
            onChange={(e: SelectChangeEvent) => setJobId(e.target.value)}
          >
            <MenuItem value="">All jobs</MenuItem>
            {KNOWN_JOBS.map((job) => (
              <MenuItem key={job.id} value={job.id}>
                {job.name}
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        <FormControl size="small" sx={{ minWidth: 180 }}>
          <InputLabel id="job-report-status-filter-label">Status</InputLabel>
          <Select
            labelId="job-report-status-filter-label"
            label="Status"
            value={status}
            onChange={(e: SelectChangeEvent) => setStatus(e.target.value)}
          >
            <MenuItem value="">Any status</MenuItem>
            <MenuItem value="Running">Running</MenuItem>
            <MenuItem value="Succeeded">Succeeded</MenuItem>
            <MenuItem value="Failed">Failed</MenuItem>
          </Select>
        </FormControl>
      </Stack>

      <Paper variant="outlined">
        <JobExecutionLogsTable
          jobId={jobId || null}
          status={(status || null) as JobExecutionStatusName | null}
        />
      </Paper>
    </Box>
  );
}
