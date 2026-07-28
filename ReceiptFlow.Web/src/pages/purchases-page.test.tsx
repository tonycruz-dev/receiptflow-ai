import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { PurchaseResponse } from '@/api/contracts';
import { createMockApiClient, renderApp } from '@/test/render-app';

const purchase: PurchaseResponse = {
  purchaseId: 'purchase-1',
  productId: 'product-1',
  productManufacturer: 'Acme',
  productName: 'Toaster',
  modelNumber: 'TX-100',
  receiptId: 'receipt-1',
  receiptLineItemId: 'line-item-1',
  receiptLineItemDescription: 'Acme Toaster',
  purchaseDate: '2026-07-18T12:00:00Z',
  amount: 49.99,
  currency: 'GBP',
  warrantySourceProductManualId: 'manual-1',
  manualVersionLabel: '2.0',
  warrantyDurationMonthsSnapshot: 24,
  warrantyExpiresOn: '2028-07-18',
  warrantyStatus: 'Active',
  createdAtUtc: '2026-07-28T10:00:00Z',
  updatedAtUtc: null,
};

describe('Purchases and warranties page', () => {
  it('shows an empty state when no purchases are linked', async () => {
    renderApp('/purchases');

    expect(
      await screen.findByRole('heading', { name: 'Warranty tracking' }),
    ).toBeInTheDocument();
    expect(screen.getByText('No purchases linked')).toBeInTheDocument();
  });

  it('shows warranty details and links back to receipt and product', async () => {
    renderApp(
      '/purchases',
      createMockApiClient({
        listPurchases: vi.fn().mockResolvedValue({ purchases: [purchase] }),
      }),
    );

    const card = (
      await screen.findByRole('heading', { name: 'Acme Toaster' })
    ).closest('article, div');
    expect(card).not.toBeNull();
    expect(screen.getByText('Active')).toBeInTheDocument();
    expect(screen.getByText('TX-100 · Acme Toaster')).toBeInTheDocument();
    expect(screen.getByText('18 Jul 2026')).toBeInTheDocument();
    expect(screen.getByText('£49.99')).toBeInTheDocument();
    expect(screen.getByText('24 months')).toBeInTheDocument();
    expect(screen.getByText('18 Jul 2028')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /View receipt/ })).toHaveAttribute(
      'href',
      '/receipts/receipt-1',
    );
    expect(screen.getByRole('link', { name: /View product/ })).toHaveAttribute(
      'href',
      '/products/product-1',
    );
    expect(screen.getByRole('link', { name: /Manual 2.0/ })).toHaveAttribute(
      'href',
      '/products/product-1?manualId=manual-1',
    );
  });

  it('confirms before unlinking a purchase', async () => {
    const user = userEvent.setup();
    const unlinkPurchase = vi.fn().mockResolvedValue(undefined);
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    renderApp(
      '/purchases',
      createMockApiClient({
        listPurchases: vi.fn().mockResolvedValue({ purchases: [purchase] }),
        unlinkPurchase,
      }),
    );

    await user.click(await screen.findByRole('button', { name: /Unlink/ }));

    await waitFor(() => {
      expect(window.confirm).toHaveBeenCalledWith(
        'Unlink this purchase from its product?',
      );
      expect(unlinkPurchase).toHaveBeenCalledWith('purchase-1');
    });
  });
});
