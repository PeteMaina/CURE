import React, { useState } from 'react';
import {
  Dialog, DialogTitle, DialogContent, DialogActions,
  TextField, MenuItem, Button, Stack, CircularProgress, Alert,
} from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { activityApi } from '../../services/api';

const ACTIVITY_TYPES = [
  { value: 'Call',            label: 'Phone call' },
  { value: 'Email',           label: 'Email' },
  { value: 'Meeting',         label: 'Meeting' },
  { value: 'Note',            label: 'Note' },
  { value: 'Task',            label: 'Task' },
  { value: 'Demo',            label: 'Product demo' },
  { value: 'LinkedInMessage', label: 'LinkedIn message' },
  { value: 'SmsMessage',      label: 'SMS' },
];

/**
 * LogActivityModal
 *
 * Thin form wired to POST /api/v1/activities.
 * Accepts defaultType so the quick-log buttons pre-select the right type.
 */
export default function LogActivityModal({
  open,
  defaultType = 'Call',
  customerId,
  linkedOpportunityId,
  onClose,
  onLogged,
}) {
  const queryClient = useQueryClient();
  const [type, setType] = useState(defaultType);
  const [subject, setSubject] = useState('');
  const [notes, setNotes] = useState('');
  const [errorMsg, setErrorMsg] = useState('');

  // Reset form when modal opens
  React.useEffect(() => {
    if (open) {
      setType(defaultType);
      setSubject('');
      setNotes('');
      setErrorMsg('');
    }
  }, [open, defaultType]);

  const { mutate, isPending } = useMutation({
    mutationFn: (payload) => activityApi.log(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['customer-timeline', customerId] });
      onLogged?.();
    },
    onError: (err) => setErrorMsg(err?.message ?? 'Failed to log activity.'),
  });

  const handleSubmit = (e) => {
    e.preventDefault();
    if (!subject.trim()) { setErrorMsg('Subject is required.'); return; }
    mutate({
      type,
      subject: subject.trim(),
      notes:   notes.trim() || undefined,
      linkedCustomerId:    customerId || undefined,
      linkedOpportunityId: linkedOpportunityId || undefined,
    });
  };

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>Log activity</DialogTitle>
      <form onSubmit={handleSubmit}>
        <DialogContent>
          {errorMsg && <Alert severity="error" sx={{ mb: 2 }}>{errorMsg}</Alert>}
          <Stack spacing={2}>
            <TextField
              select label="Type" value={type}
              onChange={(e) => setType(e.target.value)}
              size="small" fullWidth>
              {ACTIVITY_TYPES.map((t) => (
                <MenuItem key={t.value} value={t.value}>{t.label}</MenuItem>
              ))}
            </TextField>
            <TextField
              label="Subject" value={subject}
              onChange={(e) => setSubject(e.target.value)}
              size="small" fullWidth required
              placeholder={type === 'Call' ? 'e.g. Follow-up call with procurement' : 'Activity subject'} />
            <TextField
              label="Notes" value={notes}
              onChange={(e) => setNotes(e.target.value)}
              size="small" fullWidth multiline minRows={3}
              placeholder="Optional notes…" />
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={onClose} disabled={isPending}>Cancel</Button>
          <Button type="submit" variant="contained" disabled={isPending}
            startIcon={isPending ? <CircularProgress size={16} /> : null}>
            Log
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
