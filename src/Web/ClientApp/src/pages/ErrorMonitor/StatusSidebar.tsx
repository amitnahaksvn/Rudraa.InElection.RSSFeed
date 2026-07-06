import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import Divider from '@mui/material/Divider';
import type { ErrorLogCategory, ErrorLogCounts, ErrorLogFilters, ResolvedFilter } from '../../api/types';

interface StatusSidebarProps {
  filters: ErrorLogFilters;
  counts: ErrorLogCounts | undefined;
  onChange: (filters: ErrorLogFilters) => void;
  direction?: 'column' | 'row';
}

interface SidebarItem {
  key: string;
  label: string;
  color: string;
  countKey: keyof ErrorLogCounts;
  isActive: (filters: ErrorLogFilters) => boolean;
  select: (filters: ErrorLogFilters) => ErrorLogFilters;
}

const STATUS_ITEMS: SidebarItem[] = (
  [
    { value: 'all', label: 'All Errors', color: '#757575' },
    { value: 'unresolved', label: 'Unresolved', color: '#d32f2f' },
    { value: 'resolved', label: 'Resolved', color: '#2e7d32' },
  ] satisfies { value: ResolvedFilter; label: string; color: string }[]
).map((item) => ({
  key: item.value,
  label: item.label,
  color: item.color,
  countKey: item.value as keyof ErrorLogCounts,
  isActive: (filters) => filters.category === null && filters.status === item.value,
  select: (filters) => ({ ...filters, status: item.value, category: null }),
}));

const CATEGORY_ITEMS: SidebarItem[] = (
  [
    { value: 'Rss', label: 'RSS', color: '#1976d2', countKey: 'rss' },
    { value: 'Api', label: 'News API', color: '#7b1fa2', countKey: 'api' },
    { value: 'Social', label: 'Social', color: '#00897b', countKey: 'social' },
    { value: 'Http', label: 'HTTP', color: '#ef6c00', countKey: 'http' },
    { value: 'Critical', label: 'Critical', color: '#b71c1c', countKey: 'critical' },
    { value: 'Warning', label: 'Warning', color: '#f9a825', countKey: 'warning' },
  ] satisfies { value: ErrorLogCategory; label: string; color: string; countKey: keyof ErrorLogCounts }[]
).map((item) => ({
  key: item.value,
  label: item.label,
  color: item.color,
  countKey: item.countKey,
  isActive: (filters) => filters.category === item.value,
  select: (filters) => ({ ...filters, status: 'all', category: item.value }),
}));

function SidebarRow({ item, active, counts, isRow, onClick }: {
  item: SidebarItem;
  active: boolean;
  counts: ErrorLogCounts | undefined;
  isRow: boolean;
  onClick: () => void;
}) {
  return (
    <Box
      role="button"
      tabIndex={0}
      aria-pressed={active}
      onClick={onClick}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') onClick();
      }}
      sx={{
        px: 2,
        py: isRow ? 1 : 1.25,
        cursor: 'pointer',
        whiteSpace: 'nowrap',
        borderLeft: isRow ? 0 : 3,
        borderBottom: isRow ? 3 : 0,
        borderColor: active ? 'primary.main' : 'transparent',
        bgcolor: active ? 'action.selected' : 'transparent',
        '&:hover': { bgcolor: 'action.hover' },
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: 1,
      }}
    >
      <Stack direction="row" alignItems="center" gap={1}>
        <Box sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: item.color, flexShrink: 0 }} />
        <Typography variant="body2" fontWeight={active ? 700 : 500}>
          {item.label}
        </Typography>
      </Stack>
      {counts && (
        <Chip
          size="small"
          variant="outlined"
          label={counts[item.countKey]}
          sx={{ borderColor: item.color, color: item.color, fontWeight: 600 }}
        />
      )}
    </Box>
  );
}

// The left-hand "folder list" of the Outlook-style layout: a Status group (All/Unresolved/
// Resolved) plus a Category group (Rss/Api/Social/Http/Critical/Warning - see ErrorLogCategory),
// each item tinted with its own color (a dot + matching count chip) so the list reads at a glance,
// the way the reference design's colored severity labels did. Status and Category are mutually
// exclusive - picking one resets the other, since the backend only ever filters by one or the
// other for a single list view.
export function StatusSidebar({ filters, counts, onChange, direction = 'column' }: StatusSidebarProps) {
  const isRow = direction === 'row';

  return (
    <Stack direction={direction} sx={isRow ? { overflowX: 'auto' } : { py: 1 }}>
      {STATUS_ITEMS.map((item) => (
        <SidebarRow
          key={item.key}
          item={item}
          active={item.isActive(filters)}
          counts={counts}
          isRow={isRow}
          onClick={() => onChange(item.select(filters))}
        />
      ))}

      <Divider orientation={isRow ? 'vertical' : 'horizontal'} flexItem sx={isRow ? { mx: 1 } : { my: 1 }} />

      {CATEGORY_ITEMS.map((item) => (
        <SidebarRow
          key={item.key}
          item={item}
          active={item.isActive(filters)}
          counts={counts}
          isRow={isRow}
          onClick={() => onChange(item.select(filters))}
        />
      ))}
    </Stack>
  );
}
