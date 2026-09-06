import { useState } from "react";
import { NavLink, useLocation } from "react-router-dom";
import {
  AccessTimeOutlined,
  AddOutlined,
  AppsOutlined,
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

const primaryNavigation = [
  { label: "Home", to: "/", icon: DashboardOutlined },
  { label: "Customers", to: "/customers", icon: BusinessOutlined },
  { label: "Inbox", to: "/inbox", icon: InboxOutlined, badge: "—" },
  { label: "Leads", to: "/leads", icon: GroupsOutlined, soon: true },
  {
    label: "Opportunities",
    to: "/opportunities",
    icon: InsightsOutlined,
    soon: true,
  },
  {
    label: "Activities",
    to: "/activities",
    icon: AssignmentOutlined,
    soon: true,
  },
  { label: "Cases", to: "/cases", icon: SupportAgentOutlined, soon: true },
];

const secondaryNavigation = [
  { label: "Intelligence", to: "/intelligence", icon: InsightsOutlined },
  { label: "Administration", to: "/administration", icon: SettingsOutlined },
];

export default function AppShell({ children }) {
  const [collapsed, setCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const location = useLocation();
  const currentLabel =
    [...primaryNavigation, ...secondaryNavigation].find(
      (item) => item.to === location.pathname,
    )?.label || "Workspace";

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
        <button className="create-button" type="button" title="Create a record">
          <AddOutlined />
          <span>Create record</span>
        </button>
        <nav className="primary-nav" aria-label="Primary navigation">
          <p className="nav-label">Workspace</p>
          {primaryNavigation.map((item) => (
            <NavigationItem key={item.label} item={item} />
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
            <button className="global-search" type="button">
              <SearchOutlined />
              <span>Search anything</span>
              <kbd>Ctrl K</kbd>
            </button>
            <button
              className="icon-button"
              type="button"
              aria-label="View notifications"
            >
              <NotificationsNoneOutlined />
              <i className="notification-dot" />
            </button>
            <button
              className="icon-button"
              type="button"
              aria-label="View help"
            >
              <AccessTimeOutlined />
            </button>
            <button className="user-menu" type="button">
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
    </div>
  );
}

function NavigationItem({ item }) {
  const Icon = item.icon;
  return (
    <NavLink
      className={({ isActive }) =>
        `nav-item ${isActive ? "active" : ""} ${item.soon ? "disabled-nav" : ""}`
      }
      to={item.soon ? "#" : item.to}
      onClick={(event) => item.soon && event.preventDefault()}
      aria-disabled={item.soon}
    >
      <Icon />
      <span>{item.label}</span>
      {item.badge && <b>{item.badge}</b>}
      {item.soon && <small>Phase 2</small>}
    </NavLink>
  );
}
