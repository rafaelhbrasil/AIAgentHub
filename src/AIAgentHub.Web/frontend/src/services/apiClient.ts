import { ApiResponse } from '../types/api';

export type ApiFetchOptions = Omit<RequestInit, 'body'> & { body?: any };

type UnauthorizedHandler = () => void;
let onUnauthorizedCallback: UnauthorizedHandler | null = null;

export function setUnauthorizedHandler(handler: UnauthorizedHandler | null) {
  onUnauthorizedCallback = handler;
}

export async function apiFetch<T = any>(url: string, options: ApiFetchOptions = {}): Promise<ApiResponse<T>> {
  const fetchOptions: RequestInit = {
    ...options,
    headers: {
      ...(options.headers || {}),
    },
    credentials: 'include',
    body: undefined,
  };

  if (options.body && typeof options.body === 'object' && !(options.body instanceof FormData)) {
    (fetchOptions.headers as Record<string, string>)['Content-Type'] = 'application/json';
    fetchOptions.body = JSON.stringify(options.body);
  } else if (options.body) {
    fetchOptions.body = options.body;
  }

  try {
    const res = await fetch(url, fetchOptions);
    if (res.status === 401 && !url.includes('/auth/')) {
      if (onUnauthorizedCallback) {
        onUnauthorizedCallback();
      }
      return { ok: false, status: 401, error: 'Unauthorized' };
    }
    const data = await res.json().catch(() => undefined);
    return { ok: res.ok, status: res.status, data };
  } catch (err: any) {
    return { ok: false, status: 0, error: err?.message || 'Network error' };
  }
}
