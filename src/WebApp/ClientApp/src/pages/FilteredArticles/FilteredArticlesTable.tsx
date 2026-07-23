import { useState } from 'react';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TablePagination from '@mui/material/TablePagination';
import TableRow from '@mui/material/TableRow';
import Typography from '@mui/material/Typography';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import Chip from '@mui/material/Chip';
import CircularProgress from '@mui/material/CircularProgress';
import Box from '@mui/material/Box';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogContentText from '@mui/material/DialogContentText';
import DialogActions from '@mui/material/DialogActions';
import Button from '@mui/material/Button';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import { formatAbsoluteTime, formatRelativeTime } from '../../utils/formatDate';
import { useFilteredArticles } from './useFilteredArticles';
import { useDeleteFilteredArticle } from './useDeleteFilteredArticle';

const PAGE_SIZE_OPTIONS = [10, 25, 50];

export function FilteredArticlesTable() {
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(25);
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null);

  const { data, isLoading, isFetching } = useFilteredArticles(page, pageSize);
  const deleteMutation = useDeleteFilteredArticle();

  const articles = data?.items ?? [];

  const handleConfirmDelete = () => {
    if (!pendingDeleteId) return;
    deleteMutation.mutate(pendingDeleteId);
    setPendingDeleteId(null);
  };

  return (
    <Box sx={{ position: 'relative' }}>
      <TableContainer sx={{ maxHeight: 600, opacity: isFetching && !isLoading ? 0.6 : 1, transition: 'opacity 0.15s' }}>
        <Table size="small" stickyHeader>
          <TableHead>
            <TableRow>
              <TableCell>Provider</TableCell>
              <TableCell>Title</TableCell>
              <TableCell>Summary</TableCell>
              <TableCell>Category</TableCell>
              <TableCell align="center">Type</TableCell>
              <TableCell>Pulled</TableCell>
              <TableCell align="center">Delete</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {articles.map((article) => (
              <TableRow key={article.id} hover>
                <TableCell sx={{ whiteSpace: 'nowrap' }}>{article.provider}</TableCell>
                <TableCell sx={{ maxWidth: 320 }}>
                  <Typography variant="body2" noWrap title={article.title}>
                    {article.title}
                  </Typography>
                </TableCell>
                <TableCell sx={{ maxWidth: 420 }}>
                  <Typography variant="body2" color="text.secondary" noWrap title={article.summary ?? ''}>
                    {article.summary || '—'}
                  </Typography>
                </TableCell>
                <TableCell sx={{ whiteSpace: 'nowrap' }}>{article.category}</TableCell>
                <TableCell align="center">
                  <Chip label={article.sourceType} size="small" variant="outlined" />
                </TableCell>
                <TableCell sx={{ whiteSpace: 'nowrap' }}>
                  <Tooltip title={formatAbsoluteTime(article.pulledAt)}>
                    <span>{formatRelativeTime(article.pulledAt)}</span>
                  </Tooltip>
                </TableCell>
                <TableCell align="center">
                  <IconButton size="small" onClick={() => setPendingDeleteId(article.id)} aria-label="Delete filtered article">
                    <DeleteOutlineIcon fontSize="small" />
                  </IconButton>
                </TableCell>
              </TableRow>
            ))}
            {!isLoading && articles.length === 0 && (
              <TableRow>
                <TableCell colSpan={7} align="center">
                  <Typography variant="body2" color="text.secondary" sx={{ py: 3 }}>
                    Nothing has been filtered out yet.
                  </Typography>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>

      {isLoading && (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 3 }}>
          <CircularProgress size={24} />
        </Box>
      )}

      <TablePagination
        component="div"
        count={data?.totalCount ?? 0}
        rowsPerPageOptions={PAGE_SIZE_OPTIONS}
        page={page}
        rowsPerPage={pageSize}
        onPageChange={(_, newPage) => setPage(newPage)}
        onRowsPerPageChange={(e) => {
          setPageSize(parseInt(e.target.value, 10));
          setPage(0);
        }}
      />

      <Dialog open={pendingDeleteId !== null} onClose={() => setPendingDeleteId(null)}>
        <DialogTitle>Delete this record?</DialogTitle>
        <DialogContent>
          <DialogContentText>This only removes the filtered-out log entry - it never existed as a real article.</DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPendingDeleteId(null)}>Cancel</Button>
          <Button onClick={handleConfirmDelete} color="error" variant="contained">
            Delete
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
