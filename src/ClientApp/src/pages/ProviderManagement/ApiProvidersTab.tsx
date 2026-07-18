import { useMemo, useState } from 'react';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import CircularProgress from '@mui/material/CircularProgress';
import Alert from '@mui/material/Alert';
import Typography from '@mui/material/Typography';
import InputAdornment from '@mui/material/InputAdornment';
import IconButton from '@mui/material/IconButton';
import Button from '@mui/material/Button';
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup';
import ToggleButton from '@mui/material/ToggleButton';
import Collapse from '@mui/material/Collapse';
import SearchIcon from '@mui/icons-material/Search';
import ClearIcon from '@mui/icons-material/Clear';
import UnfoldLessIcon from '@mui/icons-material/UnfoldLess';
import UnfoldMoreIcon from '@mui/icons-material/UnfoldMore';
import { useApiProviders } from './useApiProviders';
import { ApiProviderCard } from './ApiProviderCard';
import { CountryGroupHeader } from './CountryGroupHeader';
import type { ApiProviderSummary } from '../../api/providerTypes';

type StatusFilter = 'all' | 'enabled' | 'disabled';

export function ApiProvidersTab() {
  const { data, isLoading, isError } = useApiProviders();
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  const [collapsedCountries, setCollapsedCountries] = useState<Set<string>>(new Set());

  const filtered = useMemo(() => {
    if (!data) return [];
    const term = search.trim().toLowerCase();
    return data.filter((p) => {
      const matchesSearch = !term || p.name.toLowerCase().includes(term) || p.country.toLowerCase().includes(term);
      const matchesStatus = statusFilter === 'all' || (statusFilter === 'enabled') === p.enabled;
      return matchesSearch && matchesStatus;
    });
  }, [data, search, statusFilter]);

  const groupedByCountry = useMemo(() => {
    const groups = new Map<string, ApiProviderSummary[]>();
    for (const provider of filtered) {
      const list = groups.get(provider.country);
      if (list) {
        list.push(provider);
      } else {
        groups.set(provider.country, [provider]);
      }
    }
    return [...groups.entries()].sort(([a], [b]) => a.localeCompare(b));
  }, [filtered]);

  const toggleCountry = (country: string) => {
    setCollapsedCountries((prev) => {
      const next = new Set(prev);
      if (next.has(country)) {
        next.delete(country);
      } else {
        next.add(country);
      }
      return next;
    });
  };

  const allCollapsed = groupedByCountry.length > 0 && groupedByCountry.every(([country]) => collapsedCountries.has(country));

  const toggleAll = () => {
    setCollapsedCountries(allCollapsed ? new Set() : new Set(groupedByCountry.map(([country]) => country)));
  };

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
    <Stack gap={1}>
      <TextField
        size="small"
        placeholder="Search provider or country..."
        fullWidth
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        slotProps={{
          input: {
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon fontSize="small" />
              </InputAdornment>
            ),
            endAdornment: search && (
              <InputAdornment position="end">
                <IconButton size="small" edge="end" aria-label="Clear search" onClick={() => setSearch('')}>
                  <ClearIcon fontSize="small" />
                </IconButton>
              </InputAdornment>
            ),
          },
        }}
      />

      <Stack direction="row" alignItems="center" justifyContent="space-between" flexWrap="wrap" gap={1.5}>
        <ToggleButtonGroup
          size="small"
          exclusive
          value={statusFilter}
          onChange={(_, next) => next && setStatusFilter(next)}
        >
          <ToggleButton value="all">All</ToggleButton>
          <ToggleButton value="enabled">Enabled</ToggleButton>
          <ToggleButton value="disabled">Disabled</ToggleButton>
        </ToggleButtonGroup>

        <Stack direction="row" alignItems="center" gap={1.5}>
          {groupedByCountry.length > 0 && (
            <Button
              size="small"
              startIcon={allCollapsed ? <UnfoldMoreIcon fontSize="small" /> : <UnfoldLessIcon fontSize="small" />}
              onClick={toggleAll}
            >
              {allCollapsed ? 'Expand all' : 'Collapse all'}
            </Button>
          )}
          <Typography variant="caption" color="text.secondary">
            {filtered.length} of {data?.length ?? 0} providers
          </Typography>
        </Stack>
      </Stack>

      {filtered.length === 0 && (
        <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center', py: 4 }}>
          No providers match your search.
        </Typography>
      )}

      {groupedByCountry.map(([country, providers]) => {
        const expanded = !collapsedCountries.has(country);
        return (
          <Stack key={country} gap={1}>
            <CountryGroupHeader
              country={country}
              count={providers.length}
              expanded={expanded}
              onToggle={() => toggleCountry(country)}
            />
            <Collapse in={expanded}>
              <Stack gap={1}>
                {providers.map((provider) => (
                  <ApiProviderCard key={`${provider.country}-${provider.name}`} provider={provider} />
                ))}
              </Stack>
            </Collapse>
          </Stack>
        );
      })}
    </Stack>
  );
}
