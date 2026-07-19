import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import { GlobalErrorBoundary } from '@/components/shared/global-error-boundary';
import { AppProviders } from '@/providers/app-providers';
import { routes } from '@/routes';

const router = createBrowserRouter(routes);

export function App() {
  return (
    <GlobalErrorBoundary>
      <AppProviders>
        <RouterProvider router={router} />
      </AppProviders>
    </GlobalErrorBoundary>
  );
}
