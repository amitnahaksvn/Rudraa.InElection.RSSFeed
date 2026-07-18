import Chip from '@mui/material/Chip';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import ErrorIcon from '@mui/icons-material/Error';

export function StatusChip({ isResolved }: { isResolved: boolean }) {
  return isResolved ? (
    <Chip size="small" color="success" variant="outlined" icon={<CheckCircleIcon />} label="Resolved" />
  ) : (
    <Chip size="small" color="error" icon={<ErrorIcon />} label="Unresolved" />
  );
}
