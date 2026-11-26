import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  AssignmentOutlined,
  InboxOutlined,
  RefreshOutlined,
  SupportAgentOutlined,
} from "@mui/icons-material";
import { Link, useSearchParams } from "react-router-dom";
import { inboxApi, notificationApi } from "../../services/api";

const categories = [
  { value: "", label: "All work" },
  { value: "activity", label: "Due activities" },
  { value: "case", label: "Open cases" },
];

export default function InboxPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const queryClient = useQueryClient();
  const category = searchParams.get("category") || "";
  const page = Math.max(1, Number(searchParams.get("page") || "1"));
  const queryString = new URLSearchParams({
    page: String(page),
    pageSize: "25",
  });
  if (category) queryString.set("category", category);

  const inboxQuery = useQuery({
    queryKey: ["inbox", category, page],
    queryFn: () => inboxApi.list(queryString.toString()),
  });
  const readMutation = useMutation({
    mutationFn: ({ item, isRead }) =>
      isRead
        ? notificationApi.markRead(item.entityType, item.entityId)
        : notificationApi.markUnread(item.entityType, item.entityId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["inbox"] });
      queryClient.invalidateQueries({ queryKey: ["notifications"] });
    },
  });
  const data = inboxQuery.data?.data || [];
  const meta = inboxQuery.data?.meta;

  function chooseCategory(value) {
    const next = new URLSearchParams(searchParams);
    if (value) next.set("category", value);
    else next.delete("category");
    next.delete("page");
    setSearchParams(next);
  }

  function movePage(nextPage) {
    const next = new URLSearchParams(searchParams);
    if (nextPage > 1) next.set("page", String(nextPage));
    else next.delete("page");
    setSearchParams(next);
  }

  return (
    <div className="page">
      <div className="page-heading">
        <div>
          <p className="eyebrow">Workspace attention</p>
          <h1>Inbox</h1>
          <p className="page-subtitle">
            Due activities and open cases from recorded workspace data. Signals,
            approvals, and commitments are not included until their sources
            exist.
          </p>
        </div>
        <button
          className="icon-button"
          onClick={() => inboxQuery.refetch()}
          aria-label="Refresh inbox"
        >
          <RefreshOutlined />
        </button>
      </div>

      <div className="inbox-summary" aria-label="Inbox totals">
        <span>
          <strong>{meta?.dueActivityCount ?? "—"}</strong> due activities
        </span>
        <span>
          <strong>{meta?.openCaseCount ?? "—"}</strong> open cases
        </span>
      </div>

      <div className="inbox-filters" role="group" aria-label="Filter inbox">
        {categories.map((option) => (
          <button
            className={category === option.value ? "active" : ""}
            key={option.value || "all"}
            type="button"
            aria-pressed={category === option.value}
            onClick={() => chooseCategory(option.value)}
          >
            {option.label}
          </button>
        ))}
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
        <div className="state-panel error-state" role="alert">
          <InboxOutlined />
          <h2>Inbox is unavailable</h2>
          <p>{inboxQuery.error.message}</p>
          <button
            className="button button-secondary"
            onClick={() => inboxQuery.refetch()}
          >
            <RefreshOutlined /> Try again
          </button>
        </div>
      ) : data.length ? (
        <>
          <section className="attention-list" aria-label="Inbox items">
            {data.map((item) => {
              const resource =
                item.entityType === "case" ? "cases" : "activities";
              const Icon =
                item.entityType === "case"
                  ? SupportAgentOutlined
                  : AssignmentOutlined;
              const isOverdue =
                item.dueAt && new Date(item.dueAt) < new Date();
              return (
                <article
                  className={`attention-item ${item.isRead ? "is-read" : ""}`}
                  key={`${item.entityType}:${item.entityId}`}
                >
                  <span className={`attention-item-icon ${item.entityType}`}>
                    <Icon />
                  </span>
                  <div className="attention-item-copy">
                    <p className="eyebrow">
                      {item.category}
                      {item.priority ? ` · ${item.priority}` : ""}
                      {isOverdue ? " · Overdue" : ""}
                      {item.isRead ? " · Reviewed" : ""}
                    </p>
                    <h2>{item.title}</h2>
                    <p>
                      {item.description || `Current status: ${item.status}`}
                      {item.customerName && item.customerId ? (
                        <>
                          {" · "}
                          <Link to={`/customers/${item.customerId}`}>
                            {item.customerName}
                          </Link>
                        </>
                      ) : null}
                    </p>
                    <small>
                      {item.dueAt
                        ? `Scheduled ${formatDateTime(item.dueAt)}`
                        : `Updated ${formatDateTime(item.updatedAt)}`}
                    </small>
                  </div>
                  <div className="attention-item-actions">
                    <Link
                      className="button button-secondary"
                      to={`/${resource}?search=${encodeURIComponent(item.title)}`}
                    >
                      Open record
                    </Link>
                    <button
                      className="button button-tertiary"
                      disabled={readMutation.isPending}
                      onClick={() =>
                        readMutation.mutate({ item, isRead: !item.isRead })
                      }
                    >
                      {item.isRead ? "Mark unread" : "Mark reviewed"}
                    </button>
                  </div>
                </article>
              );
            })}
          </section>
          <div className="table-footer">
            <span>
              {meta.total.toLocaleString()} attention item
              {meta.total === 1 ? "" : "s"}
            </span>
            <div>
              <button
                className="button button-secondary"
                disabled={page <= 1}
                onClick={() => movePage(page - 1)}
              >
                Previous
              </button>
              <button
                className="button button-secondary"
                disabled={page * meta.pageSize >= meta.total}
                onClick={() => movePage(page + 1)}
              >
                Next
              </button>
            </div>
          </div>
        </>
      ) : (
        <div className="state-panel">
          <InboxOutlined />
          <h2>No matching work</h2>
          <p>
            There are no due activities or open cases in this view. This is not
            a health assessment; it only reflects the records currently
            stored.
          </p>
        </div>
      )}
      {readMutation.isError && (
        <div className="inline-error" role="alert">
          {readMutation.error.message}
        </div>
      )}
    </div>
  );
}

function formatDateTime(value) {
  if (!value) return "date not recorded";
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
}
