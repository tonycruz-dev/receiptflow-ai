import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useState, type PropsWithChildren } from 'react';
import { shouldRetryRequest } from '@/api/api-error';
import type { AppEnvironment } from '@/config/env';
import { AuthProvider, type KeycloakFactory } from '@/providers/auth-provider';
import { ThemeProvider } from '@/providers/theme-provider';

interface AppProvidersProps extends PropsWithChildren {
  environment: AppEnvironment;
  keycloakFactory?: KeycloakFactory | undefined;
}

export function AppProviders({
  children,
  environment,
  keycloakFactory,
}: AppProvidersProps) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            retry: shouldRetryRequest,
            staleTime: 60_000,
            refetchOnWindowFocus: false,
          },
          mutations: { retry: false },
        },
      }),
  );

  return (
    <ThemeProvider>
      <AuthProvider environment={environment} keycloakFactory={keycloakFactory}>
        <QueryClientProvider client={queryClient}>
          {children}
        </QueryClientProvider>
      </AuthProvider>
    </ThemeProvider>
  );
}
