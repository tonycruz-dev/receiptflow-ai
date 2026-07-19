import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '@/api/api-error';
import { createApiClient } from '@/api/api-client';

function jsonResponse(
  body: unknown,
  status = 200,
  contentType = 'application/json',
) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': contentType },
  });
}

describe('authenticated API client', () => {
  it('refreshes and attaches the access token before a request', async () => {
    const events: string[] = [];
    const fetchMock = vi.fn<typeof fetch>(() => {
      events.push('fetch');
      return Promise.resolve(
        jsonResponse({
          userId: '1',
          username: 'bob',
          email: 'bob@example.test',
        }),
      );
    });
    const getAccessToken = vi.fn(() => {
      events.push('refresh');
      return Promise.resolve('access-token-value');
    });
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      getAccessToken,
      onUnauthorized: vi.fn(),
      fetchImplementation: fetchMock,
    });

    await client.getCurrentUser();

    expect(events).toEqual(['refresh', 'fetch']);
    const request = fetchMock.mock.calls[0]?.[1];
    expect(new Headers(request?.headers).get('Authorization')).toBe(
      'Bearer access-token-value',
    );
  });

  it('requests owner-scoped dashboard and paginated receipt endpoints', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(
        jsonResponse({
          totalReceipts: 0,
          spendingByCurrency: [],
          documentsProcessing: 0,
          recentReceipts: [],
        }),
      )
      .mockResolvedValueOnce(
        jsonResponse({ page: 2, pageSize: 12, total: 13, items: [] }),
      );
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      getAccessToken: vi.fn().mockResolvedValue('token'),
      onUnauthorized: vi.fn(),
      fetchImplementation: fetchMock,
    });

    await client.getDashboard();
    await client.listReceipts(2, 12);

    expect(fetchMock.mock.calls[0]?.[0]).toBe(
      'https://api.example.test/api/dashboard',
    );
    expect(fetchMock.mock.calls[1]?.[0]).toBe(
      'https://api.example.test/api/receipts?page=2&pageSize=12',
    );
  });

  it('parses ProblemDetails and triggers controlled re-authentication on 401', async () => {
    const onUnauthorized = vi.fn().mockResolvedValue(undefined);
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      getAccessToken: vi.fn().mockResolvedValue('token'),
      onUnauthorized,
      fetchImplementation: vi.fn().mockResolvedValue(
        jsonResponse(
          {
            title: 'Authentication required',
            detail: 'Please sign in.',
            status: 401,
          },
          401,
          'application/problem+json',
        ),
      ),
    });

    await expect(client.getCurrentUser()).rejects.toMatchObject({
      status: 401,
      problem: { title: 'Authentication required', detail: 'Please sign in.' },
    });
    expect(onUnauthorized).toHaveBeenCalledOnce();
  });

  it('does not trigger re-authentication on 403', async () => {
    const onUnauthorized = vi.fn().mockResolvedValue(undefined);
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      getAccessToken: vi.fn().mockResolvedValue('token'),
      onUnauthorized,
      fetchImplementation: vi
        .fn()
        .mockResolvedValue(new Response(null, { status: 403 })),
    });

    await expect(client.getCurrentUser()).rejects.toMatchObject({
      status: 403,
    });
    expect(onUnauthorized).not.toHaveBeenCalled();
  });

  it('propagates caller cancellation', async () => {
    const controller = new AbortController();
    controller.abort();
    const fetchMock = vi.fn<typeof fetch>((_input, init) => {
      expect(init?.signal).toBe(controller.signal);
      return Promise.reject(new DOMException('Aborted', 'AbortError'));
    });
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      getAccessToken: vi.fn().mockResolvedValue('token'),
      onUnauthorized: vi.fn(),
      fetchImplementation: fetchMock,
    });

    await expect(
      client.getCurrentUser(controller.signal),
    ).rejects.toMatchObject({
      name: 'AbortError',
    });
  });

  it('never logs tokens, response bodies or embeddings', async () => {
    const consoleSpies = [
      vi.spyOn(console, 'log'),
      vi.spyOn(console, 'info'),
      vi.spyOn(console, 'warn'),
      vi.spyOn(console, 'error'),
    ];
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      getAccessToken: vi.fn().mockResolvedValue('sensitive-token'),
      onUnauthorized: vi.fn(),
      fetchImplementation: vi
        .fn()
        .mockResolvedValue(jsonResponse({ embedding: [0.1, 0.2] }, 500)),
    });

    await expect(client.getCurrentUser()).rejects.toBeInstanceOf(ApiError);
    for (const spy of consoleSpies) expect(spy).not.toHaveBeenCalled();
  });
});
