import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AddOutlined, BusinessOutlined, FilterListOutlined, RefreshOutlined, SearchOutlined } from '@mui/icons-material';
import { Link, useSearchParams } from 'react-router-dom';
import { customerApi } from '../../services/api';
import CustomerForm from './CustomerForm';

export default function CustomersPage() {
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();
  const [search, setSearch] = useState(searchParams.get('search') || '');
  const [type, setType] = useState(searchParams.get('type') || '');
  const [industry, setIndustry] = useState(searchParams.get('industry') || '');
  const [page, setPage] = useState(Math.max(1, Number(searchParams.get('page') || '1')));
  const [formOpen, setFormOpen] = useState(false);
  const [filtersOpen, setFiltersOpen] = useState(false);
  useEffect(() => {
    setSearch(searchParams.get('search') || '');
    setType(searchParams.get('type') || '');
    setIndustry(searchParams.get('industry') || '');
    setPage(Math.max(1, Number(searchParams.get('page') || '1')));
    if (searchParams.get('create') === '1') {
      setFormOpen(true);
      const next = new URLSearchParams(searchParams);
      next.delete('create');
      setSearchParams(next, { replace: true });
    }
  }, [searchParams, setSearchParams]);
  const customersQuery = useQuery({
    queryKey: ['customers', search, type, industry, page],
    queryFn: () => customerApi.list(new URLSearchParams({
      search,
      type,
      industry,
      page: String(page),
      pageSize: '25',
    }).toString()),
  });
  const createMutation = useMutation({ mutationFn: customerApi.create, onSuccess: () => { setFormOpen(false); queryClient.invalidateQueries({ queryKey: ['customers'] }); } });
  const customers = customersQuery.data?.data || [];
  const hasFilters = Boolean(type || industry);

  return <div className="page">
    <div className="page-heading"><div><p className="eyebrow">Relationship records</p><h1>Customers</h1><p className="page-subtitle">Your customer portfolio, only need to act.</p></div><button className="button button-primary" onClick={() => setFormOpen(true)}><AddOutlined /> Create customer</button></div>
    <div className="toolbar"><label className="search-field"><SearchOutlined /><input value={search} onChange={(event) => { setSearch(event.target.value); setPage(1); }} placeholder="Search customers" aria-label="Search customers" /></label><button className="button button-secondary" aria-expanded={filtersOpen} onClick={() => setFiltersOpen((open) => !open)}><FilterListOutlined /> Filters{hasFilters ? ' · Active' : ''}</button><button className="icon-button" onClick={() => customersQuery.refetch()} aria-label="Refresh customers"><RefreshOutlined /></button></div>
    {filtersOpen && <div className="customer-filter-panel" aria-label="Customer filters">
      <label>Customer type<select value={type} onChange={(event) => { setType(event.target.value); setPage(1); }}><option value="">All types</option><option value="Business">Business</option><option value="Individual">Individual</option><option value="Government">Government</option><option value="Non-profit">Non-profit</option><option value="Partnership">Partnership</option><option value="Other">Other</option></select></label>
      <label>Industry<input value={industry} onChange={(event) => { setIndustry(event.target.value); setPage(1); }} placeholder="Match an industry" /></label>
      {hasFilters && <button className="inline-link" onClick={() => { setType(''); setIndustry(''); setPage(1); }}>Clear filters</button>}
    </div>}
    {customersQuery.isLoading ? <LoadingRows /> : customersQuery.isError ? <ErrorState onRetry={() => customersQuery.refetch()} /> : customers.length ? <CustomerTable customers={customers} meta={customersQuery.data?.meta} page={page} setPage={setPage} /> : <EmptyState onCreate={() => setFormOpen(true)} />}
    {createMutation.isError && <div className="inline-error" role="alert">{createMutation.error.message}</div>}
    {formOpen && <CustomerForm onClose={() => setFormOpen(false)} onSubmit={(values) => createMutation.mutate(values)} isSubmitting={createMutation.isPending} />}
  </div>;
}

function LoadingRows() { return <div className="table-skeleton">{[1, 2, 3, 4].map((row) => <div className="skeleton-row" key={row}><span /><span /><span /><span /></div>)}</div>; }
function ErrorState({ onRetry }) { return <div className="state-panel error-state"><BusinessOutlined /><h2>Customers are unavailable</h2><p>The customer service could not be reached. No local or placeholder records are shown.</p><button className="button button-secondary" onClick={onRetry}><RefreshOutlined /> Try again</button></div>; }
function EmptyState({ onCreate }) { return <div className="state-panel"><BusinessOutlined /><h2>No customers yet</h2><p>Create a customer when the Organizations and Customers API is available. This list will become the source for your relationship workspace.</p><button className="button button-primary" onClick={onCreate}><AddOutlined /> Create customer</button></div>; }
function CustomerTable({ customers, meta, page, setPage }) { return <div className="customer-table-wrap"><table className="customer-table"><thead><tr><th>Customer</th><th>Type</th><th>Owner</th><th>Updated</th></tr></thead><tbody>{customers.map((customer) => <tr key={customer.id}><td><Link to={`/customers/${customer.id}`} className="table-customer"><span className="table-avatar"><BusinessOutlined /></span><span><strong>{customer.name}</strong><small>{customer.industry || 'No industry recorded'}</small></span></Link></td><td>{customer.type || '—'}</td><td>{customer.ownerName || 'Unassigned'}</td><td>{customer.updatedAt ? new Date(customer.updatedAt).toLocaleDateString() : '—'}</td></tr>)}</tbody></table><div className="table-footer"><span>{meta?.total ?? customers.length} customer{(meta?.total ?? customers.length) === 1 ? '' : 's'}</span><div><button className="button button-secondary" disabled={page <= 1} onClick={() => setPage(page - 1)}>Previous</button><button className="button button-secondary" disabled={meta ? page * (meta.pageSize || 25) >= meta.total : customers.length < 25} onClick={() => setPage(page + 1)}>Next</button></div></div></div>; }
