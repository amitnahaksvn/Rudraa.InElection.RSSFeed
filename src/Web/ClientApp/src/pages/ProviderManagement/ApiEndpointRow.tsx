import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import { ProviderStatusChip } from './ProviderStatusChip';
import { TestResultPanel } from './TestResultPanel';
import { useTestApiEndpoint } from './useTestApiEndpoint';
import type { ApiEndpointSummary } from '../../api/providerTypes';

export function ApiEndpointRow({
  country,
  providerName,
  endpoint,
}: {
  country: string;
  providerName: string;
  endpoint: ApiEndpointSummary;
}) {
  const testEndpoint = useTestApiEndpoint();

  return (
    <Stack gap={0.75}>
      <Stack direction="row" alignItems="center" gap={1} flexWrap="wrap">
        <Typography variant="body2" fontWeight={600} sx={{ minWidth: 120 }}>
          {endpoint.name}
        </Typography>
        <ProviderStatusChip enabled={endpoint.enabled} />
        {endpoint.category && <Chip size="small" variant="outlined" label={endpoint.category} />}
        {endpoint.language && <Chip size="small" variant="outlined" label={endpoint.language} />}
        <Box sx={{ flexGrow: 1 }} />
        <Button
          size="small"
          variant="outlined"
          startIcon={testEndpoint.isPending ? <CircularProgress size={14} /> : <PlayArrowIcon />}
          disabled={testEndpoint.isPending}
          onClick={() => testEndpoint.mutate({ country, provider: providerName, endpointName: endpoint.name })}
        >
          Test
        </Button>
      </Stack>
      <Typography variant="caption" color="text.secondary" sx={{ wordBreak: 'break-all' }}>
        {endpoint.endpoint}
      </Typography>
      {testEndpoint.isError && (
        <Typography variant="body2" color="error">
          {(testEndpoint.error as Error).message}
        </Typography>
      )}
      {testEndpoint.data && <TestResultPanel result={testEndpoint.data} />}
    </Stack>
  );
}
