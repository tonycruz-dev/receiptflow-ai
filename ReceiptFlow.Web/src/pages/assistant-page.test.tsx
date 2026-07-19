import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { createMockApiClient, renderApp } from '@/test/render-app';

describe('Receipt assistant page', () => {
  it('renders a grounded answer and trusted backend citations', async () => {
    const askReceiptQuestion = vi.fn().mockResolvedValue({
      answer: 'You purchased USB cables for £24.50 [1].',
      sources: [
        {
          citation: 1,
          receiptId: 'receipt-1',
          documentId: 'document-1',
          merchantName: 'Cable Store',
          transactionDate: '2026-07-01T12:00:00Z',
          total: 24.5,
          currency: 'GBP',
        },
      ],
    });
    renderApp('/assistant', createMockApiClient({ askReceiptQuestion }));
    const user = userEvent.setup();
    await screen.findByRole('heading', { name: 'AI receipt assistant' });

    await user.type(
      screen.getByRole('textbox', {
        name: 'Ask a question about your receipts',
      }),
      'What electronics did I purchase?',
    );
    await user.click(screen.getByRole('button', { name: 'Ask' }));

    expect(askReceiptQuestion).toHaveBeenCalledWith(
      { question: 'What electronics did I purchase?' },
      expect.any(AbortSignal),
    );
    expect(
      await screen.findByText('You purchased USB cables for £24.50 [1].'),
    ).toBeVisible();
    expect(screen.getByText('Cable Store')).toBeVisible();
    expect(screen.getByText(/Source \[1\]/)).toBeVisible();
  });

  it('renders an empty-evidence response without invented sources', async () => {
    const askReceiptQuestion = vi.fn().mockResolvedValue({
      answer: 'I could not find this in your receipts.',
      sources: [],
    });
    renderApp('/assistant', createMockApiClient({ askReceiptQuestion }));
    const user = userEvent.setup();
    await screen.findByRole('heading', { name: 'AI receipt assistant' });

    await user.type(
      screen.getByRole('textbox', {
        name: 'Ask a question about your receipts',
      }),
      'Did I buy a telescope?',
    );
    await user.click(screen.getByRole('button', { name: 'Ask' }));

    expect(
      await screen.findByText('I could not find this in your receipts.'),
    ).toBeVisible();
    expect(
      screen.getByText('No supporting receipt evidence was found.'),
    ).toBeVisible();
  });
});
