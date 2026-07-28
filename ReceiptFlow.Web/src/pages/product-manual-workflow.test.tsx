import { fireEvent, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { ProductManualResponse, ProductResponse } from '@/api/contracts';
import { createMockApiClient, renderApp } from '@/test/render-app';

const product: ProductResponse = {
  productId: 'product-1',
  manufacturer: 'Acme',
  name: 'Toaster',
  modelNumber: 'TX-100',
  createdAtUtc: '2026-07-28T10:00:00Z',
  updatedAtUtc: null,
};

const reviewManual: ProductManualResponse = {
  productManualId: 'manual-1',
  productId: product.productId,
  manufacturer: product.manufacturer,
  productName: product.name,
  modelNumber: product.modelNumber,
  documentId: 'document-1',
  originalFileName: 'toaster.pdf',
  contentType: 'application/pdf',
  fileSize: 1024,
  documentProcessingStatus: 'AwaitingReview',
  manualLifecycleStatus: 'ReviewRequired',
  manualKind: 'UserManual',
  locale: 'und',
  versionLabel: null,
  warrantyDurationMonths: null,
  supersedesProductManualId: null,
  uploadedAtUtc: '2026-07-28T10:00:00Z',
  confirmedAtUtc: null,
  supersededAtUtc: null,
  extraction: {
    suggestedManufacturer: 'Suggested Corp',
    suggestedProductName: 'Smart Toaster',
    suggestedModelNumber: 'ST-200',
    suggestedVersionLabel: '2.0',
    suggestedWarrantyDurationMonths: 24,
    overallConfidence: 0.9,
    extractedAtUtc: '2026-07-28T10:01:00Z',
  },
  sections: [
    {
      ordinal: 0,
      headingPath: 'Safety > Electrical',
      pageStart: 1,
      pageEnd: 2,
      content: 'Keep the appliance dry.',
    },
  ],
};

describe('product manual workflow', () => {
  it('offers receipt and product manual upload choices', async () => {
    renderApp('/upload');

    expect(
      await screen.findByRole('heading', { name: 'Upload' }, { timeout: 3000 }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('link', { name: /^Receipt Extract/ }),
    ).toHaveAttribute('href', '/receipts/new');
    expect(
      screen.getByRole('link', { name: /Product manual/ }),
    ).toHaveAttribute('href', '/products/manuals/new');
  });

  it('selects a product and uploads a PDF manual', async () => {
    const user = userEvent.setup();
    const uploadProductManual = vi.fn().mockResolvedValue(reviewManual);
    const apiClient = createMockApiClient({
      listProducts: vi.fn().mockResolvedValue([product]),
      uploadProductManual,
      getProduct: vi.fn().mockResolvedValue(product),
      listProductManuals: vi.fn().mockResolvedValue([reviewManual]),
    });
    renderApp('/products/manuals/new', apiClient);

    const productSelect = await screen.findByLabelText(
      'Product',
      {},
      { timeout: 3000 },
    );
    await screen.findByRole('option', { name: /Acme Toaster/ });
    await user.selectOptions(productSelect, product.productId);
    const file = new File(['%PDF-1.7'], 'toaster.pdf', {
      type: 'application/pdf',
    });
    await user.upload(screen.getByLabelText('Product manual PDF'), file);
    expect(productSelect).toHaveValue(product.productId);
    const uploadButton = screen.getByRole('button', {
      name: 'Upload manual',
    });
    await waitFor(() => {
      expect(uploadButton).toBeEnabled();
    });
    const uploadForm = uploadButton.closest('form');
    if (!uploadForm) throw new Error('Manual upload form was not found.');
    fireEvent.submit(uploadForm);

    await waitFor(() => {
      expect(uploadProductManual).toHaveBeenCalledWith(
        product.productId,
        file,
        {},
      );
    });
  });

  it('polls processing manuals and exposes failure retry state', async () => {
    const queued = {
      ...reviewManual,
      documentProcessingStatus: 'Queued',
      manualLifecycleStatus: 'Processing',
      extraction: null,
      sections: [],
    };
    const failed = {
      ...queued,
      documentProcessingStatus: 'Failed',
      manualLifecycleStatus: 'Failed',
    };
    const listProductManuals = vi
      .fn()
      .mockResolvedValueOnce([queued])
      .mockResolvedValue([failed]);
    renderApp(
      `/products/${product.productId}`,
      createMockApiClient({
        getProduct: vi.fn().mockResolvedValue(product),
        listProductManuals,
      }),
    );

    expect(
      await screen.findByText(
        'Processing manual. This page refreshes automatically.',
        {},
        { timeout: 3000 },
      ),
    ).toBeInTheDocument();
    await waitFor(
      () => {
        expect(listProductManuals).toHaveBeenCalledTimes(2);
        expect(
          screen.getByRole('link', { name: 'Try another PDF' }),
        ).toBeInTheDocument();
      },
      { timeout: 4000 },
    );
  });

  it('reviews suggestions and confirms the manual', async () => {
    const user = userEvent.setup();
    const confirmed = {
      ...reviewManual,
      manufacturer: 'Suggested Corp',
      productName: 'Smart Toaster',
      modelNumber: 'ST-200',
      versionLabel: '2.0',
      warrantyDurationMonths: 24,
      documentProcessingStatus: 'Completed',
      manualLifecycleStatus: 'Active',
    };
    const confirmProductManual = vi.fn().mockResolvedValue(confirmed);
    renderApp(
      `/products/${product.productId}`,
      createMockApiClient({
        getProduct: vi.fn().mockResolvedValue(product),
        listProductManuals: vi.fn().mockResolvedValue([reviewManual]),
        confirmProductManual,
      }),
    );

    expect(
      await screen.findByRole(
        'heading',
        { name: 'Review extracted details' },
        { timeout: 3000 },
      ),
    ).toBeInTheDocument();
    expect(screen.getByLabelText('Manufacturer')).toHaveValue('Suggested Corp');
    expect(screen.getByText('Safety > Electrical')).toBeInTheDocument();
    await user.click(
      screen.getByRole('button', { name: 'Confirm and activate' }),
    );

    await waitFor(() => {
      expect(confirmProductManual).toHaveBeenCalledWith(
        product.productId,
        reviewManual.productManualId,
        {
          manufacturer: 'Suggested Corp',
          productName: 'Smart Toaster',
          modelNumber: 'ST-200',
          versionLabel: '2.0',
          locale: 'und',
          warrantyDurationMonths: 24,
        },
      );
    });
  });
});
