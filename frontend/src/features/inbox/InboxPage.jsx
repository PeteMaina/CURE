import { useQuery } from "@tanstack/react-query";
import { InboxOutlined, RefreshOutlined } from "@mui/icons-material";
import { inboxApi } from "../../services/api";

export default function InboxPage() {
  const inboxQuery = useQuery({ queryKey: ["inbox"], queryFn: inboxApi.list });
  return (
    <div className="page">
      <div className="page-heading">
        <div>
          <p className="eyebrow">A Focus on your</p>
          <h1>Inbox</h1>
          <p className="page-subtitle">
            Queue for decisions, commitments, and signals that need your
            response.
          </p>
        </div>
      </div>
      {inboxQuery.isLoading ? (
        <div className="table-skeleton">
          {[1, 2, 3].map((row) => (
            <div className="skeleton-row" key={row}>
              <span />
              <span />
              <span />
            </div>
          ))}
        </div>
      ) : inboxQuery.isError ? (
        <div className="state-panel error-state">
          <InboxOutlined />
          <h2>Inbox is unavailable</h2>
          <p>
            Connect the API to load actionable work. CURE will not fabricate
            tasks or notifications.
          </p>
          <button
            className="button button-secondary"
            onClick={() => inboxQuery.refetch()}
          >
            <RefreshOutlined /> Try again
          </button>
        </div>
      ) : (
        <div className="state-panel">
          <InboxOutlined />
          <h2>Your inbox is clear</h2>
          <p>
            Actionable work from workflows, commitments, approvals, and
            relationship signals will appear here.
          </p>
        </div>
      )}
    </div>
  );
}
