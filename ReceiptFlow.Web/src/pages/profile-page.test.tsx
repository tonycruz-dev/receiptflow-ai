import { screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { createMockApiClient, renderApp } from '@/test/render-app';

describe('Profile page', () => {
  it('renders the authenticated /api/auth/me user', async () => {
    const getCurrentUser = vi.fn().mockResolvedValue({
      userId: 'keycloak-user-42',
      username: 'bob',
      email: 'bob@receiptflow.test',
    });
    renderApp('/profile', createMockApiClient({ getCurrentUser }));

    expect(await screen.findByText('bob@receiptflow.test')).toBeVisible();
    expect(screen.getByText('keycloak-user-42')).toBeVisible();
    expect(getCurrentUser).toHaveBeenCalled();
  });
});
