import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import { vi } from 'vitest';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import type { ReceiptFlowApiClient } from '@/api/api-client';
import { AuthContext } from '@/providers/auth-context';
import { ThemeProvider } from '@/providers/theme-provider';
import { routes } from '@/routes';

export function createMockApiClient(
  overrides: Partial<ReceiptFlowApiClient> = {},
): ReceiptFlowApiClient {
  return {
    getCurrentUser: vi.fn().mockResolvedValue({
      userId: 'user-bob',
      username: 'bob',
      email: 'bob@example.test',
    }),
    getDashboard: vi.fn().mockResolvedValue({
      totalReceipts: 0,
      spendingByCurrency: [],
      documentsProcessing: 0,
      recentReceipts: [],
    }),
    listReceipts: vi.fn().mockResolvedValue({
      page: 1,
      pageSize: 12,
      total: 0,
      items: [],
    }),
    createReceipt: vi
      .fn()
      .mockRejectedValue(new Error('Not implemented in test.')),
    getReceipt: vi
      .fn()
      .mockRejectedValue(new Error('Not implemented in test.')),
    uploadReceiptDocument: vi
      .fn()
      .mockRejectedValue(new Error('Not implemented in test.')),
    importReceipt: vi
      .fn()
      .mockRejectedValue(new Error('Not implemented in test.')),
    confirmReceipt: vi
      .fn()
      .mockRejectedValue(new Error('Not implemented in test.')),
    listReceiptDocuments: vi.fn().mockResolvedValue([]),
    getReceiptDocument: vi
      .fn()
      .mockRejectedValue(new Error('Not implemented in test.')),
    searchReceipts: vi.fn().mockResolvedValue({
      page: 1,
      pageSize: 10,
      total: 0,
      matches: [],
    }),
    askReceiptQuestion: vi.fn().mockResolvedValue({ answer: '', sources: [] }),
    ...overrides,
  };
}

export function renderApp(
  initialEntry = '/',
  apiClient = createMockApiClient(),
) {
  const router = createMemoryRouter(routes, { initialEntries: [initialEntry] });
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const result = render(
    <ThemeProvider>
      <AuthContext.Provider
        value={{
          isAuthenticated: true,
          apiClient,
          login: vi.fn().mockResolvedValue(undefined),
          logout: vi.fn().mockResolvedValue(undefined),
        }}
      >
        <QueryClientProvider client={queryClient}>
          <RouterProvider router={router} />
        </QueryClientProvider>
      </AuthContext.Provider>
    </ThemeProvider>,
  );

  return { apiClient, queryClient, router, ...result };
}
