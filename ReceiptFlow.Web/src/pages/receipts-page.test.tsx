import { screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '@/api/api-error';
import type { ReceiptListResponse } from '@/api/contracts';
import { createMockApiClient, renderApp } from '@/test/render-app';

describe('Receipts page', () => {
  it('renders receipt-list results from the API', async () => {
    const listReceipts = vi.fn().mockResolvedValue({
      page: 1,
      pageSize: 12,
      total: 1,
      items: [
        {
          receiptId: 'receipt-live',
          merchantName: 'Real Market',
          purchaseDate: '2026-07-17T12:00:00Z',
          totalAmount: 47.86,
          currency: 'EUR',
          category: 'Groceries',
          documentId: 'document-live',
          originalFileName: 'real-receipt.jpg',
          processingStatus: 'Queued',
        },
      ],
    });
    renderApp('/receipts', createMockApiClient({ listReceipts }));

    expect(await screen.findByText('Real Market')).toBeVisible();
    expect(screen.getByText('real-receipt.jpg')).toBeVisible();
    expect(screen.getByText('€47.86')).toBeVisible();
    expect(screen.getByText('Processing')).toBeVisible();
    expect(listReceipts).toHaveBeenCalledWith(1, 12, expect.any(AbortSignal));
  });

  it('renders loading and empty states without fixtures', async () => {
    const { unmount } = renderApp(
      '/receipts',
      createMockApiClient({
        listReceipts: vi.fn(
          () => new Promise<ReceiptListResponse>(() => undefined),
        ),
      }),
    );

    expect(await screen.findByLabelText('Loading receipts')).toBeVisible();
    expect(screen.queryByText('The Corner Market')).not.toBeInTheDocument();
    unmount();

    renderApp('/receipts', createMockApiClient());
    expect(
      await screen.findByRole('heading', { name: 'No receipts yet' }),
    ).toBeVisible();
  });

  it('renders a retryable list error', async () => {
    renderApp(
      '/receipts',
      createMockApiClient({
        listReceipts: vi
          .fn()
          .mockRejectedValue(new ApiError('Unavailable', 503)),
      }),
    );

    expect(
      await screen.findByRole('heading', { name: 'Receipts unavailable' }),
    ).toBeVisible();
    expect(screen.getByRole('button', { name: 'Try again' })).toBeVisible();
  });
});
