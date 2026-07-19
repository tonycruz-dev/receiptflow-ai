import { render } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { AppProviders } from '@/providers/app-providers';
import { routes } from '@/routes';

export function renderApp(initialEntry = '/') {
  const router = createMemoryRouter(routes, { initialEntries: [initialEntry] });
  const result = render(
    <AppProviders>
      <RouterProvider router={router} />
    </AppProviders>,
  );

  return { router, ...result };
}
