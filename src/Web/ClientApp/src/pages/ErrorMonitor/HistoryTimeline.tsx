import Stack from '@mui/material/Stack';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Tooltip from '@mui/material/Tooltip';
import Divider from '@mui/material/Divider';
import { StatusChip } from './StatusChip';
import { formatAbsoluteTime, formatRelativeTime } from '../../utils/formatDate';
import { sanitizeRichText } from './sanitizeRichText';
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
          {/* Comment is plain text (a simple 500-char-limited box, not rich text) - rendered
              directly, not via dangerouslySetInnerHTML, so literal "<"/"&" a user types can't be
              misread as markup. */}
          <Typography variant="body2" sx={{ mt: 0.5, whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>
            {entry.comment}
          </Typography>
          {entry.description && !/^\s*(<br\s*\/?>)?\s*$/i.test(entry.description) && (
            <>
              <Divider sx={{ my: 1 }} />
              <Typography variant="caption" color="text.secondary" display="block" sx={{ mb: 0.5 }}>
                Description
              </Typography>
              <Box
                sx={{ fontSize: 13.5, color: 'text.secondary', wordBreak: 'break-word', '& ul, & ol': { pl: 3, my: 0.5 } }}
                dangerouslySetInnerHTML={{ __html: sanitizeRichText(entry.description) }}
              />
            </>
          )}
        </Box>
      ))}
    </Stack>
  );
}
