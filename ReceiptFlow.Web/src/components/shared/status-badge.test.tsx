import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { receiptStatuses, StatusBadge } from '@/components/shared/status-badge';

describe('StatusBadge', () => {
  it.each(receiptStatuses)('renders the %s status', (status) => {
    render(<StatusBadge status={status} />);
    expect(screen.getByText(status)).toBeVisible();
  });
});
