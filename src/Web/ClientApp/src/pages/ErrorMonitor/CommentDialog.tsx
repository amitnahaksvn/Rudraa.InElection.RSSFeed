import { useState } from 'react';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogContentText from '@mui/material/DialogContentText';
import DialogActions from '@mui/material/DialogActions';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import Alert from '@mui/material/Alert';

interface CommentDialogProps {
  open: boolean;
  title: string;
  description?: string;
  confirmLabel: string;
  submitting?: boolean;
  errorMessage?: string | null;
  onCancel: () => void;
  onConfirm: (comment: string) => void;
}

export function CommentDialog({
  open,
  title,
  description,
  confirmLabel,
  submitting,
  errorMessage,
  onCancel,
  onConfirm,
}: CommentDialogProps) {
  const [comment, setComment] = useState('');

  const handleClose = () => {
    setComment('');
    onCancel();
  };

  const handleConfirm = () => {
    if (!comment.trim()) return;
    onConfirm(comment.trim());
  };

  return (
    <Dialog open={open} onClose={handleClose} fullWidth maxWidth="sm">
      <DialogTitle>{title}</DialogTitle>
      <DialogContent>
        {description && <DialogContentText sx={{ mb: 2 }}>{description}</DialogContentText>}
        <TextField
          autoFocus
          fullWidth
          multiline
          minRows={3}
          label="Comment"
          value={comment}
          onChange={(e) => setComment(e.target.value)}
          disabled={submitting}
        />
        {errorMessage && (
          <Alert severity="error" sx={{ mt: 2 }}>
            {errorMessage}
          </Alert>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose} disabled={submitting}>
          Cancel
        </Button>
        <Button onClick={handleConfirm} variant="contained" disabled={submitting || !comment.trim()}>
          {confirmLabel}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
