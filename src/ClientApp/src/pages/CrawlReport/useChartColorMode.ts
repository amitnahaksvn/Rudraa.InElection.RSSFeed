import { useColorScheme } from '@mui/material/styles';
import { chartPalette, type ChartColorMode } from './palette';

/** Resolves MUI's "system"/"light"/"dark" color-scheme mode down to the two the chart palette actually has steps for. */
export function useChartColorMode(): ChartColorMode {
  const { mode, systemMode } = useColorScheme();
  const resolved = mode === 'system' ? systemMode : mode;
  return resolved === 'dark' ? 'dark' : 'light';
}

export function useChartColors() {
  const mode = useChartColorMode();
  return chartPalette[mode];
}
