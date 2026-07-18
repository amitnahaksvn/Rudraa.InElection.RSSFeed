import Card from '@mui/material/Card';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import Link from '@mui/material/Link';
import Checkbox from '@mui/material/Checkbox';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import ArticleIcon from '@mui/icons-material/Article';
import PublicIcon from '@mui/icons-material/Public';
import RssFeedIcon from '@mui/icons-material/RssFeed';
import ApiIcon from '@mui/icons-material/Api';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import type { NewsArticle } from '../../api/newsTypes';
import { getCountryFlagEmoji } from '../../utils/countryFlags';
import { formatAbsoluteTime, formatRelativeTime } from '../../utils/formatDate';

interface ArticleCardProps {
  article: NewsArticle;
  selected: boolean;
  onToggleSelect: (id: string) => void;
  onDelete: (id: string) => void;
}

export function ArticleCard({ article, selected, onToggleSelect, onDelete }: ArticleCardProps) {
  const flag = getCountryFlagEmoji(article.country);
  const timestamp = article.publishedAt ?? article.crawledAt;

  return (
    <Card variant="outlined" sx={{ display: 'flex', overflow: 'hidden' }}>
      <Stack alignItems="center" justifyContent="flex-start" sx={{ pl: 0.5, pt: 0.5 }}>
        <Checkbox
          size="small"
          checked={selected}
          onChange={() => onToggleSelect(article.id)}
          aria-label={selected ? 'Deselect article' : 'Select article'}
        />
      </Stack>

      <Box
        sx={{
          width: { xs: 96, sm: 180 },
          flexShrink: 0,
          bgcolor: 'action.hover',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        {article.imageUrl ? (
          <Box
            component="img"
            src={article.imageUrl}
            alt=""
            loading="lazy"
            onError={(e) => {
              // A fair number of providers' images 404/expire after the fact - fall back to the
              // placeholder rather than showing a broken-image glyph.
              e.currentTarget.style.display = 'none';
            }}
            sx={{ width: '100%', height: '100%', objectFit: 'cover', display: 'block', minHeight: 120 }}
          />
        ) : (
          <ArticleIcon sx={{ fontSize: 40, color: 'text.disabled' }} />
        )}
      </Box>

      <Stack sx={{ p: 1.5, minWidth: 0, flex: 1 }} gap={0.75}>
        <Link
          href={article.url}
          target="_blank"
          rel="noopener noreferrer"
          underline="hover"
          color="inherit"
          variant="subtitle1"
          fontWeight={600}
          sx={{
            display: '-webkit-box',
            WebkitLineClamp: 2,
            WebkitBoxOrient: 'vertical',
            overflow: 'hidden',
          }}
        >
          {article.title}
        </Link>

        {article.summary && (
          <Typography
            variant="body2"
            color="text.secondary"
            sx={{ display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden' }}
          >
            {article.summary}
          </Typography>
        )}

        <Stack direction="row" flexWrap="wrap" gap={0.5} sx={{ mt: 'auto', pt: 0.5 }}>
          <Chip
            size="small"
            variant="outlined"
            icon={<PublicIcon fontSize="small" />}
            label={flag ? `${flag} ${article.country}` : article.country}
          />
          <Chip
            size="small"
            variant="outlined"
            icon={article.sourceType === 'Rss' ? <RssFeedIcon fontSize="small" /> : <ApiIcon fontSize="small" />}
            label={article.sourceType === 'Rss' ? 'RSS' : 'API'}
          />
          <Chip size="small" variant="outlined" label={article.category} />
          <Chip size="small" variant="outlined" label={article.provider} />
          <Chip size="small" variant="outlined" label={article.language.toUpperCase()} />
          <Typography
            variant="caption"
            color="text.secondary"
            title={formatAbsoluteTime(timestamp)}
            sx={{ display: 'flex', alignItems: 'center', ml: 'auto', pl: 1 }}
          >
            {formatRelativeTime(timestamp)}
          </Typography>
          <Tooltip title="Delete article">
            <IconButton size="small" aria-label="Delete article" onClick={() => onDelete(article.id)}>
              <DeleteOutlineIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        </Stack>
      </Stack>
    </Card>
  );
}
