import { useMemo, useState } from 'react';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import CircularProgress from '@mui/material/CircularProgress';
import Alert from '@mui/material/Alert';
import Typography from '@mui/material/Typography';
import InputAdornment from '@mui/material/InputAdornment';
import SearchIcon from '@mui/icons-material/Search';
import { useApiProviders } from './useApiProviders';
import { ApiProviderCard } from './ApiProviderCard';

export function ApiProvidersTab() {
  const { data, isLoading, isError } = useApiProviders();
  const [search, setSearch] = useState('');

  const filtered = useMemo(() => {
    if (!data) return [];
    const term = search.trim().toLowerCase();
    if (!term) return data;
    return data.filter((p) => p.name.toLowerCase().includes(term) || p.country.toLowerCase().includes(term));
  }, [data, search]);

  if (isLoading) {
    return (
      <Stack alignItems="center" sx={{ py: 6 }}>
        <CircularProgress />
      </Stack>
    );
  }

  if (isError) {
    return <Alert severity="error">Failed to load API providers.</Alert>;
  }

  return (
    <Stack gap={2}>
      <Stack direction="row" alignItems="center" justifyContent="space-between" flexWrap="wrap" gap={1}>
        <TextField
          size="small"
          placeholder="Search provider or country..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          sx={{ minWidth: 280 }}
          slotProps={{
            input: {
              startAdornment: (
                <InputAdornment position="start">
                  <SearchIcon fontSize="small" />
                </InputAdornment>
              ),
            },
          }}
        />
        <Typography variant="caption" color="text.secondary">
          {filtered.length} of {data?.length ?? 0} providers
        </Typography>
      </Stack>

      {filtered.length === 0 && (
        <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center', py: 4 }}>
          No providers match your search.
        </Typography>
      )}

      <Stack gap={1}>
        {filtered.map((provider) => (
          <ApiProviderCard key={`${provider.country}-${provider.name}`} provider={provider} />
        ))}
      </Stack>
    </Stack>
  );
}
