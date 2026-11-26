import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  AccessTimeOutlined,
  AddOutlined,
  AssignmentOutlined,
  BusinessOutlined,
  ChevronLeftOutlined,
  ChevronRightOutlined,
  CircleOutlined,
  DashboardOutlined,
  GroupsOutlined,
  InboxOutlined,
  InsightsOutlined,
  MenuOutlined,
  NotificationsNoneOutlined,
  SearchOutlined,
  SettingsOutlined,
  SupportAgentOutlined,
} from "@mui/icons-material";
import { Link, NavLink, useLocation, useNavigate } from "react-router-dom";
import { notificationApi, workspaceApi } from "../services/api";

const primaryNavigation = [
  { label: "Home", to: "/", icon: DashboardOutlined },
  { label: "Customers", to: "/customers", icon: BusinessOutlined },
  { label: "Inbox", to: "/inbox", icon: InboxOutlined },
  { label: "Organizations", to: "/organizations", icon: BusinessOutlined },
  { label: "Contacts", to: "/contacts", icon: GroupsOutlined },
  { label: "Leads", to: "/leads", icon: GroupsOutlined },
  { label: "Opportunities", to: "/opportunities", icon: InsightsOutlined },
  { label: "Activities", to: "/activities", icon: AssignmentOutlined },
  { label: "Cases", to: "/cases", icon: SupportAgentOutlined },
];

const secondaryNavigation = [
  { label: "Intelligence", to: "/intelligence", icon: InsightsOutlined },
  { label: "Administration", to: "/administration", icon: SettingsOutlined },
];

const createOptions = [
  { label: "Customer", path: "/customers" },
  { label: "Contact", path: "/contacts" },
  { label: "Lead", path: "/leads" },
  { label: "Opportunity", path: "/opportunities" },
  { label: "Activity", path: "/activities" },
  { label: "Case", path: "/cases" },
];

export default function AppShell({ children }) {
  const [collapsed, setCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);
  const [searchText, setSearchText] = useState("");
  const [searchTerm, setSearchTerm] = useState("");
  const [createOpen, setCreateOpen] = useState(false);
  const [notificationsOpen, setNotificationsOpen] = useState(false);
  const location = useLocation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const currentLabel =
    [...primaryNavigation, ...secondaryNavigation].find(
      (item) => item.to === location.pathname,
    )?.label || "Workspace";

  const notificationQuery = useQuery({
    queryKey: ["notifications"],
    queryFn: notificationApi.list,
    enabled: location.pathname !== "/login",
    staleTime: 30_000,
  });
  const searchQuery = useQuery({
    queryKey: ["global-search", searchTerm],
    queryFn: () => workspaceApi.search(searchTerm),
    enabled:
      location.pathname !== "/login" &&
      searchOpen &&
      searchTerm.length >= 2,
  });
  const markReadMutation = useMutation({
    mutationFn: ({ item, isRead }) =>
      isRead
        ? notificationApi.markRead(item.entityType, item.entityId)
        : notificationApi.markUnread(item.entityType, item.entityId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["notifications"] });
      queryClient.invalidateQueries({ queryKey: ["inbox"] });
    },
  });
  const notificationFeed = notificationQuery.data?.data;
  const unreadCount = notificationFeed?.unreadCount || 0;
  const searchResults = searchQuery.data?.data || [];
  const notifications = notificationFeed?.data || [];

  useEffect(() => {
    const timer = window.setTimeout(
      () => setSearchTerm(searchText.trim()),
      250,
    );
    return () => window.clearTimeout(timer);
  }, [searchText]);

  useEffect(() => {
    function handleKeyDown(event) {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        setSearchOpen(true);
        setCreateOpen(false);
        setNotificationsOpen(false);
      }
      if (event.key === "Escape") {
        setSearchOpen(false);
        setCreateOpen(false);
        setNotificationsOpen(false);
      }
    }
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, []);

  useEffect(() => {
    setMobileOpen(false);
    setCreateOpen(false);
    setNotificationsOpen(false);
  }, [location.pathname]);

  function openSearch() {
    setSearchText("");
    setSearchTerm("");
    setSearchOpen(true);
    setCreateOpen(false);
    setNotificationsOpen(false);
  }

  function openResult(hit) {
    setSearchOpen(false);
    if (hit.entityType.toLowerCase() === "customer") {
      navigate(`/customers/${hit.id}`);
      return;
    }
    navigate(`${resourcePath(hit.entityType)}?search=${encodeURIComponent(hit.title)}`);
  }

  function openNotification(item) {
    if (!item.isRead) markReadMutation.mutate({ item, isRead: true });
    setNotificationsOpen(false);
    navigate(
      `${resourcePath(item.entityType)}?search=${encodeURIComponent(item.title)}`,
    );
  }

  return (
    <div className={`app-shell ${collapsed ? "is-collapsed" : ""}`}>
      <aside className={`sidebar ${mobileOpen ? "is-mobile-open" : ""}`}>
        <div className="brand-row">
          <div className="brand-mark" aria-hidden="true">
            <CircleOutlined />
          </div>
          <div className="brand-copy">
            <strong>CURE</strong>
            <span>The World Class CRM</span>
          </div>
          <button
            className="icon-button sidebar-close"
            type="button"
            onClick={() => setMobileOpen(false)}
            aria-label="Close navigation"
          >
            <ChevronLeftOutlined />
          </button>
        </div>
        <div className="create-menu-wrap">
          <button
            className="create-button"
            type="button"
            title="Create a record"
            aria-expanded={createOpen}
            aria-haspopup="menu"
            onClick={() => {
              setCreateOpen((open) => !open);
              setNotificationsOpen(false);
            }}
          >
            <AddOutlined />
            <span>Create record</span>
          </button>
          {createOpen && (
            <div className="create-menu" role="menu" aria-label="Create a record">
              <p className="eyebrow">Create new</p>
              {createOptions.map((option) => (
                <button
                  key={option.path}
                  role="menuitem"
                  type="button"
                  onClick={() => {
                    setCreateOpen(false);
                    navigate(`${option.path}?create=1`);
                  }}
                >
                  <AddOutlined /> {option.label}
                </button>
              ))}
            </div>
          )}
        </div>
        <nav className="primary-nav" aria-label="Primary navigation">
          <p className="nav-label">Workspace</p>
          {primaryNavigation.map((item) => (
            <NavigationItem
              key={item.label}
              item={item}
              badge={item.to === "/inbox" ? unreadCount : undefined}
            />
          ))}
          <p className="nav-label nav-label-lower">Manage</p>
          {secondaryNavigation.map((item) => (
            <NavigationItem key={item.label} item={item} />
          ))}
        </nav>
        <div className="sidebar-footer">
          <div className="workspace-switcher">
            <span className="workspace-avatar">CI</span>
            <span className="workspace-name">
              <strong>CURE Industries</strong>
              <small>workspace</small>
            </span>
            <ChevronRightOutlined />
          </div>
          <button
            className="collapse-button"
            type="button"
            onClick={() => setCollapsed((value) => !value)}
            aria-label={collapsed ? "Expand sidebar" : "Collapse sidebar"}
          >
            {collapsed ? <ChevronRightOutlined /> : <ChevronLeftOutlined />}
            <span>{collapsed ? "Expand" : "Collapse"} sidebar</span>
          </button>
        </div>
      </aside>

      <div className="shell-content">
        <header className="topbar">
          <div className="topbar-context">
            <button
              className="icon-button mobile-menu"
              type="button"
              onClick={() => setMobileOpen(true)}
              aria-label="Open navigation"
            >
              <MenuOutlined />
            </button>
            <span>{currentLabel}</span>
          </div>
          <div className="topbar-actions">
            <button
              className="global-search"
              type="button"
              onClick={openSearch}
              aria-label="Search anything"
            >
              <SearchOutlined />
              <span>Search anything</span>
              <kbd>Ctrl K</kbd>
            </button>
            <div className="notification-menu-wrap">
              <button
                className="icon-button"
                type="button"
                aria-label={`View notifications${unreadCount ? `, ${unreadCount} unread` : ""}`}
                aria-expanded={notificationsOpen}
                onClick={() => {
                  setNotificationsOpen((open) => !open);
                  setCreateOpen(false);
                }}
              >
                <NotificationsNoneOutlined />
                {unreadCount > 0 && <i className="notification-dot" />}
              </button>
              {notificationsOpen && (
                <section
                  className="notification-popover"
                  aria-label="Notifications"
                >
                  <div className="popover-heading">
                    <div>
                      <p className="eyebrow">Recorded attention</p>
                      <h2>Notifications</h2>
                    </div>
                    <span>{unreadCount} unread</span>
                  </div>
                  {notificationQuery.isLoading ? (
                    <p className="popover-state">Loading notifications…</p>
                  ) : notificationQuery.isError ? (
                    <div className="popover-state error-state" role="alert">
                      <p>{notificationQuery.error.message}</p>
                      <button
                        className="inline-link"
                        onClick={() => notificationQuery.refetch()}
                      >
                        Try again
                      </button>
                    </div>
                  ) : notifications.length ? (
                    <div className="notification-list">
                      {notifications.map((item) => (
                        <article
                          className={`notification-item ${item.isRead ? "is-read" : ""}`}
                          key={`${item.entityType}:${item.entityId}`}
                        >
                          <button
                            className="notification-copy"
                            type="button"
                            onClick={() => openNotification(item)}
                          >
                            <strong>{item.title}</strong>
                            <span>
                              {item.category}
                              {item.customerName ? ` · ${item.customerName}` : ""}
                            </span>
                          </button>
                          <button
                            className="notification-read-toggle"
                            type="button"
                            disabled={markReadMutation.isPending}
                            onClick={() =>
                              markReadMutation.mutate({
                                item,
                                isRead: !item.isRead,
                              })
                            }
                          >
                            {item.isRead ? "Unread" : "Read"}
                          </button>
                        </article>
                      ))}
                    </div>
                  ) : (
                    <p className="popover-state">
                      No due activities or open cases are currently recorded.
                    </p>
                  )}
                  {markReadMutation.isError && (
                    <p className="popover-error" role="alert">
                      {markReadMutation.error.message}
                    </p>
                  )}
                  <Link
                    className="popover-footer"
                    to="/inbox"
                    onClick={() => setNotificationsOpen(false)}
                  >
                    Open attention inbox <ChevronRightOutlined />
                  </Link>
                </section>
              )}
            </div>
            <button
              className="icon-button"
              type="button"
              aria-label="Help and support is not connected"
              title="Help and support is not connected"
              disabled
            >
              <AccessTimeOutlined />
            </button>
            <button className="user-menu" type="button" aria-label="Current user">
              <span className="user-avatar">JD</span>
              <span className="user-details">
                <strong>Jordan Davis</strong>
                <small>Administrator</small>
              </span>
              <ChevronRightOutlined />
            </button>
          </div>
        </header>
        <main className="main-content">{children}</main>
      </div>
      {mobileOpen && (
        <button
          className="mobile-scrim"
          type="button"
          onClick={() => setMobileOpen(false)}
          aria-label="Close navigation"
        />
      )}
      {searchOpen && (
        <div
          className="search-layer"
          role="presentation"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) setSearchOpen(false);
          }}
        >
          <section
            className="search-dialog"
            role="dialog"
            aria-modal="true"
            aria-labelledby="global-search-title"
          >
            <div className="search-dialog-input">
              <SearchOutlined />
              <input
                autoFocus
                value={searchText}
                onChange={(event) => setSearchText(event.target.value)}
                placeholder="Search customers, contacts, activities…"
                aria-label="Search workspace"
              />
              <kbd>ESC</kbd>
            </div>
            <div className="search-dialog-results">
              <p className="eyebrow" id="global-search-title">
                {searchText.trim().length < 2
                  ? "Search by name, email, or record"
                  : "Workspace results"}
              </p>
              {searchText.trim().length < 2 ? (
                <p className="popover-state">
                  Enter at least 2 characters to search your workspace.
                </p>
              ) : searchQuery.isLoading ? (
                <p className="popover-state">Searching workspace records…</p>
              ) : searchQuery.isError ? (
                <div className="popover-state error-state" role="alert">
                  {searchQuery.error.message}
                </div>
              ) : searchResults.length ? (
                <div className="global-search-results">
                  {searchResults.map((hit) => (
                    <button
                      className="global-search-result"
                      type="button"
                      key={`${hit.entityType}:${hit.id}`}
                      onClick={() => openResult(hit)}
                    >
                      <span>
                        <strong>{hit.title}</strong>
                        <small>
                          {hit.entityType}
                          {hit.detail ? ` · ${hit.detail}` : ""}
                        </small>
                      </span>
                      <ChevronRightOutlined />
                    </button>
                  ))}
                </div>
              ) : (
                <p className="popover-state">
                  No matching records found. Check the spelling or try another
                  identifier.
                </p>
              )}
            </div>
          </section>
        </div>
      )}
    </div>
  );
}

function NavigationItem({ item, badge }) {
  const Icon = item.icon;
  return (
    <NavLink
      className={({ isActive }) => `nav-item ${isActive ? "active" : ""}`}
      to={item.to}
    >
      <Icon />
      <span>{item.label}</span>
      {badge > 0 && <b>{badge > 99 ? "99+" : badge}</b>}
    </NavLink>
  );
}

function resourcePath(entityType) {
  const value = entityType.toLowerCase();
  if (value === "customer") return "/customers";
  if (value === "organization") return "/organizations";
  if (value === "contact") return "/contacts";
  if (value === "lead") return "/leads";
  if (value === "opportunity") return "/opportunities";
  if (value === "case") return "/cases";
  return "/activities";
}
