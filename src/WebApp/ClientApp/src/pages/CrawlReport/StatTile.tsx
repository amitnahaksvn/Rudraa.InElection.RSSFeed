import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { formatCompactNumber } from '../../utils/formatNumber';

export interface StatTileProps {
  label: string;
  value: number;
  suffix?: string;
  caption?: string;
  color?: string;
}

export function StatTile({ label, value, suffix, caption, color }: StatTileProps) {
  return (
    <Card variant="outlined" sx={{ flex: '1 1 160px', minWidth: 160 }}>
      <CardContent sx={{ py: 1.5, '&:last-child': { pb: 1.5 } }}>
        <Stack gap={0.25}>
          <Typography variant="caption" color="text.secondary">
            {label}
          </Typography>
          <Typography variant="h5" fontWeight={700} sx={{ color }}>
            {formatCompactNumber(value)}
            {suffix}
          </Typography>
          {caption && (
            <Typography variant="caption" color="text.secondary">
              {caption}
            </Typography>
          )}
        </Stack>
      </CardContent>
    </Card>
  );
}
