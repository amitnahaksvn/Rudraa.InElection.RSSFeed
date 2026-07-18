import { useMemo, useState } from 'react';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { formatFullNumber } from '../../utils/formatNumber';

// Pretty-prints JSON response bodies (2-space indent) so a test result reads like a formatted
// document instead of one giant minified line. RSS/Atom bodies (XML, not JSON) fail JSON.parse
// and fall through to being shown exactly as received - they already come reasonably formatted
// from the publisher, and this app never re-serializes XML anywhere else either.
function formatBody(raw: string): string {
  try {
    return JSON.stringify(JSON.parse(raw), null, 2);
  } catch {
    return raw;
  }
}

// Matches a publish-date field/tag across every shape this app's providers actually emit - JSON
// key variants (camelCase/snake_case) and the RSS/Atom/RDF tag names BaseRssProvider itself
// already special-cases (pubDate, Dublin Core's dc:date) - so whichever one a given provider used
// is easy to spot in a large raw dump instead of having to read every field. Deliberately narrow
// (named date fields only, not a bare "date") so it doesn't also light up unrelated fields like
// lastBuildDate/createdDate.
const PUBLISH_DATE_PATTERN =
  /("(?:pubDate|pub_date|publishedAt|published_at|publishDate|publish_date|publicationDate|publication_date|published)"\s*:\s*"[^"]*"|<\s*(?:pubDate|pubdate|published|dc:date)\s*>[^<]*<\s*\/\s*(?:pubDate|pubdate|published|dc:date)\s*>)/gi;

// String.split with a capturing-group regex interleaves the captured matches into the result
// array (odd indices), so this never needs raw HTML injection to highlight - every part stays a
// plain-text React child, safe even though the response body itself is untrusted provider output.
function withPublishDateHighlights(text: string) {
  return text.split(PUBLISH_DATE_PATTERN).map((part, index) =>
    index % 2 === 1 ? (
      <Box key={index} component="mark" sx={{ bgcolor: 'warning.light', color: 'warning.contrastText', borderRadius: 0.5, px: 0.25 }}>
        {part}
      </Box>
    ) : (
      part
    ),
  );
}

// Above this, the panel shows only the first half rather than the whole thing up front - a
// multi-hundred-KB feed/API dump would otherwise make the test-result panel unusably tall.
const TRUNCATE_THRESHOLD = 4000;

export function RawResponseViewer({ raw }: { raw: string }) {
  const [expanded, setExpanded] = useState(false);
  const formatted = useMemo(() => formatBody(raw), [raw]);
  const isLong = formatted.length > TRUNCATE_THRESHOLD;
  const visible = expanded || !isLong ? formatted : formatted.slice(0, Math.floor(formatted.length / 2));

  return (
    <Stack gap={0.5}>
      <Typography variant="caption" fontWeight={600} color="text.secondary">
        Raw response{isLong && !expanded ? ' (showing first half)' : ''}
      </Typography>
      <Box
        component="pre"
        sx={{
          m: 0,
          p: 1,
          maxHeight: 320,
          overflow: 'auto',
          bgcolor: 'action.hover',
          borderRadius: 1,
          fontSize: 12,
          fontFamily: 'monospace',
          whiteSpace: 'pre-wrap',
          wordBreak: 'break-word',
        }}
      >
        {withPublishDateHighlights(visible)}
        {isLong && !expanded && '\n…'}
      </Box>
      {isLong && (
        <Button size="small" variant="text" onClick={() => setExpanded((e) => !e)} sx={{ alignSelf: 'flex-start' }}>
          {expanded ? 'Show less' : `Show full response (${formatFullNumber(formatted.length)} characters)`}
        </Button>
      )}
    </Stack>
  );
}
