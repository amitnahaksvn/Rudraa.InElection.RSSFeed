import DOMPurify from 'dompurify';
import Stack from '@mui/material/Stack';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Tooltip from '@mui/material/Tooltip';
import Divider from '@mui/material/Divider';
import { StatusChip } from './StatusChip';
import { formatAbsoluteTime, formatRelativeTime } from '../../utils/formatDate';
import type { ErrorLogHistoryEntry } from '../../api/types';

// Comment/Description are rich text (HTML) authored by whoever resolved/commented on this error -
// untrusted the moment it's rendered back to a *different* viewer, so every render path sanitizes
// with DOMPurify first rather than trusting what's in the database.
function sanitize(html: string): string {
  return DOMPurify.sanitize(html, { ALLOWED_TAGS: ['b', 'i', 'u', 'strong', 'em', 'ul', 'ol', 'li', 'br', 'div', 'span', 'p'] });
}

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
          <Box
            sx={{ fontSize: 14, mt: 0.5, wordBreak: 'break-word', '& ul, & ol': { pl: 3, my: 0.5 } }}
            dangerouslySetInnerHTML={{ __html: sanitize(entry.comment) }}
          />
          {entry.description && !/^\s*(<br\s*\/?>)?\s*$/i.test(entry.description) && (
            <>
              <Divider sx={{ my: 1 }} />
              <Typography variant="caption" color="text.secondary" display="block" sx={{ mb: 0.5 }}>
                Description
              </Typography>
              <Box
                sx={{ fontSize: 13.5, color: 'text.secondary', wordBreak: 'break-word', '& ul, & ol': { pl: 3, my: 0.5 } }}
                dangerouslySetInnerHTML={{ __html: sanitize(entry.description) }}
              />
            </>
          )}
        </Box>
      ))}
    </Stack>
  );
}
