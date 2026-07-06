import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import Tooltip from '@mui/material/Tooltip';
import Collapse from '@mui/material/Collapse';
import { formatAbsoluteTime, formatRelativeTime } from '../../utils/formatDate';
import { StatusChip } from './StatusChip';
import { usePendingTransitions } from './PendingTransitionContext';
import { EXIT_ANIMATION_MS } from './errorLogCacheSync';
import type { ErrorLogSummary } from '../../api/types';

interface ErrorListRowProps {
  error: ErrorLogSummary;
  selected: boolean;
  onSelect: () => void;
}

// The compact "email list item" of the Outlook-style layout - a single block per error, no inline
// expansion; clicking selects it and its full detail renders in the pane alongside/after this
// list. Wrapped in a Collapse driven by leavingIds so a row about to disappear from this list
// (because it no longer matches the active status filter) animates out instead of vanishing.
export function ErrorListRow({ error, selected, onSelect }: ErrorListRowProps) {
  const { leavingIds } = usePendingTransitions();
  const leaving = leavingIds.has(error.id);

  return (
    <Collapse in={!leaving} timeout={EXIT_ANIMATION_MS}>
      <Box
        role="button"
        tabIndex={0}
        aria-pressed={selected}
        onClick={onSelect}
        onKeyDown={(e) => {
          if (e.key === 'Enter' || e.key === ' ') onSelect();
        }}
        sx={{
          px: 2,
          py: 1.25,
          cursor: 'pointer',
          borderLeft: 3,
          borderLeftColor: selected ? 'primary.main' : error.isResolved ? 'success.main' : 'error.main',
          bgcolor: selected ? 'action.selected' : 'transparent',
          '&:hover': { bgcolor: selected ? 'action.selected' : 'action.hover' },
          borderBottom: 1,
          borderBottomColor: 'divider',
        }}
      >
        <Stack direction="row" alignItems="center" justifyContent="space-between" gap={1}>
          <Typography variant="body2" fontWeight={700} noWrap sx={{ maxWidth: '70%' }}>
            {error.exceptionType.split('.').pop()}
          </Typography>
          <Tooltip title={formatAbsoluteTime(error.createdOn)}>
            <Typography variant="caption" color="text.secondary" noWrap>
              {formatRelativeTime(error.createdOn)}
            </Typography>
          </Tooltip>
        </Stack>
        <Typography variant="caption" color="text.secondary" noWrap sx={{ display: 'block' }}>
          {error.source}
          {error.provider ? ` · ${error.provider}` : ''}
        </Typography>
        <Typography
          variant="body2"
          sx={{
            mt: 0.5,
            overflow: 'hidden',
            textOverflow: 'ellipsis',
            display: '-webkit-box',
            WebkitLineClamp: 2,
            WebkitBoxOrient: 'vertical',
          }}
        >
          {error.message}
        </Typography>
        <Stack direction="row" alignItems="center" gap={0.75} sx={{ mt: 0.75 }}>
          <StatusChip isResolved={error.isResolved} />
          {error.httpStatusCode && <Chip size="small" variant="outlined" color="warning" label={`HTTP ${error.httpStatusCode}`} />}
        </Stack>
      </Box>
    </Collapse>
  );
}
