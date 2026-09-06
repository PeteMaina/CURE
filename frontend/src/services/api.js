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
  const response = await fetch(`${API_BASE_URL}${path}`, {
    credentials: 'include',
    headers: { Accept: 'application/json', ...options.headers },
    ...options,
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
};

export const inboxApi = {
  list: () => request('/inbox'),
};