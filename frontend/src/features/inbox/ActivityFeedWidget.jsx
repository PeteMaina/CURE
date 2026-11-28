import React, { useState } from 'react';
import {
  Box, Typography, Chip, Divider, IconButton, Tooltip,
  CircularProgress, Alert, Button, Menu, MenuItem,
  Card, CardContent, Stack,
} from '@mui/material';
import PhoneIcon from '@mui/icons-material/Phone';
import EmailIcon from '@mui/icons-material/Email';
import EventIcon from '@mui/icons-material/Event';
import NoteIcon from '@mui/icons-material/NoteAlt';
import TaskAltIcon from '@mui/icons-material/TaskAlt';
import PresentationIcon from '@mui/icons-material/Slideshow';
import LinkedInIcon from '@mui/icons-material/LinkedIn';
import SmsIcon from '@mui/icons-material/Sms';
import TrendingUpIcon from '@mui/icons-material/TrendingUp';
import HistoryIcon from '@mui/icons-material/History';
import AddIcon from '@mui/icons-material/Add';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { activityApi } from '../../services/api';
import LogActivityModal from './LogActivityModal';

const TYPE_META = {
  Call:            { icon: <PhoneIcon fontSize="small" />,        color: '#1976d2', label: 'Call' },
  Email:           { icon: <EmailIcon fontSize="small" />,        color: '#388e3c', label: 'Email' },
  Meeting:         { icon: <EventIcon fontSize="small" />,        color: '#7b1fa2', label: 'Meeting' },
  Note:            { icon: <NoteIcon fontSize="small" />,         color: '#f57c00', label: 'Note' },
  Task:            { icon: <TaskAltIcon fontSize="small" />,      color: '#c62828', label: 'Task' },
  Demo:            { icon: <PresentationIcon fontSize="small" />, color: '#00695c', label: 'Demo' },
  LinkedInMessage: { icon: <LinkedInIcon fontSize="small" />,     color: '#0288d1', label: 'LinkedIn' },
  SmsMessage:      { icon: <SmsIcon fontSize="small" />,          color: '#558b2f', label: 'SMS' },
  StageChanged:    { icon: <TrendingUpIcon fontSize="small" />,   color: '#5d4037', label: 'Pipeline' },
  default:         { icon: <HistoryIcon fontSize="small" />,      color: '#616161', label: 'Event' },
};

function typeMeta(eventType) {
  return TYPE_META[eventType] ?? TYPE_META.default;
}

function relativeTime(iso) {
  const diff = Date.now() - new Date(iso).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1)  return 'just now';
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24)  return `${hrs}h ago`;
  const days = Math.floor(hrs / 24);
  return `${days}d ago`;
}

function TimelineEntry({ entry }) {
  const meta = typeMeta(entry.eventType);
  return (
    <Box sx={{ display: 'flex', gap: 1.5, py: 1.25 }}>
      <Box sx={{
        mt: 0.5, width: 32, height: 32, borderRadius: '50%', flexShrink: 0,
        bgcolor: `${meta.color}18`, color: meta.color,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
      }}>
        {meta.icon}
      </Box>
      <Box sx={{ flex: 1, minWidth: 0 }}>
        <Stack direction="row" alignItems="center" spacing={1} flexWrap="wrap">
          <Chip label={meta.label} size="small"
            sx={{ bgcolor: `${meta.color}18`, color: meta.color,
                  fontWeight: 600, fontSize: '0.68rem', height: 20 }} />
          <Typography variant="body2" noWrap sx={{ fontWeight: 500, flex: 1 }}>
            {entry.summary}
          </Typography>
          <Typography variant="caption" color="text.secondary" sx={{ whiteSpace: 'nowrap' }}>
            {relativeTime(entry.occurredAt)}
          </Typography>
        </Stack>
        {entry.detail && (
          <Typography variant="caption" color="text.secondary"
            sx={{ display: 'block', mt: 0.3, whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>
            {entry.detail}
          </Typography>
        )}
      </Box>
    </Box>
  );
}

/**
 * ActivityFeedWidget
 *
 * Renders the unified customer 360 timeline: activities, deal stage transitions,
 * and audit events merged newest-first with keyset pagination.
 *
 * Props:
 *   customerId  — uuid of the customer whose timeline to display
 *   compact     — when true, renders in a collapsed card suitable for a sidebar
 */
export default function ActivityFeedWidget({ customerId, compact = false }) {
  const queryClient = useQueryClient();
  const [logModalOpen, setLogModalOpen] = useState(false);
  const [anchorEl, setAnchorEl] = useState(null);
  const [selectedType, setSelectedType] = useState('Call');

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['customer-timeline', customerId],
    queryFn: () => activityApi.timeline(customerId, 25),
    enabled: !!customerId,
    staleTime: 30_000,
  });

  const completeMutation = useMutation({
    mutationFn: ({ id, outcome }) => activityApi.complete(id, outcome),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['customer-timeline', customerId] }),
  });

  const entries = data?.data?.entries ?? [];
  const hasMore = data?.data?.hasMore ?? false;

  const handleQuickLog = (type) => {
    setSelectedType(type);
    setAnchorEl(null);
    setLogModalOpen(true);
  };

  if (isLoading) {
    return <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}><CircularProgress size={28} /></Box>;
  }
  if (isError) {
    return <Alert severity="error" sx={{ m: 2 }}>{error?.message ?? 'Failed to load timeline.'}</Alert>;
  }

  return (
    <Box>
      {/* Header row */}
      <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ mb: 1 }}>
        <Typography variant={compact ? 'subtitle2' : 'h6'} fontWeight={700}>
          Activity Timeline
        </Typography>
        <Stack direction="row" spacing={0.5}>
          <Tooltip title="Log activity">
            <IconButton size="small" onClick={(e) => setAnchorEl(e.currentTarget)}>
              <AddIcon fontSize="small" />
            </IconButton>
          </Tooltip>
          <Menu anchorEl={anchorEl} open={Boolean(anchorEl)} onClose={() => setAnchorEl(null)}>
            {['Call', 'Email', 'Meeting', 'Note', 'Task', 'Demo'].map((t) => (
              <MenuItem key={t} onClick={() => handleQuickLog(t)} dense>
                <Box sx={{ mr: 1, color: typeMeta(t).color }}>{typeMeta(t).icon}</Box>
                {t}
              </MenuItem>
            ))}
          </Menu>
        </Stack>
      </Stack>

      {entries.length === 0 ? (
        <Card variant="outlined" sx={{ textAlign: 'center', py: 3 }}>
          <CardContent>
            <Typography variant="body2" color="text.secondary">
              No activity yet. Log a call, email, or note to start the timeline.
            </Typography>
            <Button size="small" startIcon={<AddIcon />} sx={{ mt: 1 }}
              onClick={() => { setSelectedType('Call'); setLogModalOpen(true); }}>
              Log first activity
            </Button>
          </CardContent>
        </Card>
      ) : (
        <Box>
          {entries.map((entry, i) => (
            <React.Fragment key={entry.entryId}>
              <TimelineEntry entry={entry} onComplete={completeMutation.mutate} />
              {i < entries.length - 1 && <Divider />}
            </React.Fragment>
          ))}
          {hasMore && (
            <Typography variant="caption" color="text.secondary"
              sx={{ display: 'block', textAlign: 'center', mt: 1 }}>
              Scroll for older entries
            </Typography>
          )}
        </Box>
      )}

      <LogActivityModal
        open={logModalOpen}
        defaultType={selectedType}
        customerId={customerId}
        onClose={() => setLogModalOpen(false)}
        onLogged={() => {
          setLogModalOpen(false);
          queryClient.invalidateQueries({ queryKey: ['customer-timeline', customerId] });
        }}
      />
    </Box>
  );
}
