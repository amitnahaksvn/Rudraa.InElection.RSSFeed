import { useEffect, useRef, useState } from 'react';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogContentText from '@mui/material/DialogContentText';
import DialogActions from '@mui/material/DialogActions';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import Alert from '@mui/material/Alert';
import { RichTextEditor } from './RichTextEditor';

const COMMENT_MAX_LENGTH = 500;

interface CommentDialogProps {
  open: boolean;
  title: string;
  helperText?: string;
  confirmLabel: string;
  submitting?: boolean;
  errorMessage?: string | null;
  onCancel: () => void;
  onConfirm: (comment: string, description: string) => void;
}

export function CommentDialog({
  open,
  title,
  helperText,
  confirmLabel,
  submitting,
  errorMessage,
  onCancel,
  onConfirm,
}: CommentDialogProps) {
  const [comment, setComment] = useState('');
  const [descriptionHtml, setDescriptionHtml] = useState('');
  // Bumped every time the dialog transitions closed -> open, so the Description RichTextEditor
  // below remounts (via key) and clears its contentEditable DOM instead of carrying over stale
  // HTML - contentEditable is inherently uncontrolled, so this is how it gets "reset".
  const [instanceKey, setInstanceKey] = useState(0);
  const wasOpen = useRef(false);

  useEffect(() => {
    if (open && !wasOpen.current) {
      setComment('');
      setDescriptionHtml('');
      setInstanceKey((k) => k + 1);
    }
    wasOpen.current = open;
  }, [open]);

  const handleConfirm = () => {
    if (!comment.trim()) return;
    onConfirm(comment.trim(), descriptionHtml);
  };

  const commentIsBlank = !comment.trim();

  return (
    <Dialog open={open} onClose={onCancel} fullWidth maxWidth="sm">
      <DialogTitle>{title}</DialogTitle>
      <DialogContent>
        {helperText && <DialogContentText sx={{ mb: 2 }}>{helperText}</DialogContentText>}
        <Stack gap={2}>
          <TextField
            autoFocus
            fullWidth
            multiline
            minRows={3}
            label="Comment"
            required
            placeholder="What happened, or what did you do..."
            value={comment}
            onChange={(e) => setComment(e.target.value.slice(0, COMMENT_MAX_LENGTH))}
            disabled={submitting}
            slotProps={{ htmlInput: { maxLength: COMMENT_MAX_LENGTH } }}
            helperText={`${comment.length}/${COMMENT_MAX_LENGTH}`}
          />
          <RichTextEditor
            key={`description-${instanceKey}`}
            label="Description (optional)"
            placeholder="Any extra detail - steps to reproduce, root cause, links..."
            onChange={setDescriptionHtml}
            disabled={submitting}
          />
        </Stack>
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
        <Button onClick={handleConfirm} variant="contained" disabled={submitting || commentIsBlank}>
          {confirmLabel}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
