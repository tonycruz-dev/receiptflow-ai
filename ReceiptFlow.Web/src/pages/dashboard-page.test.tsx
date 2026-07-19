import { screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '@/api/api-error';
import type { DashboardResponse } from '@/api/contracts';
import { createMockApiClient, renderApp } from '@/test/render-app';

const dashboard: DashboardResponse = {
  totalReceipts: 5,
  spendingByCurrency: [{ currency: 'GBP', amount: 143.92 }],
  documentsProcessing: 1,
  recentReceipts: [
    {
      receiptId: 'receipt-1',
      merchantName: 'North & Pine Stationery',
      purchaseDate: '2026-07-18T10:00:00Z',
      totalAmount: 71.96,
      currency: 'GBP',
      category: 'Stationery',
      lifecycleStatus: 'Confirmed',
      documentId: 'document-1',
      originalFileName: 'receipt-test.pdf',
      processingStatus: 'Completed',
    },
  ],
};

describe('Dashboard page', () => {
  it('renders its loading state', async () => {
    renderApp(
      '/',
      createMockApiClient({
        getDashboard: vi.fn(
          () => new Promise<DashboardResponse>(() => undefined),
        ),
      }),
    );

    expect(await screen.findByLabelText('Loading dashboard')).toBeVisible();
  });

  it('renders real dashboard values without fixture fallbacks', async () => {
    renderApp(
      '/',
      createMockApiClient({
        getDashboard: vi.fn().mockResolvedValue(dashboard),
      }),
    );

    expect(await screen.findByText('North & Pine Stationery')).toBeVisible();
    expect(screen.getByText('5')).toBeVisible();
    expect(screen.getByText('£143.92')).toBeVisible();
    expect(screen.getByText('receipt-test.pdf')).toBeVisible();
    expect(screen.queryByText('128')).not.toBeInTheDocument();
    expect(screen.queryByText('£6,842.35')).not.toBeInTheDocument();
  });

  it('keeps totals for multiple currencies separate', async () => {
    renderApp(
      '/',
      createMockApiClient({
        getDashboard: vi.fn().mockResolvedValue({
          ...dashboard,
          spendingByCurrency: [
            { currency: 'EUR', amount: 20 },
            { currency: 'USD', amount: 30 },
          ],
        }),
      }),
    );

    expect(await screen.findByText('Total spending · EUR')).toBeVisible();
    expect(screen.getByText('Total spending · USD')).toBeVisible();
    expect(screen.getByText('€20.00')).toBeVisible();
    expect(screen.getByText('US$30.00')).toBeVisible();
  });

  it('renders the empty account state', async () => {
    renderApp('/', createMockApiClient());

    expect(
      await screen.findByRole('heading', { name: 'No receipts yet' }),
    ).toBeVisible();
    expect(screen.getByText('No recorded spending')).toBeVisible();
  });

  it('renders a retryable API error state', async () => {
    renderApp(
      '/',
      createMockApiClient({
        getDashboard: vi
          .fn()
          .mockRejectedValue(new ApiError('Unavailable', 503)),
      }),
    );

    expect(
      await screen.findByRole('heading', { name: 'Dashboard unavailable' }),
    ).toBeVisible();
    expect(screen.getByRole('button', { name: 'Try again' })).toBeVisible();
  });
});
