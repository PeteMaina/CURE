import { useQuery } from "@tanstack/react-query";
import {
  AddOutlined,
  ArrowForwardOutlined,
  AssignmentOutlined,
  BusinessOutlined,
  ErrorOutline,
  InsightsOutlined,
  RefreshOutlined,
  ScheduleOutlined,
  SupportAgentOutlined,
} from "@mui/icons-material";
import { Link, useNavigate } from "react-router-dom";
import { workspaceApi } from "../../services/api";

export default function HomePage() {
  const navigate = useNavigate();
  const dashboard = useQuery({
    queryKey: ["home-dashboard"],
    queryFn: workspaceApi.dashboard,
  });
  const model = dashboard.data?.data;

  if (dashboard.isLoading) {
    return (
      <div className="page">
        <div className="table-skeleton">
          {[1, 2, 3].map((row) => (
            <div className="skeleton-row" key={row}>
              <span />
              <span />
              <span />
            </div>
          ))}
        </div>
      </div>
    );
  }

  if (dashboard.isError) {
    return (
      <div className="page">
        <div className="state-panel error-state" role="alert">
          <InsightsOutlined />
          <h1>Workspace overview is unavailable</h1>
          <p>{dashboard.error.message}</p>
          <button
            className="button button-secondary"
            onClick={() => dashboard.refetch()}
          >
            <RefreshOutlined /> Try again
          </button>
        </div>
      </div>
    );
  }

  const counts = model.counts;
  const greeting = getGreeting();

  return (
    <div className="page page-home">
      <div className="page-heading home-heading">
        <div>
          <p className="eyebrow">
            {new Intl.DateTimeFormat(undefined, {
              weekday: "long",
              month: "long",
              day: "numeric",
              year: "numeric",
            }).format(new Date())}
          </p>
          <h1>
            {greeting}, {model.userName}.
          </h1>
          <p className="page-subtitle">
            A current view of recorded work in this workspace.
          </p>
        </div>
        <button
          className="button button-primary"
          onClick={() => navigate("/activities?create=1")}
        >
          <AddOutlined /> Add activity
        </button>
      </div>

      <section className="workspace-metrics" aria-label="Workspace records">
        <MetricCard label="Customers" value={counts.customers} to="/customers" icon={BusinessOutlined} />
        <MetricCard label="Leads" value={counts.leads} to="/leads" icon={BusinessOutlined} />
        <MetricCard label="Opportunities" value={counts.opportunities} to="/opportunities" icon={InsightsOutlined} />
        <MetricCard label="Open cases" value={counts.openCases} to="/cases" icon={SupportAgentOutlined} />
        <MetricCard label="Activities due" value={counts.dueActivities} to="/inbox?category=activity" icon={ScheduleOutlined} />
      </section>

      <div className="section-heading">
        <div>
          <p className="eyebrow">Attention</p>
          <h2>Recorded work that needs review</h2>
        </div>
        <Link className="text-link" to="/inbox">
          Open inbox <ArrowForwardOutlined />
        </Link>
      </div>
      {model.attentionItems.length ? (
        <section className="attention-list" aria-label="Attention items">
          {model.attentionItems.map((item) => (
            <AttentionItem key={`${item.entityType}:${item.entityId}`} item={item} />
          ))}
        </section>
      ) : (
        <section className="panel empty-panel">
          <ScheduleOutlined />
          <h3>No due activities or open cases found</h3>
          <p>
            This reflects the records currently available. CURE does not infer
            health risks or commitments until their source data exists.
          </p>
        </section>
      )}

      <div className="home-columns">
        <section className="panel">
          <div className="panel-heading">
            <div>
              <p className="eyebrow">Recently updated</p>
              <h2>Customers</h2>
            </div>
            <BusinessOutlined className="panel-heading-icon" />
          </div>
          {model.recentCustomers.length ? (
            <div className="workspace-record-list">
              {model.recentCustomers.map((customer) => (
                <Link
                  className="workspace-record"
                  key={customer.id}
                  to={`/customers/${customer.id}`}
                >
                  <span className="workspace-record-icon">
                    <BusinessOutlined />
                  </span>
                  <span>
                    <strong>{customer.name}</strong>
                    <small>
                      {customer.industry || customer.type} · Updated{" "}
                      {formatDate(customer.updatedAt)}
                    </small>
                  </span>
                  <ArrowForwardOutlined />
                </Link>
              ))}
            </div>
          ) : (
            <p className="panel-intro">
              No customer records yet.{" "}
              <Link to="/customers?create=1">Create the first customer.</Link>
            </p>
          )}
        </section>

        <section className="panel">
          <div className="panel-heading">
            <div>
              <p className="eyebrow">Activity records</p>
              <h2>Recent activity</h2>
            </div>
            <AssignmentOutlined className="panel-heading-icon" />
          </div>
          {model.recentActivities.length ? (
            <div className="workspace-record-list">
              {model.recentActivities.map((activity) => (
                <Link
                  className="workspace-record"
                  key={activity.id}
                  to={`/activities?search=${encodeURIComponent(activity.subject)}`}
                >
                  <span className="workspace-record-icon">
                    <AssignmentOutlined />
                  </span>
                  <span>
                    <strong>{activity.subject}</strong>
                    <small>
                      {activity.customerName || "No customer linked"} ·{" "}
                      {activity.status} · {formatDate(activity.scheduledAt)}
                    </small>
                  </span>
                  <ArrowForwardOutlined />
                </Link>
              ))}
            </div>
          ) : (
            <p className="panel-intro">
              No activity records yet.{" "}
              <button
                className="inline-link"
                onClick={() => navigate("/activities?create=1")}
              >
                Add an activity.
              </button>
            </p>
          )}
        </section>
      </div>

      <section className="panel intelligence-note">
        <ErrorOutline />
        <div>
          <h2>Relationship intelligence is not available yet</h2>
          <p>
            Health scores, risk signals, forecasts, and recommended actions
            will appear only when their evidence and calculation services are
            connected. No risk-free status is implied by an empty signal list.
          </p>
        </div>
      </section>
    </div>
  );
}

function MetricCard({ label, value, to, icon: Icon }) {
  return (
    <Link className="workspace-metric" to={to}>
      <span className="workspace-metric-icon">
        <Icon />
      </span>
      <span className="workspace-metric-value">{value.toLocaleString()}</span>
      <span className="workspace-metric-label">{label}</span>
    </Link>
  );
}

function AttentionItem({ item }) {
  const resource = item.entityType === "case" ? "cases" : "activities";
  const label = item.entityType === "case" ? "Case" : "Activity";
  const customerLink = item.customerId
    ? `/customers/${item.customerId}`
    : null;

  return (
    <article className="attention-item">
      <span className={`attention-item-icon ${item.entityType}`}>
        {item.entityType === "case" ? (
          <SupportAgentOutlined />
        ) : (
          <ScheduleOutlined />
        )}
      </span>
      <div className="attention-item-copy">
        <p className="eyebrow">
          {item.category}
          {item.priority ? ` · ${item.priority}` : ""}
          {item.dueAt && new Date(item.dueAt) < new Date() ? " · Overdue" : ""}
        </p>
        <h3>{item.title}</h3>
        <p>
          {item.description || `${label} status: ${item.status}`}
          {item.customerName && customerLink ? (
            <>
              {" · "}
              <Link to={customerLink}>{item.customerName}</Link>
            </>
          ) : null}
        </p>
      </div>
      <Link
        className="button button-secondary"
        to={`/${resource}?search=${encodeURIComponent(item.title)}`}
      >
        Review
      </Link>
    </article>
  );
}

function getGreeting() {
  const hour = new Date().getHours();
  if (hour < 12) return "Good morning";
  if (hour < 18) return "Good afternoon";
  return "Good evening";
}

function formatDate(value) {
  if (!value) return "date not recorded";
  return new Intl.DateTimeFormat(undefined, {
    month: "short",
    day: "numeric",
  }).format(new Date(value));
}
