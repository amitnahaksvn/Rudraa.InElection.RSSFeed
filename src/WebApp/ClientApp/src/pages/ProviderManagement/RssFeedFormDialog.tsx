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
import type { RssFeedSummary } from '../../api/providerTypes';

export interface RssFeedFormDialogProps {
  open: boolean;
  provider: string;
  country: string;
  feed?: RssFeedSummary;
  onClose: () => void;
}

/** Add/edit dialog for one RSS feed - `feed` present means edit, absent means add a new feed under `provider`'s `country` schedule. */
export function RssFeedFormDialog({ open, provider, country, feed, onClose }: RssFeedFormDialogProps) {
  const isEdit = feed !== undefined;
  const [name, setName] = useState(feed?.name ?? '');
  const [url, setUrl] = useState(feed?.url ?? '');
  const [category, setCategory] = useState(feed?.category ?? '');
  const [language, setLanguage] = useState(feed?.language ?? 'hi');
  const [enabled, setEnabled] = useState(feed?.enabled ?? true);

  const createFeed = useCreateFeed('Rss');
  const updateFeed = useUpdateFeed('Rss');
  const mutation = isEdit ? updateFeed : createFeed;

  const handleSubmit = () => {
    const fields = { name, url, category, language, enabled, defaultImageUrl: null, queryParameters: null };
    if (isEdit) {
      updateFeed.mutate({ id: feed.id, fields }, { onSuccess: onClose });
    } else {
      createFeed.mutate({ pipeline: 'Rss', provider, country, ...fields }, { onSuccess: onClose });
    }
  };

  const valid = name.trim() !== '' && url.trim() !== '' && category.trim() !== '' && language.trim() !== '';

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{isEdit ? `Edit feed - ${provider}` : `Add feed - ${provider}`}</DialogTitle>
      <DialogContent>
        <Stack gap={2} sx={{ mt: 1 }}>
          <TextField label="Name" value={name} onChange={(e) => setName(e.target.value)} fullWidth autoFocus />
          <TextField label="Feed URL" value={url} onChange={(e) => setUrl(e.target.value)} fullWidth />
          <TextField label="Category" value={category} onChange={(e) => setCategory(e.target.value)} fullWidth />
          <TextField label="Language" value={language} onChange={(e) => setLanguage(e.target.value)} fullWidth />
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
