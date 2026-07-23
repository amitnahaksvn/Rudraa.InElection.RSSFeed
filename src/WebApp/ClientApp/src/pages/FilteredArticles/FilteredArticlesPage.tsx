import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import FilterAltIcon from '@mui/icons-material/FilterAlt';
import { FilteredArticlesTable } from './FilteredArticlesTable';

export function FilteredArticlesPage() {
  return (
    <Box sx={{ maxWidth: 1200, mx: 'auto' }}>
      <Stack direction="row" alignItems="center" gap={1.5} sx={{ mb: 0.5 }}>
        <FilterAltIcon color="primary" fontSize="large" />
        <Typography variant="h5" fontWeight={700}>
          Filtered Articles
        </Typography>
      </Stack>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Articles that were fetched but excluded because their category wasn't in the political allowlist - nothing here was ever saved as a real news article.
      </Typography>

      <FilteredArticlesTable />
    </Box>
  );
}
