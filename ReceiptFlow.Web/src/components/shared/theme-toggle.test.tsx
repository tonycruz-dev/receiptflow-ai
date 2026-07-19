import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { ThemeToggle } from '@/components/shared/theme-toggle';
import { ThemeProvider } from '@/providers/theme-provider';

describe('ThemeToggle', () => {
  it('can be focused and activated from the keyboard', async () => {
    const user = userEvent.setup();
    render(
      <ThemeProvider>
        <ThemeToggle />
      </ThemeProvider>,
    );

    await user.tab();
    const toggle = screen.getByRole('button', {
      name: 'Switch to dark theme',
    });
    expect(toggle).toHaveFocus();

    await user.keyboard('{Enter}');
    expect(
      screen.getByRole('button', { name: 'Switch to light theme' }),
    ).toBeVisible();
    expect(document.documentElement).toHaveClass('dark');
  });
});
