import type { ReceiptSearchRequest } from '@/api/contracts';

export const queryKeys = {
  currentUser: ['auth', 'me'] as const,
  dashboard: ['dashboard'] as const,
  receiptLists: ['receipts', 'list'] as const,
  receipts: (page: number, pageSize: number) =>
    ['receipts', 'list', page, pageSize] as const,
  receipt: (receiptId: string) => ['receipts', 'detail', receiptId] as const,
  receiptDocuments: (receiptId: string) =>
    ['receipts', 'documents', receiptId] as const,
  receiptDocument: (receiptId: string, documentId: string) =>
    ['receipts', 'documents', receiptId, documentId] as const,
  receiptSearch: (request: ReceiptSearchRequest) =>
    [
      'receipts',
      'search',
      request.query,
      request.page,
      request.pageSize,
      request.documentType ?? 'Receipt',
    ] as const,
  receiptSearches: ['receipts', 'search'] as const,
  productLists: ['products', 'list'] as const,
  products: ['products', 'list'] as const,
  product: (productId: string) => ['products', 'detail', productId] as const,
  productManuals: (productId: string) =>
    ['products', 'manuals', productId] as const,
  productManual: (productId: string, productManualId: string) =>
    ['products', 'manuals', productId, productManualId] as const,
};
