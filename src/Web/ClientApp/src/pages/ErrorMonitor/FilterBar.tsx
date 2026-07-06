import { useState, useEffect, useMemo } from 'react';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Autocomplete from '@mui/material/Autocomplete';
import InputAdornment from '@mui/material/InputAdornment';
import IconButton from '@mui/material/IconButton';
import Button from '@mui/material/Button';
import Badge from '@mui/material/Badge';
import Chip from '@mui/material/Chip';
import Collapse from '@mui/material/Collapse';
import SearchIcon from '@mui/icons-material/Search';
import ClearIcon from '@mui/icons-material/Clear';
import FilterListIcon from '@mui/icons-material/FilterList';
import type { ErrorLogFilters } from '../../api/types';

interface FilterBarProps {
  filters: ErrorLogFilters;
  onChange: (filters: ErrorLogFilters) => void;
  providerOptions?: string[];
  countryOptions?: string[];
  sourceOptions?: string[];
}

const EMPTY_ADVANCED: Pick<ErrorLogFilters, 'provider' | 'country' | 'source'> = { provider: '', country: '', source: '' };

const ADVANCED_FIELDS = [
  { key: 'provider', label: 'Provider' },
  { key: 'country', label: 'Country' },
  { key: 'source', label: 'Source' },
] as const;

// Debounces free-text fields (search/provider/country/source) so every keystroke doesn't fire a
// new request. The status filter lives in StatusSidebar now (a discrete click, not typing), not
// here. Provider/Country/Source live behind a collapsible "Filters" panel rather than three
// always-visible text fields - on a phone-width list pane, three full-width inputs ate most of
// the visible list before a single result even showed; a badge on the toggle button plus
// removable chips for whichever ones are actually set keeps that same filtering power without
// permanently spending the vertical space. Each field is an Autocomplete rather than a plain
// text input - freeSolo, so typing anything not in the suggestion list still works exactly like
// before - suggesting distinct values already seen among the currently loaded errors, since the
// backend has no dedicated "list distinct providers" endpoint to source a canonical list from.
export function FilterBar({ filters, onChange, providerOptions = [], countryOptions = [], sourceOptions = [] }: FilterBarProps) {
  const [draft, setDraft] = useState(filters);
  const [advancedOpen, setAdvancedOpen] = useState(false);

  useEffect(() => setDraft(filters), [filters]);

  useEffect(() => {
    const handle = setTimeout(() => {
      if (JSON.stringify(draft) !== JSON.stringify(filters)) {
        onChange(draft);
      }
    }, 350);
    return () => clearTimeout(handle);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [draft]);

  const activeAdvanced = useMemo(
    () => ADVANCED_FIELDS.filter((field) => draft[field.key].trim().length > 0),
    [draft],
  );

  const hasAnyActiveFilter = activeAdvanced.length > 0 || draft.search.trim().length > 0;

  const clearField = (key: (typeof ADVANCED_FIELDS)[number]['key']) => {
    const next = { ...draft, [key]: '' };
    setDraft(next);
    onChange(next);
  };

  const clearAll = () => {
    const next = { ...draft, ...EMPTY_ADVANCED, search: '' };
    setDraft(next);
    onChange(next);
  };

  return (
    <Stack gap={1} sx={{ p: 1.5 }}>
      <TextField
        size="small"
        placeholder="Search errors..."
        fullWidth
        value={draft.search}
        onChange={(e) => setDraft({ ...draft, search: e.target.value })}
        slotProps={{
          input: {
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon fontSize="small" />
              </InputAdornment>
            ),
            endAdornment: draft.search && (
              <InputAdornment position="end">
                <IconButton
                  size="small"
                  edge="end"
                  aria-label="Clear search"
                  onClick={() => {
                    const next = { ...draft, search: '' };
                    setDraft(next);
                    onChange(next);
                  }}
                >
                  <ClearIcon fontSize="small" />
                </IconButton>
              </InputAdornment>
            ),
          },
        }}
      />

      <Stack direction="row" alignItems="center" gap={1} flexWrap="wrap">
        <Badge badgeContent={activeAdvanced.length} color="primary">
          <Button
            size="small"
            variant={advancedOpen ? 'contained' : 'outlined'}
            startIcon={<FilterListIcon fontSize="small" />}
            onClick={() => setAdvancedOpen((v) => !v)}
          >
            Filters
          </Button>
        </Badge>

        {!advancedOpen &&
          activeAdvanced.map((field) => (
            <Chip
              key={field.key}
              size="small"
              label={`${field.label}: ${draft[field.key]}`}
              onDelete={() => clearField(field.key)}
            />
          ))}

        {hasAnyActiveFilter && (
          <Button size="small" color="inherit" onClick={clearAll} sx={{ ml: 'auto', color: 'text.secondary' }}>
            Clear all
          </Button>
        )}
      </Stack>

      <Collapse in={advancedOpen}>
        <Stack direction={{ xs: 'column', sm: 'row' }} gap={1} sx={{ pt: 0.5 }}>
          <Autocomplete
            freeSolo
            size="small"
            fullWidth
            options={providerOptions}
            inputValue={draft.provider}
            onInputChange={(_, value) => setDraft({ ...draft, provider: value })}
            renderInput={(params) => <TextField {...params} placeholder="Provider" />}
          />
          <Autocomplete
            freeSolo
            size="small"
            fullWidth
            options={countryOptions}
            inputValue={draft.country}
            onInputChange={(_, value) => setDraft({ ...draft, country: value })}
            renderInput={(params) => <TextField {...params} placeholder="Country" />}
          />
          <Autocomplete
            freeSolo
            size="small"
            fullWidth
            options={sourceOptions}
            inputValue={draft.source}
            onInputChange={(_, value) => setDraft({ ...draft, source: value })}
            renderInput={(params) => <TextField {...params} placeholder="Source" />}
          />
        </Stack>
      </Collapse>
    </Stack>
  );
}
