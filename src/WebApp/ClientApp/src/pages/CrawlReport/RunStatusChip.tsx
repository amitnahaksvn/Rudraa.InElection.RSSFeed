import Chip from '@mui/material/Chip';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import ErrorIcon from '@mui/icons-material/Error';
import SkipNextIcon from '@mui/icons-material/SkipNext';
import HourglassTopIcon from '@mui/icons-material/HourglassTop';
import HelpOutlineIcon from '@mui/icons-material/HelpOutline';
import { useChartColors } from './useChartColorMode';

const LABELS: Record<string, string> = {
  Completed: 'Completed',
  CompletedWithErrors: 'Completed with errors',
  Failed: 'Failed',
  Skipped: 'Skipped (lock held)',
  Running: 'Running',
};

// Status colors are never color-alone (see the dataviz skill's status-palette rule) - every chip
// pairs its color with both an icon and a text label.
export function RunStatusChip({ status }: { status: string }) {
  const colors = useChartColors();
  const label = LABELS[status] ?? status;

  const styleFor = (color: string) => ({
    color,
    borderColor: color,
    '& .MuiChip-icon': { color },
  });

  switch (status) {
    case 'Completed':
      return <Chip size="small" variant="outlined" icon={<CheckCircleIcon />} label={label} sx={styleFor(colors.good)} />;
    case 'CompletedWithErrors':
      return <Chip size="small" variant="outlined" icon={<WarningAmberIcon />} label={label} sx={styleFor(colors.warning)} />;
    case 'Failed':
      return <Chip size="small" variant="outlined" icon={<ErrorIcon />} label={label} sx={styleFor(colors.critical)} />;
    case 'Skipped':
      return <Chip size="small" variant="outlined" icon={<SkipNextIcon />} label={label} sx={styleFor(colors.inkMuted)} />;
    case 'Running':
      return <Chip size="small" variant="outlined" icon={<HourglassTopIcon />} label={label} color="primary" />;
    default:
      return <Chip size="small" variant="outlined" icon={<HelpOutlineIcon />} label={label} sx={styleFor(colors.inkMuted)} />;
  }
}
