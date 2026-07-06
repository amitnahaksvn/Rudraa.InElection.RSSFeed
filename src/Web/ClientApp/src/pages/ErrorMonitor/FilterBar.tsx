import { useState, useEffect } from 'react';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import InputAdornment from '@mui/material/InputAdornment';
import SearchIcon from '@mui/icons-material/Search';
import type { ErrorLogFilters } from '../../api/types';

interface FilterBarProps {
  filters: ErrorLogFilters;
  onChange: (filters: ErrorLogFilters) => void;
}

// Debounces free-text fields (search/provider/country/source) so every keystroke doesn't fire a
// new request. The status filter lives in StatusSidebar now (a discrete click, not typing), not here.
export function FilterBar({ filters, onChange }: FilterBarProps) {
  const [draft, setDraft] = useState(filters);

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
          },
        }}
      />
      <Stack direction="row" gap={1}>
        <TextField
          size="small"
          placeholder="Provider"
          fullWidth
          value={draft.provider}
          onChange={(e) => setDraft({ ...draft, provider: e.target.value })}
        />
        <TextField
          size="small"
          placeholder="Country"
          fullWidth
          value={draft.country}
          onChange={(e) => setDraft({ ...draft, country: e.target.value })}
        />
        <TextField
          size="small"
          placeholder="Source"
          fullWidth
          value={draft.source}
          onChange={(e) => setDraft({ ...draft, source: e.target.value })}
        />
      </Stack>
    </Stack>
  );
}
