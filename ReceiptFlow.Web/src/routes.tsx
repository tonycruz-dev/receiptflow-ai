import type { RouteObject } from 'react-router-dom';
import { AppShell } from '@/components/layout/app-shell';
import { ErrorState } from '@/components/shared/error-state';

export const routes: RouteObject[] = [
  {
    path: '/',
    element: <AppShell />,
    errorElement: (
      <div className="grid min-h-screen place-items-center p-6">
        <ErrorState
          title="This page could not load"
          description="The requested view encountered an unexpected error."
        />
      </div>
    ),
    children: [
      { index: true, lazy: () => import('@/pages/dashboard-page') },
      { path: 'receipts', lazy: () => import('@/pages/receipts-page') },
      {
        path: 'receipts/new',
        lazy: () => import('@/pages/upload-receipt-page'),
      },
      {
        path: 'receipts/:receiptId',
        lazy: () => import('@/pages/receipt-details-page'),
      },
      { path: 'search', lazy: () => import('@/pages/search-page') },
      { path: 'assistant', lazy: () => import('@/pages/assistant-page') },
      { path: 'profile', lazy: () => import('@/pages/profile-page') },
      { path: '*', lazy: () => import('@/pages/not-found-page') },
    ],
  },
];
