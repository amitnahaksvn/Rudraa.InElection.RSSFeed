import Stack from '@mui/material/Stack';
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup';
import ToggleButton from '@mui/material/ToggleButton';
import TextField from '@mui/material/TextField';

export type DateRangePreset = '1' | '7' | '30' | '90' | 'custom';

export interface DateRangeControlProps {
  preset: DateRangePreset;
  onPresetChange: (preset: DateRangePreset) => void;
  customFrom: string;
  customTo: string;
  onCustomFromChange: (value: string) => void;
  onCustomToChange: (value: string) => void;
}

// Filters are standard UI, not chart marks (see the dataviz skill's interaction.md) - one row,
// presets before a custom range, so a reader reaches for "last 7 days" before fighting a calendar.
export function DateRangeControl({
  preset,
  onPresetChange,
  customFrom,
  customTo,
  onCustomFromChange,
  onCustomToChange,
}: DateRangeControlProps) {
  return (
    <Stack direction="row" flexWrap="wrap" alignItems="center" gap={1.5}>
      <ToggleButtonGroup
        size="small"
        exclusive
        value={preset}
        onChange={(_, next: DateRangePreset | null) => next && onPresetChange(next)}
      >
        <ToggleButton value="1">Today</ToggleButton>
        <ToggleButton value="7">Last 7 days</ToggleButton>
        <ToggleButton value="30">Last 30 days</ToggleButton>
        <ToggleButton value="90">Last 90 days</ToggleButton>
        <ToggleButton value="custom">Custom</ToggleButton>
      </ToggleButtonGroup>

      {preset === 'custom' && (
        <Stack direction="row" alignItems="center" gap={1}>
          <TextField
            size="small"
            type="date"
            label="From"
            value={customFrom}
            onChange={(e) => onCustomFromChange(e.target.value)}
            slotProps={{ inputLabel: { shrink: true } }}
          />
          <TextField
            size="small"
            type="date"
            label="To"
            value={customTo}
            onChange={(e) => onCustomToChange(e.target.value)}
            slotProps={{ inputLabel: { shrink: true } }}
          />
        </Stack>
      )}
    </Stack>
  );
}
