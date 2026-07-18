import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogContentText from '@mui/material/DialogContentText';
import DialogActions from '@mui/material/DialogActions';
import Button from '@mui/material/Button';
import Alert from '@mui/material/Alert';

interface DeleteArticlesDialogProps {
  open: boolean;
  count: number;
  submitting?: boolean;
  errorMessage?: string | null;
  onCancel: () => void;
  onConfirm: () => void;
}

export function DeleteArticlesDialog({ open, count, submitting, errorMessage, onCancel, onConfirm }: DeleteArticlesDialogProps) {
  return (
    <Dialog open={open} onClose={onCancel} maxWidth="xs" fullWidth>
      <DialogTitle>Delete {count === 1 ? 'this article' : `${count} articles`}?</DialogTitle>
      <DialogContent>
        <DialogContentText>
          {count === 1
            ? 'This article will stop appearing in the News Feed.'
            : `These ${count} articles will stop appearing in the News Feed.`}{' '}
          It won't be re-added by a later crawl of the same source.
        </DialogContentText>
        {errorMessage && (
          <Alert severity="error" sx={{ mt: 2 }}>
            {errorMessage}
          </Alert>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onCancel} disabled={submitting}>
          Cancel
        </Button>
        <Button onClick={onConfirm} color="error" variant="contained" disabled={submitting}>
          Delete
        </Button>
      </DialogActions>
    </Dialog>
  );
}
