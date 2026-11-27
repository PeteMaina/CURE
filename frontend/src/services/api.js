import { getAccessToken } from "./oidc";

const API_BASE_URL = process.env.CURE_API_BASE_URL || '/api/v1';

export class ApiError extends Error {
  constructor(message, { status, code, requestId } = {}) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
    this.requestId = requestId;
  }
}

export async function request(path, options = {}) {
  const developmentHeaders = process.env.NODE_ENV === 'development' ? {
    'X-CURE-Tenant-Id': process.env.CURE_DEV_TENANT_ID,
    'X-CURE-User-Id': process.env.CURE_DEV_USER_ID,
  } : {};
  const accessToken = await getAccessToken();
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    credentials: options.credentials || 'include',
    headers: { Accept: 'application/json', ...developmentHeaders, ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}), ...options.headers },
  });

  const contentType = response.headers.get('content-type') || '';
  const body = contentType.includes('application/json') ? await response.json() : null;

  if (!response.ok) {
    throw new ApiError(body?.error?.message || `Request failed with status ${response.status}.`, {
      status: response.status,
      code: body?.error?.code,
      requestId: body?.error?.requestId,
    });
  }

  return body;
}

export const customerApi = {
  list: (searchParams = '') => request(`/customers${searchParams ? `?${searchParams}` : ''}`),
  get: (customerId) => request(`/customers/${customerId}`),
  get360: (customerId) => request(`/customers/${customerId}/360`),
  create: (payload) => request('/customers', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'Idempotency-Key': crypto.randomUUID() },
    body: JSON.stringify(payload),
  }),
  update: (customerId, payload, expectedVersion) => request(`/customers/${customerId}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json', 'If-Match': String(expectedVersion) },
    body: JSON.stringify(payload),
  }),
  delete: (customerId) => request(`/customers/${customerId}`, { method: 'DELETE' }),
  contacts: (customerId) => request(`/customers/${customerId}/360`).then((result) => ({ data: result?.data?.contacts || [] })),
  linkContact: (customerId, contactId) => request(`/customers/${customerId}/contacts`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ contactId }),
  }),
  unlinkContact: (customerId, contactId) => request(`/customers/${customerId}/contacts/${contactId}`, { method: 'DELETE' }),
};

export const inboxApi = {
  list: (searchParams = '') => request(`/inbox${searchParams ? `?${searchParams}` : ''}`),
};

export const workspaceApi = {
  dashboard: () => request('/home/dashboard'),
  search: (query) => request(`/search?${new URLSearchParams({ query, limit: '20' })}`),
};

export const notificationApi = {
  list: () => request('/notifications?limit=20'),
  markRead: (entityType, entityId) => request(`/notifications/${entityType}/${entityId}/read`, { method: 'POST' }),
  markUnread: (entityType, entityId) => request(`/notifications/${entityType}/${entityId}/read`, { method: 'DELETE' }),
};

export const authApi = {
  developmentToken: (password) => request('/auth/development-token', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ password }),
  }),
};

export const coreApi = {
  list: (resource, searchParams = '') => request(`/${resource}${searchParams ? `?${searchParams}` : ''}`),
  get: (resource, id) => request(`/${resource}/${id}`),
  create: (resource, payload) => request(`/${resource}`, { method: 'POST', headers: { 'Content-Type': 'application/json', 'Idempotency-Key': crypto.randomUUID() }, body: JSON.stringify(payload) }),
  update: (resource, id, payload, version) => request(`/${resource}/${id}`, { method: 'PATCH', headers: { 'Content-Type': 'application/json', 'If-Match': String(version) }, body: JSON.stringify(payload) }),
  delete: (resource, id) => request(`/${resource}/${id}`, { method: 'DELETE' }),
};

export const pipelineApi = {
  summary: () => request('/pipeline/summary'),
  history: (opportunityId) => request(`/opportunities/${opportunityId}/history`),
  transition: (opportunityId, payload) => request(`/opportunities/${opportunityId}/transition`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  }),
  convertLead: (leadId, payload) => request(`/leads/${leadId}/convert`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  }),
};