import { act, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { ReceiptDocumentDetail, ReceiptResponse } from '@/api/contracts';
import { createMockApiClient, renderApp } from '@/test/render-app';

const draftReceipt: ReceiptResponse = {
  id: 'receipt-1',
  merchantName: null,
  purchaseDate: null,
  subtotalAmount: null,
  taxAmount: null,
  totalAmount: null,
  currency: null,
  category: null,
  lifecycleStatus: 'Processing',
  createdAtUtc: '2026-07-19T10:00:00Z',
  lineItems: [],
};

const pendingDocument: ReceiptDocumentDetail = {
  documentId: 'document-1',
  receiptId: 'receipt-1',
  originalFileName: 'receipt.pdf',
  contentType: 'application/pdf',
  fileSize: 100,
  uploadedAtUtc: '2026-07-19T10:00:00Z',
  processingStatus: 'Pending',
  processingError: null,
  receiptLifecycleStatus: 'Processing',
  confirmationRequired: false,
  extraction: null,
};

const completedDocument: ReceiptDocumentDetail = {
  ...pendingDocument,
  processingStatus: 'Completed',
  receiptLifecycleStatus: 'ReviewRequired',
  confirmationRequired: true,
  extraction: {
    merchantName: 'AI Corner Shop',
    transactionDate: '2026-07-18T12:00:00Z',
    subtotal: 10,
    tax: 2,
    total: 12,
    currency: 'GBP',
    category: 'Groceries',
    overallConfidence: 0.98,
    provider: 'hidden',
    modelId: 'hidden',
    extractedAtUtc: '2026-07-19T10:01:00Z',
    lineItems: [
      {
        description: 'Milk',
        quantity: 2,
        unitPrice: 1.5,
        totalPrice: 3,
        tax: null,
        displayOrder: 1,
      },
    ],
  },
};

const confirmedReceipt: ReceiptResponse = {
  ...draftReceipt,
  merchantName: 'Corrected Shop',
  purchaseDate: '2026-07-18T12:00:00Z',
  subtotalAmount: 10,
  taxAmount: 2,
  totalAmount: 12,
  currency: 'GBP',
  category: 'Food',
  lifecycleStatus: 'Confirmed',
  lineItems: [
    {
      id: 'line-item-1',
      description: 'Corrected milk',
      quantity: 2,
      unitPrice: 1.5,
      totalPrice: 3,
      tax: null,
      displayOrder: 1,
    },
  ],
};

afterEach(() => {
  vi.useRealTimers();
});

describe('Receipt processing and review page', () => {
  it('polls processing and populates the review form when extraction completes', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const getReceiptDocument = vi
      .fn()
      .mockResolvedValueOnce(pendingDocument)
      .mockResolvedValue(completedDocument);
    renderDetails({ getReceiptDocument });

    expect(await screen.findByText('Document is pending')).toBeVisible();
    await act(() => vi.advanceTimersByTimeAsync(2_500));

    expect(
      await screen.findByRole('heading', {
        name: 'Review AI-extracted suggestions',
      }),
    ).toBeVisible();
    expect(screen.getByLabelText(/Merchant name/)).toHaveValue(
      'AI Corner Shop',
    );
    expect(screen.getByLabelText(/Total amount/)).toHaveValue(12);
    expect(screen.getByLabelText(/Category/)).toHaveValue('Groceries');
    expect(screen.getByLabelText(/Description/)).toHaveValue('Milk');
    await act(() => vi.advanceTimersByTimeAsync(7_500));
    expect(getReceiptDocument).toHaveBeenCalledTimes(2);
  });

  it('persists user corrections and confirms explicitly', async () => {
    const user = userEvent.setup();
    const confirmReceipt = vi.fn().mockResolvedValue(confirmedReceipt);
    renderDetails({
      getReceiptDocument: vi.fn().mockResolvedValue(completedDocument),
      confirmReceipt,
    });
    const merchant = await screen.findByLabelText(/Merchant name/);
    await user.clear(merchant);
    await user.type(merchant, 'Corrected Shop');
    const category = screen.getByLabelText(/Category/);
    await user.clear(category);
    await user.type(category, 'Food');
    await user.click(screen.getByRole('button', { name: 'Confirm receipt' }));

    expect(await screen.findByText('Receipt confirmed')).toBeVisible();
    expect(confirmReceipt).toHaveBeenCalledWith(
      'receipt-1',
      expect.objectContaining({
        merchantName: 'Corrected Shop',
        category: 'Food',
        totalAmount: 12,
      }),
    );
    expect(confirmReceipt.mock.calls[0]?.[1]).not.toHaveProperty('manualEntry');
  });

  it('offers manual entry only after extraction failure', async () => {
    const user = userEvent.setup();
    const failedDocument = {
      ...pendingDocument,
      processingStatus: 'Failed',
      receiptLifecycleStatus: 'Failed',
      processingError: 'Document processing failed.',
    };
    renderDetails({
      getReceiptDocument: vi.fn().mockResolvedValue(failedDocument),
    });

    expect(await screen.findByText('Extraction failed')).toBeVisible();
    expect(screen.queryByLabelText(/Merchant name/)).not.toBeInTheDocument();
    await user.click(
      screen.getByRole('button', { name: 'Enter details manually' }),
    );
    expect(
      screen.getByRole('heading', { name: 'Enter receipt details' }),
    ).toBeVisible();
    expect(screen.getByLabelText(/Merchant name/)).toHaveValue('');
  });

  it('cleans up active polling when unmounted', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const getReceiptDocument = vi.fn().mockResolvedValue(pendingDocument);
    const { unmount } = renderDetails({ getReceiptDocument });
    await waitFor(() => {
      expect(getReceiptDocument).toHaveBeenCalledOnce();
    });
    unmount();
    await act(() => vi.advanceTimersByTimeAsync(7_500));
    expect(getReceiptDocument).toHaveBeenCalledOnce();
  });
});

function renderDetails(
  overrides: Parameters<typeof createMockApiClient>[0] = {},
) {
  return renderApp(
    '/receipts/receipt-1',
    createMockApiClient({
      getReceipt: vi.fn().mockResolvedValue(draftReceipt),
      listReceiptDocuments: vi.fn().mockResolvedValue([
        {
          documentId: 'document-1',
          originalFileName: 'receipt.pdf',
          contentType: 'application/pdf',
          fileSize: 100,
          uploadedAtUtc: '2026-07-19T10:00:00Z',
          processingStatus: 'Pending',
          hasExtraction: false,
        },
      ]),
      ...overrides,
    }),
  );
}
