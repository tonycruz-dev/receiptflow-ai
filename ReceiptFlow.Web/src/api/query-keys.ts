import type { ReceiptSearchRequest } from '@/api/contracts';

export const queryKeys = {
  currentUser: ['auth', 'me'] as const,
  dashboard: ['dashboard'] as const,
  receipts: (page: number, pageSize: number) =>
    ['receipts', 'list', page, pageSize] as const,
  receiptSearch: (request: ReceiptSearchRequest) =>
    [
      'receipts',
      'search',
      request.query,
      request.page,
      request.pageSize,
    ] as const,
};
