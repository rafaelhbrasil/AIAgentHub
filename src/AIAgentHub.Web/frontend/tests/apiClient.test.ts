import { describe, it, expect, vi, beforeEach } from 'vitest';
import { apiFetch } from '../src/services/apiClient';

describe('apiClient service', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('performs JSON request and parses successful response', async () => {
    const mockData = { id: 'ws-123', name: 'Test Workspace' };
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => mockData,
    } as any);

    const res = await apiFetch('/api/v1/workspaces');
    expect(res.ok).toBe(true);
    expect(res.status).toBe(200);
    expect(res.data).toEqual(mockData);
  });

  it('serializes javascript body object to JSON with Content-Type header', async () => {
    let capturedOptions: any = null;
    globalThis.fetch = vi.fn().mockImplementation((_url, options) => {
      capturedOptions = options;
      return Promise.resolve({
        ok: true,
        status: 201,
        json: async () => ({ id: 'new-id' }),
      } as any);
    });

    const bodyObj = { name: 'My WS', path: 'D:\\Code\\test' };
    await apiFetch('/api/v1/workspaces', { method: 'POST', body: bodyObj });

    expect(capturedOptions.headers['Content-Type']).toBe('application/json');
    expect(capturedOptions.body).toBe(JSON.stringify(bodyObj));
    expect(capturedOptions.credentials).toBe('include');
  });

  it('handles 401 unauthorized gracefully', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 401,
      json: async () => ({ message: 'Unauthorized' }),
    } as any);

    const res = await apiFetch('/api/v1/workspaces');
    expect(res.ok).toBe(false);
    expect(res.status).toBe(401);
  });

  it('parses 401 auth payload with custom invalid_credentials message', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 401,
      json: async () => ({ code: 'invalid_credentials', message: 'Invalid username or password.' }),
    } as any);

    const res = await apiFetch('/api/v1/auth/login', { method: 'POST', body: { username: 'admin', password: 'wrong' } });
    expect(res.ok).toBe(false);
    expect(res.status).toBe(401);
    expect(res.data?.message).toBe('Invalid username or password.');
  });

  it('parses 500 internal server error payload on auth endpoints without masking as 401', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 500,
      json: async () => ({ message: 'Database connection failed' }),
    } as any);

    const res = await apiFetch('/api/v1/auth/login', { method: 'POST', body: { username: 'admin', password: 'password' } });
    expect(res.ok).toBe(false);
    expect(res.status).toBe(500);
    expect(res.data?.message).toBe('Database connection failed');
  });
});

