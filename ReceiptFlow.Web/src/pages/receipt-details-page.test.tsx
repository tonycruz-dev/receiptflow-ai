import { act, screen, waitFor, within } from '@testing-library/react';
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

  it('presents completed extraction as review required', async () => {
    renderDetails({
      getReceiptDocument: vi.fn().mockResolvedValue(completedDocument),
    });

    const notice = (
      await screen.findByText('Review required', { selector: 'p' })
    ).closest('[role="status"]');
    expect(notice).not.toBeNull();
    expect(notice).toHaveTextContent(
      'Review the suggested values before this receipt affects spending totals or search.',
    );
    expect(
      screen.getByRole('heading', { name: 'Review extracted details' }),
    ).toBeVisible();
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
      processingError: 'ProviderException: secret technical detail',
    };
    renderDetails({
      getReceiptDocument: vi.fn().mockResolvedValue(failedDocument),
    });

    expect(await screen.findByText('Extraction failed')).toBeVisible();
    expect(
      screen.queryByText(/ProviderException: secret technical detail/),
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: 'Upload a replacement' }),
    ).toBeVisible();
    expect(screen.queryByLabelText(/Merchant name/)).not.toBeInTheDocument();
    await user.click(
      screen.getByRole('button', { name: 'Enter details manually' }),
    );
    expect(
      screen.getByRole('heading', { name: 'Enter receipt details' }),
    ).toBeVisible();
    expect(screen.getByLabelText(/Merchant name/)).toHaveValue('');
  });

  it('uploads a validated replacement document and selects it', async () => {
    const user = userEvent.setup();
    const failedDocument: ReceiptDocumentDetail = {
      ...pendingDocument,
      processingStatus: 'Failed',
      receiptLifecycleStatus: 'Failed',
      processingError: 'Extraction provider failed.',
    };
    const replacementDocument: ReceiptDocumentDetail = {
      ...pendingDocument,
      documentId: 'document-2',
      originalFileName: 'clearer-receipt.pdf',
    };
    const uploadReceiptDocument = vi.fn().mockResolvedValue({
      documentId: 'document-2',
      receiptId: 'receipt-1',
      originalFileName: 'clearer-receipt.pdf',
      contentType: 'application/pdf',
      fileSize: 120,
      processingStatus: 'Pending',
    });
    const getReceiptDocument = vi
      .fn()
      .mockImplementation((_receiptId: string, documentId: string) =>
        Promise.resolve(
          documentId === 'document-2' ? replacementDocument : failedDocument,
        ),
      );
    renderDetails({ getReceiptDocument, uploadReceiptDocument });

    const fileInput = await screen.findByLabelText('Receipt file');
    const file = new File(['receipt'], 'clearer-receipt.pdf', {
      type: 'application/pdf',
    });
    await user.upload(fileInput, file);
    await user.click(
      screen.getByRole('button', { name: 'Upload replacement' }),
    );

    await waitFor(() => {
      expect(uploadReceiptDocument).toHaveBeenCalledWith('receipt-1', file);
      expect(getReceiptDocument).toHaveBeenCalledWith(
        'receipt-1',
        'document-2',
        expect.any(AbortSignal),
      );
    });
    expect(
      await screen.findByText('Source: clearer-receipt.pdf'),
    ).toBeVisible();
  });

  it('shows confirmed totals, purchase information and numbered line items', async () => {
    const confirmedDocument: ReceiptDocumentDetail = {
      ...completedDocument,
      receiptLifecycleStatus: 'Confirmed',
      confirmationRequired: false,
    };
    renderDetails({
      getReceipt: vi.fn().mockResolvedValue(confirmedReceipt),
      getReceiptDocument: vi.fn().mockResolvedValue(confirmedDocument),
    });

    expect(
      await screen.findByRole('heading', { name: 'Confirmed receipt' }),
    ).toBeVisible();
    const purchaseInformation = screen
      .getByRole('heading', { name: 'Purchase information' })
      .closest('section');
    if (!purchaseInformation) throw new Error('Purchase section not found.');
    expect(
      within(purchaseInformation).getByText('Corrected Shop'),
    ).toBeVisible();
    expect(within(purchaseInformation).getByText('18 July 2026')).toBeVisible();
    expect(within(purchaseInformation).getByText('GBP')).toBeVisible();
    expect(within(purchaseInformation).getByText('Food')).toBeVisible();
    expect(within(purchaseInformation).getByText('receipt.pdf')).toBeVisible();

    const totals = screen.getByText('Subtotal').closest('dl');
    if (!totals) throw new Error('Totals list not found.');
    expect(within(totals).getByText('£10.00')).toBeVisible();
    expect(within(totals).getByText('£2.00')).toBeVisible();
    expect(within(totals).getByText('£12.00')).toBeVisible();

    const lineItems = screen
      .getByRole('heading', { name: 'Line items' })
      .closest('section');
    if (!lineItems) throw new Error('Line items section not found.');
    expect(within(lineItems).getByText('1')).toBeVisible();
    expect(within(lineItems).getByText('Corrected milk')).toBeVisible();
    expect(within(lineItems).getByText('Quantity 2')).toBeVisible();
    expect(within(lineItems).getByText('£3.00')).toBeVisible();
  });

  it('shows the redesigned empty-document state', async () => {
    renderDetails({
      listReceiptDocuments: vi.fn().mockResolvedValue([]),
    });

    expect(
      await screen.findByRole('heading', { name: 'No document uploaded' }),
    ).toBeVisible();
    expect(screen.getByText('No source document')).toBeVisible();
    expect(
      screen.getByText(
        'This receipt does not have a source document to display or process.',
      ),
    ).toBeVisible();
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
