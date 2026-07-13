import { useState } from 'react';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import Divider from '@mui/material/Divider';
import IconButton from '@mui/material/IconButton';
import CircularProgress from '@mui/material/CircularProgress';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import type { ErrorLogCategory, ErrorLogCategoryBreakdown, ErrorLogCounts, ErrorLogFilters, ResolvedFilter } from '../../api/types';

interface StatusSidebarProps {
  filters: ErrorLogFilters;
  counts: ErrorLogCounts | undefined;
  breakdown: ErrorLogCategoryBreakdown[] | undefined;
  isBreakdownLoading: boolean;
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
  select: (filters) => ({ ...filters, status: item.value, category: null, provider: '' }),
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
  isActive: (filters) => filters.category === item.value && !filters.provider,
  select: (filters) => ({ ...filters, status: 'all', category: item.value, provider: '' }),
}));

// Only the four "pipeline" categories (see Application.Abstractions.ErrorLogCategory) have a
// meaningful per-provider/feed breakdown - Critical/Warning are an HTTP-status-derived severity
// that cuts across every pipeline, not a fetch source of their own, so they get no expand toggle.
const EXPANDABLE_CATEGORIES = new Set<ErrorLogCategory>(['Rss', 'Api', 'Social', 'Http']);

function SidebarRow({ item, active, counts, isRow, onClick, expandable, expanded, onToggleExpand }: {
  item: SidebarItem;
  active: boolean;
  counts: ErrorLogCounts | undefined;
  isRow: boolean;
  onClick: () => void;
  expandable?: boolean;
  expanded?: boolean;
  onToggleExpand?: () => void;
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
        px: expandable ? 0.5 : 2,
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
        {expandable && !isRow && (
          <IconButton
            size="small"
            aria-label={expanded ? `Collapse ${item.label}` : `Expand ${item.label}`}
            onClick={(e) => {
              e.stopPropagation();
              onToggleExpand?.();
            }}
          >
            {expanded ? <ExpandMoreIcon fontSize="small" /> : <ChevronRightIcon fontSize="small" />}
          </IconButton>
        )}
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

// A single provider/feed row nested under a pipeline category once expanded - e.g. "AajTak" or
// "ABPNews" under RSS - selecting it narrows the list to that exact category + provider.
function ProviderRow({ label, color, count, active, onClick }: { label: string; color: string; count: number; active: boolean; onClick: () => void }) {
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
        pl: 5,
        pr: 2,
        py: 0.75,
        cursor: 'pointer',
        bgcolor: active ? 'action.selected' : 'transparent',
        '&:hover': { bgcolor: 'action.hover' },
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: 1,
      }}
    >
      <Typography variant="caption" fontWeight={active ? 700 : 400} noWrap sx={{ color: 'text.secondary' }}>
        {label}
      </Typography>
      <Chip size="small" variant="outlined" label={count} sx={{ borderColor: color, color, height: 18, fontSize: 11 }} />
    </Box>
  );
}

// The left-hand "folder list" of the Outlook-style layout: a Status group (All/Unresolved/
// Resolved) plus a Category group (Rss/Api/Social/Http/Critical/Warning - see ErrorLogCategory),
// each item tinted with its own color (a dot + matching count chip) so the list reads at a glance,
// the way the reference design's colored severity labels did. Status and Category are mutually
// exclusive - picking one resets the other, since the backend only ever filters by one or the
// other for a single list view. The four pipeline categories can also be expanded to show which
// specific provider/feed (e.g. AajTak, ABPNews under Rss) is producing the errors, via
// GetErrorLogProviderBreakdownQuery - selecting one of those narrows the list to that exact
// category + provider.
export function StatusSidebar({ filters, counts, breakdown, isBreakdownLoading, onChange, direction = 'column' }: StatusSidebarProps) {
  const isRow = direction === 'row';
  const [expanded, setExpanded] = useState<Set<ErrorLogCategory>>(new Set());

  const toggleExpand = (category: ErrorLogCategory) => {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(category)) next.delete(category);
      else next.add(category);
      return next;
    });
  };

  const breakdownFor = (category: ErrorLogCategory) => breakdown?.find((b) => b.category === category);

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

      {CATEGORY_ITEMS.map((item) => {
        const category = item.key as ErrorLogCategory;
        const isExpandable = EXPANDABLE_CATEGORIES.has(category) && !isRow;
        const isExpanded = expanded.has(category);
        const group = breakdownFor(category);

        return (
          <Box key={item.key}>
            <SidebarRow
              item={item}
              active={item.isActive(filters)}
              counts={counts}
              isRow={isRow}
              onClick={() => onChange(item.select(filters))}
              expandable={isExpandable}
              expanded={isExpanded}
              onToggleExpand={() => toggleExpand(category)}
            />

            {isExpandable && isExpanded && (
              <Box>
                {isBreakdownLoading && (
                  <Stack alignItems="center" sx={{ py: 1 }}>
                    <CircularProgress size={16} />
                  </Stack>
                )}

                {!isBreakdownLoading && group && group.providers.length === 0 && (
                  <Typography variant="caption" color="text.secondary" sx={{ display: 'block', pl: 5, py: 0.5 }}>
                    No errors
                  </Typography>
                )}

                {!isBreakdownLoading &&
                  group?.providers.map((p) => (
                    <ProviderRow
                      key={p.provider}
                      label={p.provider}
                      color={item.color}
                      count={p.count}
                      active={filters.category === category && filters.provider === p.provider}
                      onClick={() => onChange({ ...filters, status: 'all', category, provider: p.provider })}
                    />
                  ))}
              </Box>
            )}
          </Box>
        );
      })}
    </Stack>
  );
}
