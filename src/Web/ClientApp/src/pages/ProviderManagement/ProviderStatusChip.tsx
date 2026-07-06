import Chip from '@mui/material/Chip';

export function ProviderStatusChip({ enabled }: { enabled: boolean }) {
  return enabled ? (
    <Chip size="small" color="success" variant="outlined" label="Enabled" />
  ) : (
    <Chip size="small" color="default" variant="outlined" label="Disabled" />
  );
}
