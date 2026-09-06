import { AddOutlined, ArrowForwardOutlined, CheckCircleOutline, ChevronRightOutlined, ErrorOutline, FlagOutlined, InsightsOutlined, PeopleOutline, ScheduleOutlined } from '@mui/icons-material';

const setupSteps = [
  { label: 'Connect your provider', detail: 'Configure secure sign-in and session policies', done: false },
  { label: 'Create your first customer', detail: 'Customer records will appear here once added', done: false },
  { label: 'Invite your team', detail: 'Owners and permissions are managed in Administration', done: false },
];

export default function HomePage() {
  return <div className="page page-home">
    <div className="page-heading home-heading"><div><p className="eyebrow">Monday, September 5, 2026</p><h1>Good morning, Jordan.</h1><p className="page-subtitle">Your  workspace is ready.</p></div><button className="button button-primary"><AddOutlined /> Add activity</button></div>
    <section className="hero-strip"><div className="hero-icon"><InsightsOutlined /></div><div><p className="eyebrow">The CURE loop</p><h2>Context becomes action.</h2><p>Once your workspace is connected, CURE will surface what changed, why it matters, and the next action worth taking.</p></div><ArrowForwardOutlined className="hero-arrow" /></section>
    <div className="section-heading"><div><p className="eyebrow">Your attention</p><h2>Work that needs you</h2></div><span className="muted-label"> From your workspace</span></div>
    <section className="attention-grid"><AttentionCard icon={FlagOutlined} title="Priority actions" value="—" copy="Your prioritized work will appear here." /><AttentionCard icon={ErrorOutline} title="Relationship risks" value="—" copy="Signals need customer history to be evaluated." tone="amber" /><AttentionCard icon={ScheduleOutlined} title="Upcoming commitments" value="—" copy="Commitments will be tracked once activities are connected." tone="blue" /></section>
    <div className="home-columns"><section className="panel setup-panel"><div className="panel-heading"><div><p className="eyebrow">Workspace setup</p><h2>Make CURE yours</h2></div><span className="progress-ring">0<span>/3</span></span></div><p className="panel-intro">Complete these foundations so CURE serves you.</p><div className="setup-list">{setupSteps.map((step) => <div className="setup-item" key={step.label}><span className="setup-status">{step.done ? <CheckCircleOutline /> : <PeopleOutline />}</span><div><strong>{step.label}</strong><small>{step.detail}</small></div><ChevronRightOutlined /></div>)}</div></section><section className="panel signal-panel"><div className="panel-heading"><div><p className="eyebrow">Recent signals</p><h2>Nothing to review yet</h2></div><InsightsOutlined className="panel-heading-icon" /></div><div className="empty-panel"><div className="empty-icon"><InsightsOutlined /></div><p>Signals are explainable observations built from your customer history.</p><a href="/customers">Open customers <ArrowForwardOutlined /></a></div></section></div>
  </div>;
}

function AttentionCard({ icon: Icon, title, value, copy, tone = 'green' }) { return <div className={`attention-card tone-${tone}`}><div className="attention-card-top"><span className="attention-icon"><Icon /></span><span className="card-arrow"><ChevronRightOutlined /></span></div><strong className="attention-value">{value}</strong><h3>{title}</h3><p>{copy}</p></div>; }