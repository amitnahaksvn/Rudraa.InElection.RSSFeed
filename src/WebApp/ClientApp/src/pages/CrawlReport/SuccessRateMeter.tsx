import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { useChartColors } from './useChartColorMode';

/** "A single ratio against a limit" (choosing-a-form.md) - a same-ramp meter, not a pie of 2 slices. */
export function SuccessRateMeter({ percent }: { percent: number }) {
  const colors = useChartColors();
  const clamped = Math.max(0, Math.min(100, percent));

  return (
    <Stack direction="row" alignItems="center" gap={1} sx={{ minWidth: 120 }}>
      <Box sx={{ position: 'relative', flex: 1, height: 6, borderRadius: 3, bgcolor: colors.meterTrack, overflow: 'hidden' }}>
        <Box sx={{ position: 'absolute', inset: 0, width: `${clamped}%`, bgcolor: colors.meterFill, borderRadius: 3 }} />
      </Box>
      <Typography variant="caption" sx={{ fontVariantNumeric: 'tabular-nums', minWidth: 38, textAlign: 'right' }}>
        {clamped.toFixed(1)}%
      </Typography>
    </Stack>
  );
}
