import { useQuery } from "@tanstack/react-query";
import {
  AddOutlined,
  BusinessOutlined,
  FilterListOutlined,
  RefreshOutlined,
  SearchOutlined,
} from "@mui/icons-material";
import { customerApi } from "../../services/api";

export default function CustomersPage() {
  const customersQuery = useQuery({
    queryKey: ["customers"],
    queryFn: () => customerApi.list(),
  });
  return (
    <div className="page">
      <div className="page-heading">
        <div>
          <p className="eyebrow">Relationship records</p>
          <h1>Customers</h1>
          <p className="page-subtitle">
            Your customer portfolio, only need to act.
          </p>
        </div>
        <button className="button button-primary">
          <AddOutlined /> Create customer
        </button>
      </div>
      <div className="toolbar">
        <label className="search-field">
          <SearchOutlined />
          <input placeholder="Search customers" aria-label="Search customers" />
        </label>
        <button className="button button-secondary">
          <FilterListOutlined /> Filters
        </button>
        <button
          className="icon-button"
          onClick={() => customersQuery.refetch()}
          aria-label="Refresh customers"
        >
          <RefreshOutlined />
        </button>
      </div>
      {customersQuery.isLoading ? (
        <LoadingRows />
      ) : customersQuery.isError ? (
        <ErrorState onRetry={() => customersQuery.refetch()} />
      ) : (
        <EmptyState />
      )}
    </div>
  );
}

function LoadingRows() {
  return (
    <div className="table-skeleton">
      {[1, 2, 3, 4].map((row) => (
        <div className="skeleton-row" key={row}>
          <span />
          <span />
          <span />
          <span />
        </div>
      ))}
    </div>
  );
}
function ErrorState({ onRetry }) {
  return (
    <div className="state-panel error-state">
      <BusinessOutlined />
      <h2>Customers are unavailable</h2>
      <p>
        The customer service could not be reached. No local or placeholder
        records are shown.
      </p>
      <button className="button button-secondary" onClick={onRetry}>
        <RefreshOutlined /> Try again
      </button>
    </div>
  );
}
function EmptyState() {
  return (
    <div className="state-panel">
      <BusinessOutlined />
      <h2>No customers yet</h2>
      <p>
        Create a customer when the Organizations and Customers API is available.
        This list will become the source for your relationship workspace.
      </p>
      <button className="button button-primary">
        <AddOutlined /> Create customer
      </button>
    </div>
  );
}
