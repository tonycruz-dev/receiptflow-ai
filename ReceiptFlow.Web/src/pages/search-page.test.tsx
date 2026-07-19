import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { ReceiptSearchResponse } from '@/api/contracts';
import { createMockApiClient, renderApp } from '@/test/render-app';

async function submitSearch(query = 'USB cables') {
  const user = userEvent.setup();
  await screen.findByRole('heading', { name: 'Receipt search' });
  await user.type(
    screen.getByRole('searchbox', { name: 'Search receipts' }),
    query,
  );
  await user.click(screen.getByRole('button', { name: 'Search' }));
}

describe('Receipt search page', () => {
  it('submits the backend contract and renders safe result fields', async () => {
    const searchReceipts = vi.fn().mockResolvedValue({
      page: 1,
      pageSize: 10,
      total: 1,
      matches: [
        {
          receiptId: 'receipt-1',
          documentId: 'document-1',
          chunkIndex: 0,
          merchantName: 'Cable Store',
          transactionDate: '2026-07-01T12:00:00Z',
          category: 'Electronics',
          currency: 'GBP',
          total: 24.5,
          content: 'Two braided USB cables',
          relevanceScore: 0.92,
          embedding: [0.1, 0.2],
        },
      ],
    });
    renderApp('/search', createMockApiClient({ searchReceipts }));

    await submitSearch();

    expect(searchReceipts).toHaveBeenCalledWith(
      { query: 'USB cables', page: 1, pageSize: 10 },
      expect.any(AbortSignal),
    );
    expect(await screen.findByText('Cable Store')).toBeVisible();
    expect(screen.getByText('Electronics')).toBeVisible();
    expect(screen.getByText('Two braided USB cables')).toBeVisible();
    expect(screen.queryByText('0.1')).not.toBeInTheDocument();
  });

  it('renders a loading state', async () => {
    const searchReceipts = vi.fn(
      () => new Promise<ReceiptSearchResponse>(() => undefined),
    );
    renderApp('/search', createMockApiClient({ searchReceipts }));

    await submitSearch();

    expect(
      await screen.findByRole('status', { name: 'Searching receipts' }),
    ).toBeVisible();
  });

  it('renders an empty result', async () => {
    renderApp('/search');
    await submitSearch();

    expect(
      await screen.findByRole('heading', { name: 'No matching receipts' }),
    ).toBeVisible();
  });

  it('renders a sanitized dependency error', async () => {
    const searchReceipts = vi
      .fn()
      .mockRejectedValue(new Error('provider secret response'));
    renderApp('/search', createMockApiClient({ searchReceipts }));
    await submitSearch();

    expect(
      await screen.findByRole('heading', { name: 'Search unavailable' }),
    ).toBeVisible();
    expect(
      screen.queryByText('provider secret response'),
    ).not.toBeInTheDocument();
  });
});
