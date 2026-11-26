import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  AddOutlined,
  DeleteOutline,
  EditOutlined,
  RefreshOutlined,
  SearchOutlined,
} from "@mui/icons-material";
import { useSearchParams } from "react-router-dom";
import { coreApi } from "../../services/api";

const resources = {
  organizations: {
    title: "Organizations",
    eyebrow: "Organization records",
    fields: [
      ["name", "Name", "text", true],
      ["industry", "Industry", "text"],
      ["registrationNumber", "Registration number", "text"],
    ],
    columns: [
      ["name", "Name"],
      ["industry", "Industry"],
      ["registrationNumber", "Registration"],
    ],
    initial: { name: "", industry: "", registrationNumber: "" },
  },
  contacts: {
    title: "Contacts",
    eyebrow: "Relationship people",
    fields: [
      ["firstName", "First name", "text", true],
      ["lastName", "Last name", "text", true],
      ["email", "Email", "email"],
      ["phone", "Phone", "text"],
      ["role", "Role", "text"],
    ],
    columns: [
      ["firstName", "First name"],
      ["lastName", "Last name"],
      ["email", "Email"],
      ["role", "Role"],
    ],
    initial: { firstName: "", lastName: "", email: "", phone: "", role: "" },
  },
  leads: {
    title: "Leads",
    eyebrow: "Lead management",
    fields: [
      ["firstName", "First name", "text", true],
      ["lastName", "Last name", "text", true],
      ["companyName", "Company", "text"],
      ["email", "Email", "email"],
      ["source", "Source", "text", true],
      ["estimatedValue", "Estimated value", "number"],
    ],
    columns: [
      ["leadNumber", "Lead"],
      ["firstName", "First name"],
      ["lastName", "Last name"],
      ["companyName", "Company"],
      ["status", "Status"],
    ],
    initial: {
      firstName: "",
      lastName: "",
      companyName: "",
      email: "",
      source: "",
      estimatedValue: "",
    },
  },
  opportunities: {
    title: "Opportunities",
    eyebrow: "Commercial pipeline",
    fields: [
      ["name", "Name", "text", true],
      ["stage", "Stage", "text", true],
      ["amount", "Amount", "number", true],
      ["currency", "Currency", "text", true],
      ["probability", "Probability", "number", true],
    ],
    columns: [
      ["opportunityNumber", "Opportunity"],
      ["name", "Name"],
      ["stage", "Stage"],
      ["amount", "Amount"],
      ["probability", "Probability"],
    ],
    initial: {
      name: "",
      stage: "QUALIFICATION",
      amount: 0,
      currency: "USD",
      probability: 0,
    },
  },
  activities: {
    title: "Activities",
    eyebrow: "Customer activity",
    fields: [
      ["type", "Type", "text", true],
      ["subject", "Subject", "text", true],
      ["description", "Description", "text"],
      ["scheduledAt", "Scheduled at", "datetime-local"],
      ["customerId", "Customer ID", "text"],
    ],
    columns: [
      ["type", "Type"],
      ["subject", "Subject"],
      ["status", "Status"],
      ["scheduledAt", "Scheduled"],
    ],
    initial: { type: "TASK", subject: "", description: "", scheduledAt: "", customerId: "" },
  },
  cases: {
    title: "Cases",
    eyebrow: "Customer service",
    fields: [
      ["subject", "Subject", "text", true],
      ["description", "Description", "text"],
      ["priority", "Priority", "text", true],
    ],
    columns: [
      ["caseNumber", "Case"],
      ["subject", "Subject"],
      ["priority", "Priority"],
      ["status", "Status"],
    ],
    initial: { subject: "", description: "", priority: "NORMAL" },
  },
  "saved-views": {
    title: "Saved views",
    eyebrow: "Personal workspace",
    fields: [
      ["name", "Name", "text", true],
      ["resourceType", "Resource type", "text", true],
      ["configurationJson", "Configuration JSON", "text", true],
    ],
    columns: [
      ["name", "Name"],
      ["resourceType", "Resource"],
      ["shared", "Shared"],
    ],
    initial: { name: "", resourceType: "customers", configurationJson: "{}" },
  },
};

export default function CoreResourcePage({ resource }) {
  const config = resources[resource];
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();
  const [search, setSearch] = useState(searchParams.get("search") || "");
  const [page, setPage] = useState(Math.max(1, Number(searchParams.get("page") || "1")));
  const [editing, setEditing] = useState(null);
  useEffect(() => {
    setSearch(searchParams.get("search") || "");
    setPage(Math.max(1, Number(searchParams.get("page") || "1")));
    if (searchParams.get("create") === "1") {
      const initial = { ...config.initial };
      if (resource === "activities" && searchParams.has("customerId")) {
        initial.customerId = searchParams.get("customerId");
      }
      setEditing(initial);
      const next = new URLSearchParams(searchParams);
      next.delete("create");
      next.delete("customerId");
      setSearchParams(next, { replace: true });
    }
  }, [config.initial, resource, searchParams, setSearchParams]);
  const query = useQuery({
    queryKey: [resource, search, page],
    queryFn: () =>
      coreApi.list(
        resource,
        new URLSearchParams({ search, page, pageSize: 25 }).toString(),
      ),
  });
  const mutation = useMutation({
    mutationFn: ({ id, payload, version }) => {
      const requestPayload = { ...payload };
      if (resource === "activities") {
        if (!requestPayload.customerId) delete requestPayload.customerId;
        if (requestPayload.scheduledAt) {
          requestPayload.scheduledAt = new Date(
            requestPayload.scheduledAt,
          ).toISOString();
        } else {
          requestPayload.scheduledAt = null;
        }
      }
      return id
        ? coreApi.update(
            resource,
            id,
            { ...requestPayload, expectedVersion: version },
            version,
          )
        : coreApi.create(resource, requestPayload);
    },
    onSuccess: () => {
      setEditing(null);
      queryClient.invalidateQueries({ queryKey: [resource] });
    },
  });
  const deleteMutation = useMutation({
    mutationFn: (id) => coreApi.delete(resource, id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [resource] }),
  });
  const rows = query.data?.data || [];
  return (
    <div className="page">
      <div className="page-heading">
        <div>
          <p className="eyebrow">{config.eyebrow}</p>
          <h1>{config.title}</h1>
          <p className="page-subtitle">
            Authorized records for your workspace.
          </p>
        </div>
        <button
          className="button button-primary"
          onClick={() => setEditing({ ...config.initial })}
        >
          <AddOutlined /> Create {config.title.slice(0, -1)}
        </button>
      </div>
      <div className="toolbar">
        <label className="search-field">
          <SearchOutlined />
          <input
            value={search}
            onChange={(event) => {
              setSearch(event.target.value);
              setPage(1);
            }}
            placeholder={`Search ${config.title.toLowerCase()}`}
          />
        </label>
        <button
          className="icon-button"
          onClick={() => query.refetch()}
          aria-label={`Refresh ${config.title}`}
        >
          <RefreshOutlined />
        </button>
      </div>
      {query.isLoading ? (
        <div className="table-skeleton">
          <div className="skeleton-row" />
        </div>
      ) : query.isError ? (
        <div className="state-panel error-state">
          <h2>{config.title} are unavailable</h2>
          <p>{query.error.message}</p>
          <button
            className="button button-secondary"
            onClick={() => query.refetch()}
          >
            <RefreshOutlined /> Try again
          </button>
        </div>
      ) : rows.length ? (
        <div className="customer-table-wrap">
          <table className="customer-table">
            <thead>
              <tr>
                {config.columns.map(([, label]) => (
                  <th key={label}>{label}</th>
                ))}
                <th />
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.id}>
                  {config.columns.map(([key]) => (
                    <td key={key}>
                      {key === "amount" && row[key] !== undefined
                        ? `${row[key]} ${row.currency || ""}`
                        : String(row[key] ?? "—")}
                    </td>
                  ))}
                  <td>
                    <button
                      className="icon-button"
                      onClick={() => setEditing({ ...row })}
                      aria-label={`Edit ${config.title.slice(0, -1)}`}
                    >
                      <EditOutlined />
                    </button>
                    <button
                      className="icon-button danger-icon"
                      onClick={() =>
                        window.confirm(
                          "Delete this record? This action is audited.",
                        ) && deleteMutation.mutate(row.id)
                      }
                      aria-label={`Delete ${config.title.slice(0, -1)}`}
                    >
                      ×
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="table-footer">
            <span>{query.data?.meta?.total ?? rows.length} records</span>
            <div>
              <button
                className="button button-secondary"
                disabled={page <= 1}
                onClick={() => setPage(page - 1)}
              >
                Previous
              </button>
              <button
                className="button button-secondary"
                disabled={rows.length < 25}
                onClick={() => setPage(page + 1)}
              >
                Next
              </button>
            </div>
          </div>
        </div>
      ) : (
        <div className="state-panel">
          <h2>No {config.title.toLowerCase()} yet</h2>
          <p>
            Create a record to begin tracking this part of the customer
            relationship.
          </p>
          <button
            className="button button-primary"
            onClick={() => setEditing({ ...config.initial })}
          >
            <AddOutlined /> Create record
          </button>
        </div>
      )}
      {editing && (
        <ResourceForm
          config={config}
          value={editing}
          onClose={() => setEditing(null)}
          onSubmit={(payload) =>
            mutation.mutate({
              id: editing.id,
              version: editing.version,
              payload,
            })
          }
          isSubmitting={mutation.isPending}
          error={mutation.error}
        />
      )}
    </div>
  );
}

function ResourceForm({
  config,
  value,
  onClose,
  onSubmit,
  isSubmitting,
  error,
}) {
  const [form, setForm] = useState(() => ({
    ...value,
    ...(value.scheduledAt
      ? { scheduledAt: toLocalDateTimeInput(value.scheduledAt) }
      : {}),
  }));
  const update = (event) =>
    setForm((current) => ({
      ...current,
      [event.target.name]:
        event.target.type === "number"
          ? Number(event.target.value)
          : event.target.value,
    }));
  return (
    <div className="drawer-layer">
      <button
        className="drawer-scrim"
        type="button"
        onClick={onClose}
        aria-label="Close form"
      />
      <aside className="form-drawer" role="dialog" aria-modal="true">
        <div className="drawer-header">
          <div>
            <p className="eyebrow">{value.id ? "Edit record" : "New record"}</p>
            <h2>
              {value.id
                ? `Edit ${config.title.slice(0, -1)}`
                : `Create ${config.title.slice(0, -1)}`}
            </h2>
          </div>
          <button className="icon-button" onClick={onClose} aria-label="Close">
            ×
          </button>
        </div>
        <form
          className="record-form"
          onSubmit={(event) => {
            event.preventDefault();
            onSubmit(form);
          }}
        >
          {config.fields.map(([name, label, type, required]) => (
            <label key={name}>
              {label}
              {required && <span aria-hidden="true">*</span>}
              <input
                name={name}
                type={type}
                required={required}
                value={form[name] ?? ""}
                onChange={update}
              />
            </label>
          ))}
          {error && (
            <div className="inline-error" role="alert">
              {error.message}
            </div>
          )}
          <div className="drawer-actions">
            <button
              className="button button-secondary"
              type="button"
              onClick={onClose}
            >
              Cancel
            </button>
            <button
              className="button button-primary"
              type="submit"
              disabled={isSubmitting}
            >
              {isSubmitting ? "Saving..." : "Save record"}
            </button>
          </div>
        </form>
      </aside>
    </div>
  );
}

function toLocalDateTimeInput(value) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
  return date.toISOString().slice(0, 16);
}
