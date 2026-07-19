import { fireEvent, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { ReceiptDocumentDetail, ReceiptResponse } from '@/api/contracts';
import { maximumReceiptFileSize } from '@/components/receipts/receipt-file-picker';
import { createMockApiClient, renderApp } from '@/test/render-app';

const draftReceipt: ReceiptResponse = {
  id: 'receipt-imported',
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
  documentId: 'document-imported',
  receiptId: draftReceipt.id,
  originalFileName: 'receipt.pdf',
  contentType: 'application/pdf',
  fileSize: 8,
  uploadedAtUtc: '2026-07-19T10:00:00Z',
  processingStatus: 'Pending',
  processingError: null,
  receiptLifecycleStatus: 'Processing',
  confirmationRequired: false,
  extraction: null,
};

beforeEach(() => {
  Object.defineProperty(URL, 'createObjectURL', {
    configurable: true,
    value: vi.fn(() => 'blob:preview'),
  });
  Object.defineProperty(URL, 'revokeObjectURL', {
    configurable: true,
    value: vi.fn(),
  });
});

describe('Upload-first receipt page', () => {
  it('asks only for a receipt file and no metadata', async () => {
    renderApp('/receipts/new');

    expect(await screen.findByLabelText('Receipt file')).toBeInTheDocument();
    expect(screen.queryByLabelText(/Merchant name/)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/Purchase date/)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/Total amount/)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/ISO currency/)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/Category/)).not.toBeInTheDocument();
  });

  it('imports a PDF without submitting fake metadata and navigates to processing', async () => {
    const user = userEvent.setup();
    const importReceipt = vi.fn().mockResolvedValue({
      receiptId: draftReceipt.id,
      documentId: pendingDocument.documentId,
      processingStatus: 'Pending',
    });
    const createReceipt = vi.fn();
    const { router } = renderApp(
      '/receipts/new',
      createMockApiClient({
        importReceipt,
        createReceipt,
        getReceipt: vi.fn().mockResolvedValue(draftReceipt),
        listReceiptDocuments: vi.fn().mockResolvedValue([]),
        getReceiptDocument: vi.fn().mockResolvedValue(pendingDocument),
      }),
    );
    const file = new File(['%PDF-1.7'], 'receipt.pdf', {
      type: 'application/pdf',
    });

    await user.upload(await screen.findByLabelText('Receipt file'), file);
    await user.click(screen.getByRole('button', { name: 'Upload receipt' }));

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/receipts/receipt-imported');
    });
    expect(importReceipt).toHaveBeenCalledWith(file);
    expect(createReceipt).not.toHaveBeenCalled();
  });

  it('accepts an image and displays a safe preview', async () => {
    const user = userEvent.setup();
    renderApp('/receipts/new');
    await user.upload(
      await screen.findByLabelText('Receipt file'),
      new File(['png'], 'receipt.png', { type: 'image/png' }),
    );
    expect(
      screen.getByRole('img', { name: 'Selected receipt preview' }),
    ).toHaveAttribute('src', 'blob:preview');
  });

  it('rejects unsupported and oversized files', async () => {
    renderApp('/receipts/new');
    const input = await screen.findByLabelText('Receipt file');
    fireEvent.change(input, {
      target: {
        files: [new File(['webp'], 'receipt.webp', { type: 'image/webp' })],
      },
    });
    expect(screen.getByText(/Choose a PDF, JPEG or PNG/)).toBeVisible();

    const oversized = new File(
      [new Uint8Array(maximumReceiptFileSize + 1)],
      'receipt.pdf',
      { type: 'application/pdf' },
    );
    fireEvent.change(input, { target: { files: [oversized] } });
    expect(
      screen.getByText('The receipt file must be 10 MB or smaller.'),
    ).toBeVisible();
  });
});
