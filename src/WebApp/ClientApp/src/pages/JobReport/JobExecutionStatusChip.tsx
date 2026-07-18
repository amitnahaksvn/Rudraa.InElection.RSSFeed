import Chip from '@mui/material/Chip';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import ErrorIcon from '@mui/icons-material/Error';
import HourglassTopIcon from '@mui/icons-material/HourglassTop';
import HelpOutlineIcon from '@mui/icons-material/HelpOutline';
import { useChartColors } from '../CrawlReport/useChartColorMode';
import type { JobExecutionStatusName } from '../../api/jobReportTypes';

// Status colors are never color-alone (see the dataviz skill's status-palette rule) - every chip
// pairs its color with both an icon and a text label, same convention as CrawlReport's RunStatusChip.
export function JobExecutionStatusChip({ status }: { status: JobExecutionStatusName | string }) {
  const colors = useChartColors();

  const styleFor = (color: string) => ({
    color,
    borderColor: color,
    '& .MuiChip-icon': { color },
  });

  switch (status) {
    case 'Succeeded':
      return <Chip size="small" variant="outlined" icon={<CheckCircleIcon />} label="Succeeded" sx={styleFor(colors.good)} />;
    case 'Failed':
      return <Chip size="small" variant="outlined" icon={<ErrorIcon />} label="Failed" sx={styleFor(colors.critical)} />;
    case 'Running':
      return <Chip size="small" variant="outlined" icon={<HourglassTopIcon />} label="Running" color="primary" />;
    default:
      return <Chip size="small" variant="outlined" icon={<HelpOutlineIcon />} label={status} sx={styleFor(colors.inkMuted)} />;
  }
}
