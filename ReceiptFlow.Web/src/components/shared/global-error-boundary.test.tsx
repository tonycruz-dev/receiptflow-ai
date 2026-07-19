import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { GlobalErrorBoundary } from '@/components/shared/global-error-boundary';

function BrokenView(): never {
  throw new Error('Test render error');
}

describe('GlobalErrorBoundary', () => {
  it('renders a useful fallback', () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined);

    render(
      <GlobalErrorBoundary>
        <BrokenView />
      </GlobalErrorBoundary>,
    );

    expect(
      screen.getByRole('heading', { name: 'ReceiptFlow could not load' }),
    ).toBeVisible();
    expect(screen.getByRole('button', { name: 'Refresh page' })).toBeVisible();
  });
});
