import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  AddOutlined,
  ArrowBackOutlined,
  AssignmentOutlined,
  BusinessOutlined,
  CloseOutlined,
  EditOutlined,
  RefreshOutlined,
  SupportAgentOutlined,
} from "@mui/icons-material";
import { Link, useNavigate, useParams } from "react-router-dom";
import { coreApi, customerApi } from "../../services/api";
import CustomerForm from "./CustomerForm";

const tabs = [
  { id: "overview", label: "Overview" },
  { id: "timeline", label: "Timeline" },
  { id: "contacts", label: "Contacts" },
  { id: "opportunities", label: "Opportunities" },
  { id: "cases", label: "Cases" },
  { id: "activities", label: "Activities" },
];

export default function CustomerDetailPage() {
  const { customerId } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [activeTab, setActiveTab] = useState("overview");
  const [contactToLink, setContactToLink] = useState("");
  const [contactSearch, setContactSearch] = useState("");

  const customerQuery = useQuery({
    queryKey: ["customer360", customerId],
    queryFn: () => customerApi.get360(customerId),
  });
  const view = customerQuery.data?.data;
  const customer = view?.customer;

  const updateMutation = useMutation({
    mutationFn: (payload) =>
      customerApi.update(customerId, payload, customer?.version),
    onSuccess: () => {
      setEditing(false);
      queryClient.invalidateQueries({ queryKey: ["customer360", customerId] });
      queryClient.invalidateQueries({ queryKey: ["customers"] });
    },
  });
  const contactsQuery = useQuery({
    queryKey: ["available-contacts", contactSearch],
    queryFn: () => coreApi.list("contacts", new URLSearchParams({
      search: contactSearch,
      page: "1",
      pageSize: "100",
    }).toString()),
    enabled: activeTab === "contacts",
  });
  const linkMutation = useMutation({
    mutationFn: (contactId) => customerApi.linkContact(customerId, contactId),
    onSuccess: () => {
      setContactToLink("");
      setContactSearch("");
      queryClient.invalidateQueries({ queryKey: ["customer360", customerId] });
    },
  });
  const unlinkMutation = useMutation({
    mutationFn: (contactId) => customerApi.unlinkContact(customerId, contactId),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ["customer360", customerId] }),
  });

  if (customerQuery.isLoading) {
    return (
      <div className="page">
        <div className="table-skeleton">
          <div className="skeleton-detail" />
          <div className="skeleton-detail short" />
        </div>
      </div>
    );
  }

  if (customerQuery.isError) {
    return (
      <div className="page">
        <Link className="back-link" to="/customers">
          <ArrowBackOutlined /> Customers
        </Link>
        <div className="state-panel error-state" role="alert">
          <BusinessOutlined />
          <h1>Customer unavailable</h1>
          <p>{customerQuery.error.message}</p>
          <button
            className="button button-secondary"
            onClick={() => customerQuery.refetch()}
          >
            <RefreshOutlined /> Try again
          </button>
        </div>
      </div>
    );
  }

  const availableContacts = (contactsQuery.data?.data || []).filter(
    (contact) => !view.contacts.some((linked) => linked.id === contact.id),
  );
  return (
    <div className="page">
      <Link className="back-link" to="/customers">
        <ArrowBackOutlined /> Customers
      </Link>
      <div className="customer-detail-heading">
        <div className="customer-identity">
          <span className="customer-logo">
            <BusinessOutlined />
          </span>
          <div>
            <p className="eyebrow">Customer</p>
            <h1>{customer.name}</h1>
            <p className="page-subtitle">
              {customer.type}
              {customer.industry ? ` · ${customer.industry}` : ""}
            </p>
          </div>
        </div>
        <div className="customer-detail-actions">
          <button
            className="button button-secondary"
            onClick={() =>
              navigate(`/activities?create=1&customerId=${customer.id}`)
            }
          >
            <AddOutlined /> Add activity
          </button>
          <button
            className="button button-secondary"
            onClick={() => setEditing(true)}
          >
            <EditOutlined /> Edit customer
          </button>
        </div>
      </div>

      <section className="customer-metrics" aria-label="Customer record summary">
        <Metric label="Contacts" value={view.metrics.contacts} detail="Linked records" />
        <Metric label="Opportunities" value={view.metrics.opportunities} detail="Linked records" />
        <Metric label="Open cases" value={view.metrics.openCases} detail={`${view.metrics.activities} activity records`} />
        <Metric label="Relationship health" value="Not computed" detail="No connected assessment" />
      </section>

      <div className="detail-tabs" role="tablist" aria-label="Customer sections">
        {tabs.map((tab) => (
          <button
            className={activeTab === tab.id ? "active" : ""}
            id={`customer-tab-${tab.id}`}
            key={tab.id}
            type="button"
            role="tab"
            aria-selected={activeTab === tab.id}
            aria-controls="customer-tab-panel"
            onClick={() => setActiveTab(tab.id)}
          >
            {tab.label}
          </button>
        ))}
      </div>

      <section
        className="panel detail-panel"
        id="customer-tab-panel"
        role="tabpanel"
        aria-labelledby={`customer-tab-${activeTab}`}
        tabIndex={0}
      >
        {activeTab === "overview" && <Overview view={view} customer={customer} />}
        {activeTab === "timeline" && <Timeline entries={view.timeline} />}
        {activeTab === "contacts" && (
          <Contacts
            contacts={view.contacts}
            total={view.metrics.contacts}
            availableContacts={availableContacts}
            isLoading={contactsQuery.isLoading}
            isError={contactsQuery.isError}
            selectedContact={contactToLink}
            contactSearch={contactSearch}
            onSearchContacts={setContactSearch}
            onSelectContact={setContactToLink}
            onLink={() => linkMutation.mutate(contactToLink)}
            onUnlink={(contactId) => unlinkMutation.mutate(contactId)}
            isSubmitting={linkMutation.isPending || unlinkMutation.isPending}
          />
        )}
        {activeTab === "opportunities" && (
          <Opportunities items={view.opportunities} total={view.metrics.opportunities} />
        )}
        {activeTab === "cases" && <Cases items={view.cases} total={view.metrics.cases} />}
        {activeTab === "activities" && (
          <Activities items={view.activities} total={view.metrics.activities} />
        )}
      </section>

      {(updateMutation.isError ||
        linkMutation.isError ||
        unlinkMutation.isError ||
        contactsQuery.isError) && (
        <div className="inline-error" role="alert">
          {updateMutation.error?.message ||
            linkMutation.error?.message ||
            unlinkMutation.error?.message ||
            contactsQuery.error?.message}
        </div>
      )}

      {editing && (
        <CustomerForm
          customer={customer}
          onClose={() => setEditing(false)}
          onSubmit={(payload) => updateMutation.mutate(payload)}
          isSubmitting={updateMutation.isPending}
        />
      )}
    </div>
  );
}

function Overview({ view, customer }) {
  return (
    <>
      <p className="eyebrow">Relationship context</p>
      <h2>{customer.name}</h2>
      <p className="panel-intro">
        This view combines linked records and recorded audit changes. Health,
        financial, and signal assessments are not connected yet.
      </p>
      <div className="customer-context-grid">
        <ContextValue label="Customer type" value={customer.type} />
        <ContextValue label="Industry" value={customer.industry || "Not recorded"} />
        <ContextValue label="Owner" value={customer.ownerId || "Unassigned"} />
        <ContextValue label="Created" value={formatDate(customer.createdAt)} />
        <ContextValue label="Updated" value={formatDate(customer.updatedAt)} />
        <ContextValue label="Email" value={customer.email || "Not recorded"} />
      </div>
      <div className="customer-data-availability">
        <h3>Not yet connected</h3>
        <p>
          Revenue, lifetime value, customer health, signals, and next-best
          actions will show only when their source data and calculations are
          available.
        </p>
        <p>
          Current linked records: {view.metrics.contacts} contacts,{" "}
          {view.metrics.opportunities} opportunities, {view.metrics.openCases}{" "}
          open cases, and {view.metrics.activities} activities.
        </p>
      </div>
    </>
  );
}

function Timeline({ entries }) {
  return (
    <>
      <p className="eyebrow">Audit-backed history</p>
      <h2>Timeline</h2>
      <p className="panel-intro">
        Events shown here come from recorded audit changes. This is not yet a
        complete communication or integration event stream.
      </p>
      {entries.length ? (
        <ol className="customer-timeline">
          {entries.map((entry) => (
            <li key={entry.id}>
              <span className="timeline-marker" />
              <div>
                <strong>{formatAction(entry.action)}</strong>
                <p>{formatEntityType(entry.entityType)}</p>
                <small>
                  {formatDateTime(entry.occurredAt)} · Actor {entry.actorId}
                </small>
              </div>
            </li>
          ))}
        </ol>
      ) : (
        <div className="empty-panel">
          <AssignmentOutlined />
          <p>No audit events have been recorded for this customer yet.</p>
        </div>
      )}
    </>
  );
}

function Contacts({
  contacts,
  total,
  availableContacts,
  isLoading,
  isError,
  selectedContact,
  contactSearch,
  onSearchContacts,
  onSelectContact,
  onLink,
  onUnlink,
  isSubmitting,
}) {
  return (
    <>
      <div className="panel-heading">
        <div>
          <p className="eyebrow">Relationship network</p>
          <h2>Contacts</h2>
        </div>
      </div>
      <form
        className="inline-create-form"
        onSubmit={(event) => {
          event.preventDefault();
          if (selectedContact) onLink();
        }}
      >
        <label>
          Search contacts to link
          <input
            type="search"
            value={contactSearch}
            onChange={(event) => onSearchContacts(event.target.value)}
            placeholder="Search by name or email"
            aria-label="Search contacts to link"
          />
        </label>
        <label>
          Contact
          <select
            value={selectedContact}
            onChange={(event) => onSelectContact(event.target.value)}
            disabled={isLoading || isError || isSubmitting}
          >
            <option value="">Choose a contact</option>
            {availableContacts.map((contact) => (
              <option value={contact.id} key={contact.id}>
                {contact.firstName} {contact.lastName}
                {contact.email ? ` · ${contact.email}` : ""}
              </option>
            ))}
          </select>
        </label>
        <button
          className="button button-primary"
          type="submit"
          disabled={!selectedContact || isSubmitting}
        >
          <AddOutlined /> Link contact
        </button>
      </form>
      {contacts.length ? (
        <>
          <div className="related-record-list">
            {contacts.map((contact) => (
              <article className="related-record" key={contact.id}>
                <span className="workspace-record-icon">
                  <BusinessOutlined />
                </span>
                <div>
                  <strong>
                    {contact.firstName} {contact.lastName}
                  </strong>
                  <p>
                    {[contact.role, contact.email, contact.phone]
                      .filter(Boolean)
                      .join(" · ") || "No contact details recorded"}
                  </p>
                </div>
                <button
                  className="icon-button danger-icon"
                  type="button"
                  aria-label={`Unlink ${contact.firstName} ${contact.lastName}`}
                  disabled={isSubmitting}
                  onClick={() => onUnlink(contact.id)}
                >
                  <CloseOutlined />
                </button>
              </article>
            ))}
          </div>
          {contacts.length < total && (
            <p className="panel-intro">
              Showing {contacts.length} of {total} linked contacts.
            </p>
          )}
        </>
      ) : (
        <div className="empty-panel">
          <BusinessOutlined />
          <p>No contacts are linked to this customer yet.</p>
        </div>
      )}
    </>
  );
}

function Opportunities({ items, total }) {
  return (
    <RelatedRecords
      eyebrow="Commercial"
      title="Opportunities"
      items={items}
      total={total}
      empty="No opportunities are linked to this customer."
      render={(item) => (
        <>
          <strong>{item.name}</strong>
          <p>
            {item.stage} · {formatMoney(item.amount, item.currency)}
            {item.expectedCloseDate
              ? ` · Expected close ${formatDate(item.expectedCloseDate)}`
              : ""}
          </p>
        </>
      )}
    />
  );
}

function Cases({ items, total }) {
  return (
    <RelatedRecords
      eyebrow="Customer service"
      title="Cases"
      items={items}
      total={total}
      empty="No cases are linked to this customer."
      render={(item) => (
        <>
          <strong>{item.subject}</strong>
          <p>
            {item.caseNumber} · {item.priority} · {item.status}
          </p>
        </>
      )}
    />
  );
}

function Activities({ items, total }) {
  return (
    <RelatedRecords
      eyebrow="Recorded work"
      title="Activities"
      items={items}
      total={total}
      empty="No activities are linked to this customer."
      render={(item) => (
        <>
          <strong>{item.subject}</strong>
          <p>
            {item.type} · {item.status}
            {item.scheduledAt ? ` · ${formatDateTime(item.scheduledAt)}` : ""}
          </p>
          {item.description && <p>{item.description}</p>}
        </>
      )}
    />
  );
}

function RelatedRecords({ eyebrow, title, items, total, empty, render }) {
  return (
    <>
      <p className="eyebrow">{eyebrow}</p>
      <h2>{title}</h2>
      {items.length ? (
        <>
          <div className="related-record-list">
            {items.map((item) => (
              <article className="related-record" key={item.id}>
                <span className="workspace-record-icon">
                  {title === "Cases" ? (
                    <SupportAgentOutlined />
                  ) : (
                    <AssignmentOutlined />
                  )}
                </span>
                <div>{render(item)}</div>
              </article>
            ))}
          </div>
          {items.length < total && (
            <p className="panel-intro">
              Showing {items.length} of {total} records. Additional record
              pagination is not available yet.
            </p>
          )}
        </>
      ) : (
        <div className="empty-panel">
          <AssignmentOutlined />
          <p>{empty}</p>
        </div>
      )}
    </>
  );
}

function Metric({ label, value, detail }) {
  return (
    <div className="metric">
      <span>{label}</span>
      <strong>{value}</strong>
      <small>{detail}</small>
    </div>
  );
}

function ContextValue({ label, value }) {
  return (
    <div className="customer-context-value">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function formatAction(value) {
  return value
    .toLowerCase()
    .split("_")
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(" ");
}

function formatEntityType(value) {
  return value.toLowerCase().replaceAll("_", " ");
}

function formatDate(value) {
  if (!value) return "Not recorded";
  return new Intl.DateTimeFormat(undefined, { dateStyle: "medium" }).format(
    new Date(value),
  );
}

function formatDateTime(value) {
  if (!value) return "Date not recorded";
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
}

function formatMoney(amount, currency) {
  return `${Number(amount).toLocaleString()} ${currency}`;
}
