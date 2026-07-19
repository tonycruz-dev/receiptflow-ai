import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import { GlobalErrorBoundary } from '@/components/shared/global-error-boundary';
import type { AppEnvironment } from '@/config/env';
import type { KeycloakFactory } from '@/providers/auth-provider';
import { AppProviders } from '@/providers/app-providers';
import { routes } from '@/routes';

const router = createBrowserRouter(routes);

interface AppProps {
  environment: AppEnvironment;
  keycloakFactory?: KeycloakFactory | undefined;
}

export function App({ environment, keycloakFactory }: AppProps) {
  return (
    <GlobalErrorBoundary>
      <AppProviders environment={environment} keycloakFactory={keycloakFactory}>
        <RouterProvider router={router} />
      </AppProviders>
    </GlobalErrorBoundary>
  );
}
