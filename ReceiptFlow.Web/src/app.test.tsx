import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { renderApp } from '@/test/render-app';

describe('application shell', () => {
  it('renders the dashboard with mocked API data and no live calls', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch');
    renderApp();

    expect(
      await screen.findByRole('heading', { name: 'Good morning' }),
    ).toBeInTheDocument();
    expect(await screen.findByText('Total receipts')).toBeInTheDocument();
    expect(fetchSpy).not.toHaveBeenCalled();
  });

  it('navigates between routes', async () => {
    const user = userEvent.setup();
    const { router } = renderApp();
    await screen.findByRole('heading', { name: 'Good morning' });

    const receiptsLink = screen
      .getAllByRole('link', { name: 'Receipts' })
      .at(0);
    if (!receiptsLink)
      throw new Error('Receipts navigation link was not found.');
    await user.click(receiptsLink);

    expect(
      await screen.findByRole('heading', { name: 'Receipts' }),
    ).toBeVisible();
    expect(router.state.location.pathname).toBe('/receipts');
  });

  it('provides labelled mobile navigation', async () => {
    renderApp();
    await screen.findByRole('heading', { name: 'Good morning' });

    const navigation = screen.getByRole('navigation', {
      name: 'Mobile navigation',
    });
    expect(navigation).toBeInTheDocument();
    expect(navigation).toHaveTextContent('Dashboard');
    expect(navigation).toHaveTextContent('Assistant');
  });
});
