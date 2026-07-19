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

  it('uploads multipart field file without overriding the browser boundary', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      jsonResponse(
        {
          documentId: 'document-1',
          receiptId: 'receipt-1',
          originalFileName: 'receipt.pdf',
          contentType: 'application/pdf',
          fileSize: 8,
          processingStatus: 'Pending',
        },
        201,
      ),
    );
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      getAccessToken: vi.fn().mockResolvedValue('token'),
      onUnauthorized: vi.fn(),
      fetchImplementation: fetchMock,
    });
    const file = new File(['%PDF-1.7'], 'receipt.pdf', {
      type: 'application/pdf',
    });

    await client.uploadReceiptDocument('receipt-1', file);

    const request = fetchMock.mock.calls[0]?.[1];
    const headers = new Headers(request?.headers);
    expect(headers.has('Content-Type')).toBe(false);
    expect(request?.body).toBeInstanceOf(FormData);
    expect((request?.body as FormData).get('file')).toBe(file);
  });

  it('imports a receipt as file-only multipart data', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      jsonResponse(
        {
          receiptId: 'receipt-1',
          documentId: 'document-1',
          processingStatus: 'Pending',
        },
        202,
      ),
    );
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      getAccessToken: vi.fn().mockResolvedValue('token'),
      onUnauthorized: vi.fn(),
      fetchImplementation: fetchMock,
    });
    const file = new File(['%PDF-1.7'], 'receipt.pdf', {
      type: 'application/pdf',
    });

    await client.importReceipt(file);

    expect(fetchMock.mock.calls[0]?.[0]).toBe(
      'https://api.example.test/api/receipts/import',
    );
    const request = fetchMock.mock.calls[0]?.[1];
    expect(request?.method).toBe('POST');
    expect(new Headers(request?.headers).has('Content-Type')).toBe(false);
    const form = request?.body as FormData;
    expect([...form.keys()]).toEqual(['file']);
    expect(form.get('file')).toBe(file);
  });

  it('submits corrected receipt confirmation with PUT', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValue(
        jsonResponse({ id: 'receipt-1', lifecycleStatus: 'Confirmed' }),
      );
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      getAccessToken: vi.fn().mockResolvedValue('token'),
      onUnauthorized: vi.fn(),
      fetchImplementation: fetchMock,
    });
    const confirmation = {
      merchantName: 'Corrected Shop',
      purchaseDate: '2026-07-18T12:00:00Z',
      subtotal: 10,
      tax: 2,
      totalAmount: 12,
      currency: 'GBP',
      category: 'Food',
      lineItems: [],
    };

    await client.confirmReceipt('receipt-1', confirmation);

    expect(fetchMock.mock.calls[0]?.[0]).toBe(
      'https://api.example.test/api/receipts/receipt-1/confirmation',
    );
    const request = fetchMock.mock.calls[0]?.[1];
    expect(request?.method).toBe('PUT');
    expect(request?.body).toBe(JSON.stringify(confirmation));
  });

  it('coordinates upload re-authentication only for 401, not 403', async () => {
    const onUnauthorized = vi.fn().mockResolvedValue(undefined);
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockResolvedValueOnce(new Response(null, { status: 403 }));
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      getAccessToken: vi.fn().mockResolvedValue('token'),
      onUnauthorized,
      fetchImplementation: fetchMock,
    });
    const file = new File(['%PDF'], 'receipt.pdf', { type: 'application/pdf' });

    await expect(
      client.uploadReceiptDocument('receipt-1', file),
    ).rejects.toMatchObject({ status: 401 });
    await expect(
      client.uploadReceiptDocument('receipt-1', file),
    ).rejects.toMatchObject({ status: 403 });
    expect(onUnauthorized).toHaveBeenCalledOnce();
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
