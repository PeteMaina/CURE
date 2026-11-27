import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Box,
  Card,
  CardContent,
  Typography,
  Grid,
  Chip,
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  MenuItem,
  Alert,
  CircularProgress,
  IconButton,
  Stack,
  Tooltip,
} from '@mui/material';
import ArrowForwardOutlined from '@mui/icons-material/ArrowForwardOutlined';
import CheckCircleOutline from '@mui/icons-material/CheckCircleOutline';
import HighlightOffOutlined from '@mui/icons-material/HighlightOffOutlined';
import HistoryOutlined from '@mui/icons-material/HistoryOutlined';
import TransformOutlined from '@mui/icons-material/TransformOutlined';
import { pipelineApi, coreApi } from '../../services/api';

const STAGES = [
  { key: 'Qualification', label: 'Qualification', probability: 10, color: '#3b82f6' },
  { key: 'Discovery', label: 'Discovery', probability: 25, color: '#6366f1' },
  { key: 'Proposal', label: 'Proposal', probability: 50, color: '#8b5cf6' },
  { key: 'Negotiation', label: 'Negotiation', probability: 75, color: '#f59e0b' },
  { key: 'ClosedWon', label: 'Closed Won', probability: 100, color: '#10b981' },
  { key: 'ClosedLost', label: 'Closed Lost', probability: 0, color: '#ef4444' },
];

const WIN_REASONS = [
  { value: 'BEST_VALUE', label: 'Superior Value Proposition' },
  { value: 'PRODUCT_CAPABILITIES', label: 'Product Fit & Capabilities' },
  { value: 'RELATIONSHIP_TRUST', label: 'Executive Relationship & Trust' },
  { value: 'EXECUTIVE_SPONSOR', label: 'Strong Executive Sponsor' },
  { value: 'PRICING_DISCOUNT', label: 'Competitive Commercial Pricing' },
];

const LOSS_REASONS = [
  { value: 'COMPETITOR_SELECTED', label: 'Competitor Selected' },
  { value: 'BUDGET_CANCELLED', label: 'Budget Deprioritized/Cancelled' },
  { value: 'PRICE_TOO_HIGH', label: 'Price Resistance' },
  { value: 'DECISION_MAKER_UNAVAILABLE', label: 'Lack of Executive Authority' },
  { value: 'TIMING_POSTPONED', label: 'Postponed to Future Quarter' },
  { value: 'GHOSTED_NO_RESPONSE', label: 'Prospect Stopped Responding' },
];

export default function PipelineBoardPage() {
  const queryClient = useQueryClient();
  const [selectedOpp, setSelectedOpp] = useState(null);
  const [transitionDialog, setTransitionDialog] = useState(false);
  const [targetStage, setTargetStage] = useState('');
  const [closeReason, setCloseReason] = useState('');
  const [competitorName, setCompetitorName] = useState('');
  const [notes, setNotes] = useState('');
  const [historyDialog, setHistoryDialog] = useState(false);
  const [historyOpp, setHistoryOpp] = useState(null);
  const [convertDialog, setConvertDialog] = useState(false);
  const [selectedLead, setSelectedLead] = useState(null);
  const [customerType, setCustomerType] = useState('Business');
  const [createOppOnConvert, setCreateOppOnConvert] = useState(true);
  const [formError, setFormError] = useState(null);

  // Queries
  const summaryQuery = useQuery({
    queryKey: ['pipeline', 'summary'],
    queryFn: pipelineApi.summary,
    staleTime: 10_000,
  });

  const oppsQuery = useQuery({
    queryKey: ['opportunities'],
    queryFn: () => coreApi.list('opportunities', 'limit=200'),
    staleTime: 10_000,
  });

  const leadsQuery = useQuery({
    queryKey: ['leads'],
    queryFn: () => coreApi.list('leads', 'limit=50'),
    staleTime: 10_000,
  });

  const historyQuery = useQuery({
    queryKey: ['pipeline', 'history', historyOpp?.id],
    queryFn: () => pipelineApi.history(historyOpp.id),
    enabled: Boolean(historyOpp?.id),
  });

  // Mutations
  const transitionMutation = useMutation({
    mutationFn: ({ id, payload }) => pipelineApi.transition(id, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['pipeline', 'summary'] });
      queryClient.invalidateQueries({ queryKey: ['opportunities'] });
      setTransitionDialog(false);
      resetTransitionForm();
    },
    onError: (err) => {
      setFormError(err.message || 'Stage transition failed.');
    },
  });

  const convertLeadMutation = useMutation({
    mutationFn: ({ id, payload }) => pipelineApi.convertLead(id, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['leads'] });
      queryClient.invalidateQueries({ queryKey: ['customers'] });
      queryClient.invalidateQueries({ queryKey: ['opportunities'] });
      queryClient.invalidateQueries({ queryKey: ['pipeline', 'summary'] });
      setConvertDialog(false);
      setSelectedLead(null);
    },
    onError: (err) => {
      setFormError(err.message || 'Lead conversion failed.');
    },
  });

  const resetTransitionForm = () => {
    setSelectedOpp(null);
    setTargetStage('');
    setCloseReason('');
    setCompetitorName('');
    setNotes('');
    setFormError(null);
  };

  const handleOpenTransition = (opp, stage) => {
    setSelectedOpp(opp);
    setTargetStage(stage || '');
    setCloseReason('');
    setCompetitorName('');
    setNotes('');
    setFormError(null);
    setTransitionDialog(true);
  };

  const handleConfirmTransition = () => {
    if (!targetStage) {
      setFormError('Please select a target stage.');
      return;
    }
    if ((targetStage === 'ClosedWon' || targetStage === 'ClosedLost') && !closeReason) {
      setFormError('A reason is mandatory when closing an opportunity.');
      return;
    }
    transitionMutation.mutate({
      id: selectedOpp.id,
      payload: {
        targetStage,
        closeReason: closeReason || null,
        competitorName: competitorName || null,
        notes: notes || null,
        expectedVersion: selectedOpp.version,
      },
    });
  };

  const handleConvertLead = () => {
    if (!selectedLead) return;
    convertLeadMutation.mutate({
      id: selectedLead.id,
      payload: {
        customerType,
        createOpportunity: createOppOnConvert,
        opportunityName: `${selectedLead.companyName || selectedLead.lastName} - Qualified Deal`,
        opportunityAmount: selectedLead.estimatedValue || 25000,
        currency: 'USD',
      },
    });
  };

  const summary = summaryQuery.data?.data;
  const opportunities = oppsQuery.data?.data?.items || oppsQuery.data?.data || [];
  const leads = leadsQuery.data?.data?.items || leadsQuery.data?.data || [];
  const convertibleLeads = leads.filter((l) => l.status !== 'CONVERTED');

  const oppsByStage = STAGES.reduce((acc, stage) => {
    acc[stage.key] = opportunities.filter((o) =>
      o.stage?.toUpperCase() === stage.key.toUpperCase()
    );
    return acc;
  }, {});

  const formatMoney = (val) =>
    new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(val || 0);

  return (
    <Box sx={{ p: 3 }}>
      {/* Header & KPI Summary */}
      <Box sx={{ mb: 4, display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 2 }}>
        <div>
          <Typography variant="h4" fontWeight={700} gutterBottom>
            Revenue Pipeline & Deal Flow
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Deterministic stage gates, weighted revenue forecasts, and atomic lead conversion.
          </Typography>
        </div>
        <Button
          variant="contained"
          color="primary"
          startIcon={<TransformOutlined />}
          onClick={() => { setFormError(null); setConvertDialog(true); }}
        >
          Convert Qualified Lead
        </Button>
      </Box>

      {/* KPI Summary Cards */}
      <Grid container spacing={2} sx={{ mb: 4 }}>
        <Grid item xs={12} sm={6} md={3}>
          <Card variant="outlined" sx={{ borderLeft: '4px solid #3b82f6' }}>
            <CardContent sx={{ py: 1.5 }}>
              <Typography variant="caption" color="text.secondary" fontWeight={600} textTransform="uppercase">
                Active Pipeline
              </Typography>
              <Typography variant="h5" fontWeight={700} sx={{ mt: 0.5 }}>
                {formatMoney(summary?.totalPipelineAmount)}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                {summary?.totalOpenDeals || 0} open deals across active stages
              </Typography>
            </CardContent>
          </Card>
        </Grid>
        <Grid item xs={12} sm={6} md={3}>
          <Card variant="outlined" sx={{ borderLeft: '4px solid #8b5cf6' }}>
            <CardContent sx={{ py: 1.5 }}>
              <Typography variant="caption" color="text.secondary" fontWeight={600} textTransform="uppercase">
                Weighted Revenue Forecast
              </Typography>
              <Typography variant="h5" fontWeight={700} sx={{ mt: 0.5 }}>
                {formatMoney(summary?.totalWeightedAmount)}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                Probability-adjusted real expectations
              </Typography>
            </CardContent>
          </Card>
        </Grid>
        <Grid item xs={12} sm={6} md={3}>
          <Card variant="outlined" sx={{ borderLeft: '4px solid #10b981' }}>
            <CardContent sx={{ py: 1.5 }}>
              <Typography variant="caption" color="text.secondary" fontWeight={600} textTransform="uppercase">
                Win Rate (All-Time)
              </Typography>
              <Typography variant="h5" fontWeight={700} sx={{ mt: 0.5 }}>
                {summary?.winRatePercentage || 0}%
              </Typography>
              <Typography variant="caption" color="text.secondary">
                Ratio of closed-won deals to total closed
              </Typography>
            </CardContent>
          </Card>
        </Grid>
        <Grid item xs={12} sm={6} md={3}>
          <Card variant="outlined" sx={{ borderLeft: '4px solid #f59e0b' }}>
            <CardContent sx={{ py: 1.5 }}>
              <Typography variant="caption" color="text.secondary" fontWeight={600} textTransform="uppercase">
                Convertible Inbound Leads
              </Typography>
              <Typography variant="h5" fontWeight={700} sx={{ mt: 0.5 }}>
                {convertibleLeads.length}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                Prospects ready for customer generation
              </Typography>
            </CardContent>
          </Card>
        </Grid>
      </Grid>

      {/* Kanban Board Swimlanes */}
      {oppsQuery.isLoading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', p: 8 }}>
          <CircularProgress />
        </Box>
      ) : (
        <Grid container spacing={2} sx={{ overflowX: 'auto', minWidth: 1200 }}>
          {STAGES.map((stage) => {
            const opps = oppsByStage[stage.key] || [];
            const colAmount = opps.reduce((sum, o) => sum + Number(o.amount || 0), 0);
            const colWeighted = opps.reduce(
              (sum, o) => sum + (Number(o.amount || 0) * (Number(o.probability || stage.probability) / 100)),
              0
            );

            return (
              <Grid item xs={2} key={stage.key}>
                <Box
                  sx={{
                    backgroundColor: 'rgba(0,0,0,0.02)',
                    borderRadius: 2,
                    p: 1.5,
                    borderTop: `3px solid ${stage.color}`,
                    minHeight: 500,
                  }}
                >
                  {/* Column Header */}
                  <Box sx={{ mb: 2 }}>
                    <Typography variant="subtitle2" fontWeight={700}>
                      {stage.label}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {opps.length} deals • {formatMoney(colAmount)}
                    </Typography>
                    {stage.probability > 0 && stage.probability < 100 && (
                      <Typography variant="caption" display="block" color="text.secondary" sx={{ fontSize: '0.7rem' }}>
                        Weighted: {formatMoney(colWeighted)} ({stage.probability}%)
                      </Typography>
                    )}
                  </Box>

                  {/* Deal Cards */}
                  <Stack spacing={1.5}>
                    {opps.map((opp) => (
                      <Card
                        key={opp.id}
                        variant="outlined"
                        sx={{
                          boxShadow: '0 1px 3px rgba(0,0,0,0.05)',
                          '&:hover': { boxShadow: '0 4px 6px rgba(0,0,0,0.1)' },
                        }}
                      >
                        <CardContent sx={{ p: 1.5, '&:last-child': { pb: 1.5 } }}>
                          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 0.5 }}>
                            <Typography variant="caption" color="text.secondary" fontFamily="monospace">
                              {opp.opportunityNumber || 'OPP'}
                            </Typography>
                            <Chip
                              label={`${opp.probability ?? stage.probability}%`}
                              size="small"
                              sx={{ height: 20, fontSize: '0.65rem' }}
                            />
                          </Box>

                          <Typography variant="body2" fontWeight={600} noWrap title={opp.name}>
                            {opp.name}
                          </Typography>

                          <Typography variant="subtitle2" color="primary" fontWeight={700} sx={{ mt: 1 }}>
                            {formatMoney(opp.amount)}
                          </Typography>

                          {opp.expectedCloseDate && (
                            <Typography variant="caption" display="block" color="text.secondary" sx={{ mt: 0.5 }}>
                              Close: {opp.expectedCloseDate}
                            </Typography>
                          )}

                          {/* Quick Action Buttons */}
                          <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 0.5, mt: 1.5 }}>
                            <Tooltip title="View stage transition history">
                              <IconButton
                                size="small"
                                onClick={() => { setHistoryOpp(opp); setHistoryDialog(true); }}
                              >
                                <HistoryOutlined fontSize="small" />
                              </IconButton>
                            </Tooltip>
                            {stage.key !== 'ClosedWon' && stage.key !== 'ClosedLost' && (
                              <Button
                                size="small"
                                variant="outlined"
                                endIcon={<ArrowForwardOutlined fontSize="small" />}
                                onClick={() => handleOpenTransition(opp)}
                                sx={{ py: 0.2, px: 1, fontSize: '0.75rem' }}
                              >
                                Advance
                              </Button>
                            )}
                          </Box>
                        </CardContent>
                      </Card>
                    ))}
                    {opps.length === 0 && (
                      <Box sx={{ textAlign: 'center', py: 4, color: 'text.disabled' }}>
                        <Typography variant="caption">No deals</Typography>
                      </Box>
                    )}
                  </Stack>
                </Box>
              </Grid>
            );
          })}
        </Grid>
      )}

      {/* Transition Modal Dialog */}
      <Dialog open={transitionDialog} onClose={resetTransitionForm} maxWidth="sm" fullWidth>
        <DialogTitle>Advance Opportunity Stage</DialogTitle>
        <DialogContent>
          <Box sx={{ mt: 1 }}>
            {formError && <Alert severity="error" sx={{ mb: 2 }}>{formError}</Alert>}
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
              Moving <strong>{selectedOpp?.name}</strong> ({selectedOpp?.opportunityNumber})
            </Typography>

            <TextField
              select
              fullWidth
              label="Target Stage"
              value={targetStage}
              onChange={(e) => setTargetStage(e.target.value)}
              sx={{ mb: 2 }}
            >
              {STAGES.map((s) => (
                <MenuItem key={s.key} value={s.key}>
                  {s.label} ({s.probability}%)
                </MenuItem>
              ))}
            </TextField>

            {targetStage === 'ClosedWon' && (
              <TextField
                select
                fullWidth
                required
                label="Winning Factor (Mandatory for Closed Won)"
                value={closeReason}
                onChange={(e) => setCloseReason(e.target.value)}
                sx={{ mb: 2 }}
              >
                {WIN_REASONS.map((r) => (
                  <MenuItem key={r.value} value={r.value}>{r.label}</MenuItem>
                ))}
              </TextField>
            )}

            {targetStage === 'ClosedLost' && (
              <>
                <TextField
                  select
                  fullWidth
                  required
                  label="Loss Factor (Mandatory for Closed Lost)"
                  value={closeReason}
                  onChange={(e) => setCloseReason(e.target.value)}
                  sx={{ mb: 2 }}
                >
                  {LOSS_REASONS.map((r) => (
                    <MenuItem key={r.value} value={r.value}>{r.label}</MenuItem>
                  ))}
                </TextField>
                <TextField
                  fullWidth
                  label="Selected Competitor (if applicable)"
                  value={competitorName}
                  onChange={(e) => setCompetitorName(e.target.value)}
                  sx={{ mb: 2 }}
                />
              </>
            )}

            <TextField
              fullWidth
              multiline
              rows={3}
              label="Transition Notes / Justification"
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
            />
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={resetTransitionForm} disabled={transitionMutation.isPending}>
            Cancel
          </Button>
          <Button
            variant="contained"
            color="primary"
            onClick={handleConfirmTransition}
            disabled={transitionMutation.isPending}
          >
            {transitionMutation.isPending ? 'Committing...' : 'Confirm Stage Move'}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Stage History Dialog */}
      <Dialog open={historyDialog} onClose={() => setHistoryDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Deal Stage Audit History</DialogTitle>
        <DialogContent>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            Immutable transition audit trail for <strong>{historyOpp?.name}</strong>
          </Typography>
          {historyQuery.isLoading ? (
            <CircularProgress />
          ) : (
            <Stack spacing={1}>
              {historyQuery.data?.data?.map((entry) => (
                <Card key={entry.id} variant="outlined" sx={{ p: 1.5 }}>
                  <Typography variant="subtitle2" fontWeight={600}>
                    {entry.fromStage || 'Initiated'} → {entry.toStage}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {formatMoney(entry.amount)} • {new Date(entry.changedAt).toLocaleString()}
                  </Typography>
                  {entry.closeReason && (
                    <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
                      Reason: <em>{entry.closeReason}</em>
                    </Typography>
                  )}
                  {entry.notes && (
                    <Typography variant="caption" display="block" color="text.secondary">
                      Notes: {entry.notes}
                    </Typography>
                  )}
                </Card>
              ))}
              {(!historyQuery.data?.data || historyQuery.data.data.length === 0) && (
                <Typography variant="body2" color="text.secondary">No transitions recorded yet.</Typography>
              )}
            </Stack>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setHistoryDialog(false)}>Close</Button>
        </DialogActions>
      </Dialog>

      {/* Convert Lead Dialog */}
      <Dialog open={convertDialog} onClose={() => setConvertDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Convert Prospect Lead into Customer</DialogTitle>
        <DialogContent>
          <Box sx={{ mt: 1 }}>
            {formError && <Alert severity="error" sx={{ mb: 2 }}>{formError}</Alert>}
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
              Atomically generates a Customer/Account, Contact, and Opportunity in a single transaction.
            </Typography>

            <TextField
              select
              fullWidth
              required
              label="Select Inbound Lead"
              value={selectedLead ? selectedLead.id : ''}
              onChange={(e) => {
                const lead = convertibleLeads.find((l) => l.id === e.target.value);
                setSelectedLead(lead || null);
              }}
              sx={{ mb: 2 }}
            >
              {convertibleLeads.map((lead) => (
                <MenuItem key={lead.id} value={lead.id}>
                  {lead.companyName || `${lead.firstName} ${lead.lastName}`} ({lead.leadNumber}) - {lead.status}
                </MenuItem>
              ))}
            </TextField>

            <TextField
              select
              fullWidth
              label="Customer Account Type"
              value={customerType}
              onChange={(e) => setCustomerType(e.target.value)}
              sx={{ mb: 2 }}
            >
              <MenuItem value="Business">Business</MenuItem>
              <MenuItem value="Individual">Individual</MenuItem>
              <MenuItem value="Enterprise">Enterprise</MenuItem>
            </TextField>

            {selectedLead && (
              <Alert severity="info" sx={{ mt: 1 }}>
                Will create Customer: <strong>{selectedLead.companyName || `${selectedLead.firstName} ${selectedLead.lastName}`}</strong>
                <br />
                Will link Primary Contact: <strong>{selectedLead.firstName} {selectedLead.lastName}</strong>
                <br />
                Will spawn Qualified Deal: <strong>{formatMoney(selectedLead.estimatedValue || 25000)}</strong>
              </Alert>
            )}
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConvertDialog(false)} disabled={convertLeadMutation.isPending}>
            Cancel
          </Button>
          <Button
            variant="contained"
            color="primary"
            onClick={handleConvertLead}
            disabled={!selectedLead || convertLeadMutation.isPending}
          >
            {convertLeadMutation.isPending ? 'Converting...' : 'Execute Conversion'}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
