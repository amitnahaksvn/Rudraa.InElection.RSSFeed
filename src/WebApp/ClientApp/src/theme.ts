import { createTheme } from '@mui/material/styles';

// A dark-first admin/ops theme - conventional for monitoring dashboards viewed for long
// stretches - with status colors tuned specifically for the resolved/unresolved distinction the
// error monitor leans on everywhere (row accent, chips).
export const theme = createTheme({
  colorSchemes: {
    dark: {
      palette: {
        background: { default: '#12141c', paper: '#1a1d29' },
        primary: { main: '#7c9eff' },
        error: { main: '#ff6b6b' },
        success: { main: '#4cd97b' },
        warning: { main: '#ffb648' },
      },
    },
    light: {
      palette: {
        background: { default: '#f4f5f9', paper: '#ffffff' },
        primary: { main: '#3457d5' },
        error: { main: '#d32f2f' },
        success: { main: '#2e9e52' },
        warning: { main: '#b46a00' },
      },
    },
  },
  shape: { borderRadius: 10 },
  typography: {
    fontFamily: [
      'Inter',
      '-apple-system',
      'BlinkMacSystemFont',
      'Segoe UI',
      'Roboto',
      'Helvetica Neue',
      'Arial',
      'sans-serif',
    ].join(','),
  },
  components: {
    MuiCard: {
      styleOverrides: {
        root: { backgroundImage: 'none' },
      },
    },
  },
});
