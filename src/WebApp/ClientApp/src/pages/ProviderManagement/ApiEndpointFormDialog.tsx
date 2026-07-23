import { useState } from 'react';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import Button from '@mui/material/Button';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import FormControlLabel from '@mui/material/FormControlLabel';
import Switch from '@mui/material/Switch';
import Typography from '@mui/material/Typography';
import { useCreateFeed, useUpdateFeed } from './useCrawlFeedMutations';
import type { ApiEndpointSummary } from '../../api/providerTypes';

export interface ApiEndpointFormDialogProps {
  open: boolean;
  provider: string;
  country: string;
  endpoint?: ApiEndpointSummary;
  onClose: () => void;
}

/** Add/edit dialog for one JSON-API endpoint - `endpoint` present means edit, absent means add a new endpoint under `provider`'s `country` schedule. */
export function ApiEndpointFormDialog({ open, provider, country, endpoint, onClose }: ApiEndpointFormDialogProps) {
  const isEdit = endpoint !== undefined;
  const [name, setName] = useState(endpoint?.name ?? '');
  const [path, setPath] = useState(endpoint?.endpoint ?? '');
  const [category, setCategory] = useState(endpoint?.category ?? 'General');
  const [language, setLanguage] = useState(endpoint?.language ?? 'en');
  const [enabled, setEnabled] = useState(endpoint?.enabled ?? true);
  const [queryParamsText, setQueryParamsText] = useState('{}');
  const [queryParamsError, setQueryParamsError] = useState<string | null>(null);

  const createFeed = useCreateFeed('Api');
  const updateFeed = useUpdateFeed('Api');
  const mutation = isEdit ? updateFeed : createFeed;

  const handleSubmit = () => {
    let queryParameters: Record<string, string> | null = null;
    try {
      queryParameters = queryParamsText.trim() === '' ? null : JSON.parse(queryParamsText);
      setQueryParamsError(null);
    } catch {
      setQueryParamsError('Query parameters must be valid JSON, e.g. {"country":"in"}');
      return;
    }

    const fields = { name, url: path, category, language, enabled, defaultImageUrl: null, queryParameters };
    if (isEdit) {
      updateFeed.mutate({ id: endpoint.id, fields }, { onSuccess: onClose });
    } else {
      createFeed.mutate({ pipeline: 'Api', provider, country, ...fields }, { onSuccess: onClose });
    }
  };

  const valid = name.trim() !== '' && path.trim() !== '' && category.trim() !== '' && language.trim() !== '';

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{isEdit ? `Edit endpoint - ${provider}` : `Add endpoint - ${provider}`}</DialogTitle>
      <DialogContent>
        <Stack gap={2} sx={{ mt: 1 }}>
          <TextField label="Name" value={name} onChange={(e) => setName(e.target.value)} fullWidth autoFocus />
          <TextField
            label="Endpoint path"
            value={path}
            onChange={(e) => setPath(e.target.value)}
            placeholder="everything"
            helperText="Appended to the provider's own Base URL"
            fullWidth
          />
          <TextField label="Category" value={category} onChange={(e) => setCategory(e.target.value)} fullWidth />
          <TextField label="Language" value={language} onChange={(e) => setLanguage(e.target.value)} fullWidth />
          <TextField
            label="Query parameters (JSON)"
            value={queryParamsText}
            onChange={(e) => setQueryParamsText(e.target.value)}
            placeholder='{"country":"in"}'
            error={queryParamsError !== null}
            helperText={queryParamsError}
            multiline
            minRows={2}
            fullWidth
          />
          <FormControlLabel control={<Switch checked={enabled} onChange={(e) => setEnabled(e.target.checked)} />} label="Enabled" />
          {mutation.isError && (
            <Typography variant="body2" color="error">
              {(mutation.error as Error).message}
            </Typography>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button variant="contained" disabled={!valid || mutation.isPending} onClick={handleSubmit}>
          {isEdit ? 'Save' : 'Add'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
