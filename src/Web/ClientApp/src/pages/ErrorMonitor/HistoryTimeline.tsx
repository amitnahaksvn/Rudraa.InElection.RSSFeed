import Stack from '@mui/material/Stack';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Tooltip from '@mui/material/Tooltip';
import { StatusChip } from './StatusChip';
import { formatAbsoluteTime, formatRelativeTime } from '../../utils/formatDate';
import type { ErrorLogHistoryEntry } from '../../api/types';

export function HistoryTimeline({ history }: { history: ErrorLogHistoryEntry[] }) {
  if (history.length === 0) {
    return (
      <Typography variant="body2" color="text.secondary">
        No comments or status changes yet.
      </Typography>
    );
  }

  return (
    <Stack gap={1.5}>
      {history.map((entry, index) => (
        <Box
          key={index}
          sx={{
            pl: 1.5,
            borderLeft: 3,
            borderLeftColor: entry.isResolved ? 'success.main' : 'error.main',
          }}
        >
          <Stack direction="row" alignItems="center" gap={1} flexWrap="wrap">
            <StatusChip isResolved={entry.isResolved} />
            <Tooltip title={formatAbsoluteTime(entry.createdOn)}>
              <Typography variant="caption" color="text.secondary">
                {formatRelativeTime(entry.createdOn)}
              </Typography>
            </Tooltip>
          </Stack>
          <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', wordBreak: 'break-word', mt: 0.5 }}>
            {entry.comment}
          </Typography>
        </Box>
      ))}
    </Stack>
  );
}
