import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import IconButton from '@mui/material/IconButton';
import PublicIcon from '@mui/icons-material/Public';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import { getCountryFlagEmoji } from '../../utils/countryFlags';

export function CountryGroupHeader({
  country,
  count,
  expanded,
  onToggle,
}: {
  country: string;
  count: number;
  expanded: boolean;
  onToggle: () => void;
}) {
  const flag = getCountryFlagEmoji(country);

  return (
    <Stack
      direction="row"
      alignItems="center"
      gap={1}
      role="button"
      tabIndex={0}
      aria-expanded={expanded}
      onClick={onToggle}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          onToggle();
        }
      }}
      sx={{ mt: 2.5, mb: 1, cursor: 'pointer', userSelect: 'none' }}
    >
      <IconButton size="small" tabIndex={-1} sx={{ p: 0.25 }}>
        <ExpandMoreIcon
          fontSize="small"
          sx={{
            transform: expanded ? 'rotate(0deg)' : 'rotate(-90deg)',
            transition: 'transform 0.15s ease',
          }}
        />
      </IconButton>
      {flag ? (
        <Typography component="span" sx={{ fontSize: 20, lineHeight: 1 }} aria-hidden>
          {flag}
        </Typography>
      ) : (
        <PublicIcon fontSize="small" sx={{ color: 'text.secondary' }} />
      )}
      <Typography variant="subtitle2" fontWeight={700} sx={{ textTransform: 'uppercase', letterSpacing: 0.6 }}>
        {country}
      </Typography>
      <Chip size="small" label={count} sx={{ height: 20, fontWeight: 600 }} />
      <Box sx={{ flexGrow: 1, borderBottom: 1, borderColor: 'divider' }} />
    </Stack>
  );
}
