import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { StrictMode } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { VoiceInputButton } from '@/components/assistant/voice-input-button';
import { createMockApiClient, renderApp } from '@/test/render-app';

interface MockSpeechResultEvent {
  resultIndex: number;
  results: Array<
    Array<{ transcript: string }> & {
      isFinal: boolean;
    }
  >;
}

interface MockSpeechErrorEvent {
  error: string;
}

class MockSpeechRecognition {
  static instances: MockSpeechRecognition[] = [];

  continuous = true;
  interimResults = false;
  lang = '';
  onstart: ((event: Event) => void) | null = null;
  onresult: ((event: MockSpeechResultEvent) => void) | null = null;
  onerror: ((event: MockSpeechErrorEvent) => void) | null = null;
  onend: ((event: Event) => void) | null = null;
  onspeechend: ((event: Event) => void) | null = null;
  start = vi.fn();
  stop = vi.fn();
  abort = vi.fn();

  constructor() {
    MockSpeechRecognition.instances.push(this);
  }

  emitStart() {
    this.onstart?.(new Event('start'));
  }

  emitResult(transcript: string, isFinal: boolean) {
    this.emitResults([{ transcript, isFinal }], 0);
  }

  emitResults(
    results: Array<{ transcript: string; isFinal: boolean }>,
    resultIndex: number,
  ) {
    this.onresult?.({
      resultIndex,
      results: results.map(({ transcript, isFinal }) =>
        Object.assign([{ transcript }], { isFinal }),
      ),
    });
  }

  emitError(error: string) {
    this.onerror?.({ error });
  }

  emitEnd() {
    this.onend?.(new Event('end'));
  }

  emitSpeechEnd() {
    this.onspeechend?.(new Event('speechend'));
  }
}

afterEach(() => {
  MockSpeechRecognition.instances = [];
  Reflect.deleteProperty(window, 'SpeechRecognition');
  Reflect.deleteProperty(window, 'webkitSpeechRecognition');
});

describe('Receipt assistant page', () => {
  it('keeps typed questions functional when voice input is unsupported', async () => {
    const askReceiptQuestion = vi
      .fn()
      .mockResolvedValue({ answer: 'Typed answer.', sources: [] });
    renderApp('/assistant', createMockApiClient({ askReceiptQuestion }));
    const user = userEvent.setup();

    expect(
      await screen.findByText('Voice input is not supported by this browser.'),
    ).toBeVisible();
    const textbox = screen.getByRole('textbox', {
      name: 'Ask a question about your receipts',
    });
    await user.type(textbox, 'What did I buy?');
    await user.click(screen.getByRole('button', { name: 'Ask' }));

    expect(askReceiptQuestion).toHaveBeenCalledWith(
      { question: 'What did I buy?' },
      expect.any(AbortSignal),
    );
  });

  it('starts one continuous recognition session and shows the privacy note', async () => {
    installSpeechRecognition();
    renderApp('/assistant');
    const user = userEvent.setup();

    await user.click(
      await screen.findByRole('button', { name: 'Start voice input' }),
    );

    expect(MockSpeechRecognition.instances).toHaveLength(1);
    const recognition = latestRecognition();
    expect(recognition.start).toHaveBeenCalledOnce();
    expect(recognition.continuous).toBe(true);
    expect(recognition.interimResults).toBe(true);
    expect(recognition.lang).toBe(navigator.language || 'en-GB');
    expect(
      screen.getByText(
        /Your browser processes speech recognition and may use an online recognition service/,
      ),
    ).toBeVisible();
  });

  it('announces interim speech without committing or asking', async () => {
    installSpeechRecognition();
    const askReceiptQuestion = vi.fn();
    renderApp('/assistant', createMockApiClient({ askReceiptQuestion }));
    const user = userEvent.setup();
    await user.click(
      await screen.findByRole('button', { name: 'Start voice input' }),
    );
    const recognition = latestRecognition();

    act(() => {
      recognition.emitStart();
      recognition.emitResult('how much did I spend', false);
    });

    expect(screen.getByText('Listening… how much did I spend')).toBeVisible();
    expect(
      screen.getByRole('textbox', {
        name: 'Ask a question about your receipts',
      }),
    ).toHaveValue('');
    expect(askReceiptQuestion).not.toHaveBeenCalled();
  });

  it('buffers multiple final results without duplicates until explicit stop', async () => {
    installSpeechRecognition();
    const askReceiptQuestion = vi
      .fn()
      .mockResolvedValue({ answer: 'Answer.', sources: [] });
    renderApp('/assistant', createMockApiClient({ askReceiptQuestion }));
    const user = userEvent.setup();
    const textbox = await screen.findByRole('textbox', {
      name: 'Ask a question about your receipts',
    });
    await user.type(textbox, 'How much');
    await user.click(screen.getByRole('button', { name: 'Start voice input' }));
    const recognition = latestRecognition();

    act(() => {
      recognition.emitStart();
      recognition.emitResults(
        [{ transcript: 'did I spend?', isFinal: true }],
        0,
      );
    });

    expect(textbox).toHaveValue('How much');
    expect(askReceiptQuestion).not.toHaveBeenCalled();

    act(() => {
      recognition.emitResults(
        [
          { transcript: 'did I spend?', isFinal: true },
          { transcript: 'at grocery stores?', isFinal: true },
        ],
        1,
      );
      recognition.emitResults(
        [
          { transcript: 'did I spend?', isFinal: true },
          { transcript: 'at grocery stores?', isFinal: true },
        ],
        0,
      );
    });
    expect(textbox).toHaveValue('How much');

    await user.click(
      screen.getByRole('button', { name: 'Finish voice input' }),
    );
    expect(recognition.stop).toHaveBeenCalledOnce();
    act(() => {
      recognition.emitEnd();
    });

    expect(textbox).toHaveValue('How much did I spend? at grocery stores?');
    expect(MockSpeechRecognition.instances).toHaveLength(1);
    expect(askReceiptQuestion).not.toHaveBeenCalled();
    await user.type(textbox, ' last month');
    expect(textbox).toHaveValue(
      'How much did I spend? at grocery stores? last month',
    );
    await user.click(screen.getByRole('button', { name: 'Ask' }));
    expect(askReceiptQuestion).toHaveBeenCalledWith(
      {
        question: 'How much did I spend? at grocery stores? last month',
      },
      expect.any(AbortSignal),
    );
  });

  it('ignores speechend and continues across a pause until Finish is pressed', async () => {
    installSpeechRecognition();
    renderApp('/assistant');
    const user = userEvent.setup();
    await user.click(
      await screen.findByRole('button', { name: 'Start voice input' }),
    );
    const recognition = latestRecognition();
    act(() => {
      recognition.emitStart();
      recognition.emitResult('First sentence.', true);
      recognition.emitSpeechEnd();
    });

    expect(
      screen.getByRole('textbox', {
        name: 'Ask a question about your receipts',
      }),
    ).toHaveValue('');
    expect(
      screen.getByRole('button', { name: 'Finish voice input' }),
    ).toBeVisible();
    expect(recognition.stop).not.toHaveBeenCalled();

    const finish = screen.getByRole('button', { name: 'Finish voice input' });
    expect(finish).toHaveAttribute('aria-pressed', 'true');
    await user.click(finish);

    expect(recognition.stop).toHaveBeenCalledOnce();
    expect(screen.getByText('Processing transcript…')).toBeVisible();
    act(() => {
      recognition.emitResults(
        [
          { transcript: 'First sentence.', isFinal: true },
          { transcript: 'Second sentence.', isFinal: true },
        ],
        1,
      );
      recognition.emitEnd();
    });
    expect(
      screen.getByRole('textbox', {
        name: 'Ask a question about your receipts',
      }),
    ).toHaveValue('First sentence. Second sentence.');
    expect(MockSpeechRecognition.instances).toHaveLength(1);
  });

  it('restarts safely after unexpected onend and preserves buffered speech', async () => {
    installSpeechRecognition();
    const askReceiptQuestion = vi.fn();
    renderApp('/assistant', createMockApiClient({ askReceiptQuestion }));
    const user = userEvent.setup();
    await user.click(
      await screen.findByRole('button', { name: 'Start voice input' }),
    );
    const firstRecognition = latestRecognition();
    act(() => {
      firstRecognition.emitStart();
      firstRecognition.emitResult('First paragraph.', true);
      firstRecognition.emitEnd();
    });

    expect(MockSpeechRecognition.instances).toHaveLength(2);
    const restartedRecognition = latestRecognition();
    expect(restartedRecognition.start).toHaveBeenCalledOnce();
    expect(restartedRecognition.continuous).toBe(true);
    expect(
      screen.getByRole('textbox', {
        name: 'Ask a question about your receipts',
      }),
    ).toHaveValue('');

    act(() => {
      restartedRecognition.emitStart();
      restartedRecognition.emitResults(
        [
          { transcript: 'First paragraph.', isFinal: true },
          { transcript: 'Second paragraph.', isFinal: true },
        ],
        0,
      );
    });
    await user.click(
      screen.getByRole('button', { name: 'Finish voice input' }),
    );
    act(() => {
      restartedRecognition.emitEnd();
    });

    expect(
      screen.getByRole('textbox', {
        name: 'Ask a question about your receipts',
      }),
    ).toHaveValue('First paragraph. Second paragraph.');
    expect(askReceiptQuestion).not.toHaveBeenCalled();
  });

  it('shows a safe retry message when microphone permission is denied', async () => {
    installSpeechRecognition();
    renderApp('/assistant');
    const user = userEvent.setup();
    await user.click(
      await screen.findByRole('button', { name: 'Start voice input' }),
    );

    act(() => {
      const recognition = latestRecognition();
      recognition.emitError('not-allowed');
      recognition.emitEnd();
    });

    expect(
      screen.getByText(
        'Microphone permission was denied. Allow access and try again.',
      ),
    ).toBeVisible();
    expect(
      screen.getByRole('button', { name: 'Try voice input again' }),
    ).toBeVisible();
    expect(MockSpeechRecognition.instances).toHaveLength(1);
  });

  it('aborts recognition when the assistant unmounts', async () => {
    installSpeechRecognition();
    const user = userEvent.setup();
    const { unmount } = renderApp('/assistant');
    await user.click(
      await screen.findByRole('button', { name: 'Start voice input' }),
    );
    const firstRecognition = latestRecognition();
    act(() => {
      firstRecognition.emitEnd();
    });
    const recognition = latestRecognition();

    unmount();

    expect(recognition.abort).toHaveBeenCalledOnce();
    expect(MockSpeechRecognition.instances).toHaveLength(2);
  });

  it('limits appended speech to 1,000 characters', async () => {
    installSpeechRecognition();
    renderApp('/assistant');
    const user = userEvent.setup();
    const textbox = await screen.findByRole('textbox', {
      name: 'Ask a question about your receipts',
    });
    fireEvent.change(textbox, { target: { value: 'a'.repeat(995) } });
    await user.click(screen.getByRole('button', { name: 'Start voice input' }));

    act(() => {
      const recognition = latestRecognition();
      recognition.emitStart();
    });
    await user.click(
      screen.getByRole('button', { name: 'Finish voice input' }),
    );
    act(() => {
      const recognition = latestRecognition();
      recognition.emitResult('total amount', true);
      recognition.emitEnd();
    });

    expect(textbox).toHaveValue(`${'a'.repeat(995)} tota`);
    expect((textbox as HTMLTextAreaElement).value).toHaveLength(1000);
  });

  it('does not create duplicate sessions under React Strict Mode', async () => {
    installSpeechRecognition();
    const user = userEvent.setup();
    render(
      <StrictMode>
        <VoiceInputButton onTranscript={vi.fn()} />
      </StrictMode>,
    );

    await user.dblClick(
      screen.getByRole('button', { name: 'Start voice input' }),
    );

    expect(MockSpeechRecognition.instances).toHaveLength(1);
    expect(latestRecognition().start).toHaveBeenCalledOnce();
  });

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
    const listReceiptDocuments = vi.fn().mockResolvedValue([
      {
        documentId: 'document-1',
        originalFileName: 'northstar-receipt.pdf',
        contentType: 'application/pdf',
        fileSize: 1234,
        uploadedAtUtc: '2026-07-01T12:00:00Z',
        processingStatus: 'Completed',
        hasExtraction: true,
      },
    ]);
    const { router } = renderApp(
      '/assistant',
      createMockApiClient({ askReceiptQuestion, listReceiptDocuments }),
    );
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
    expect(
      screen.getByText('Source file: northstar-receipt.pdf'),
    ).toBeVisible();
    const sourceLink = screen.getByRole('link', { name: 'View receipt' });
    expect(sourceLink).toHaveAttribute('href', '/receipts/receipt-1');

    await user.click(sourceLink);
    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/receipts/receipt-1');
    });
    expect(router.state.location.state).toEqual({ documentId: 'document-1' });
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

function installSpeechRecognition() {
  Object.defineProperty(window, 'SpeechRecognition', {
    configurable: true,
    value: MockSpeechRecognition,
  });
}

function latestRecognition() {
  const recognition = MockSpeechRecognition.instances.at(-1);
  if (!recognition) throw new Error('Speech recognition was not created.');
  return recognition;
}
