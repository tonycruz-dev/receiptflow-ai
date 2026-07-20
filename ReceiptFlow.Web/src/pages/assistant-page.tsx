import { useMutation } from '@tanstack/react-query';
import {
  ArrowUp,
  Bot,
  FileSearch,
  LoaderCircle,
  MessageCircleQuestion,
  ShieldCheck,
  Sparkles,
} from 'lucide-react';
import {
  useEffect,
  useRef,
  useState,
  type Dispatch,
  type SetStateAction,
  type SyntheticEvent,
} from 'react';
import type {
  AskReceiptQuestionRequest,
  ReceiptAnswerSource,
} from '@/api/contracts';
import { getSafeErrorMessage } from '@/api/error-message';
import { useReceiptDocuments } from '@/api/use-receipt';
import { VoiceInputButton } from '@/components/assistant/voice-input-button';
import { AnswerCard } from '@/components/shared/answer-card';
import { ErrorState } from '@/components/shared/error-state';
import { LoadingSkeleton } from '@/components/shared/loading-skeleton';
import { SourceCitationCard } from '@/components/shared/source-citation-card';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { formatCurrency } from '@/lib/utils';
import { useAuth } from '@/providers/use-auth';

interface AskVariables extends AskReceiptQuestionRequest {
  signal: AbortSignal;
}

const suggestedQuestions = [
  'What electronics have I purchased?',
  'How much have I spent in total?',
  'Which receipts include delivery charges?',
  'What are my most recent purchases?',
];

export function Component() {
  const { apiClient } = useAuth();
  const [question, setQuestion] = useState('');
  const controllerRef = useRef<AbortController | null>(null);
  const textareaRef = useRef<HTMLTextAreaElement | null>(null);

  const assistant = useMutation({
    mutationFn: ({ question: submittedQuestion, signal }: AskVariables) =>
      apiClient.askReceiptQuestion(
        {
          question: submittedQuestion,
        },
        signal,
      ),
    onSettled: () => {
      controllerRef.current = null;
    },
  });

  useEffect(
    () => () => {
      controllerRef.current?.abort();
    },
    [],
  );

  function submitQuestion(submittedQuestion: string) {
    const trimmedQuestion = submittedQuestion.trim();

    if (!trimmedQuestion || assistant.isPending) return;

    controllerRef.current?.abort();

    const controller = new AbortController();
    controllerRef.current = controller;

    assistant.mutate({
      question: trimmedQuestion,
      signal: controller.signal,
    });
  }

  function handleSubmit(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault();
    submitQuestion(question);
  }

  function selectSuggestedQuestion(suggestion: string) {
    setQuestion(suggestion);
    textareaRef.current?.focus();
  }

  return (
    <div className="mx-auto max-w-6xl space-y-8">
      <AssistantPageHeader />

      <Card className="overflow-hidden rounded-3xl border-border/70 shadow-sm">
        {assistant.isPending ? (
          <div
            className="h-1 overflow-hidden bg-primary/10"
            role="status"
            aria-label="Preparing an answer"
          >
            <div className="h-full w-1/3 animate-pulse rounded-full bg-primary" />
          </div>
        ) : null}

        <div className="border-b bg-muted/20 px-5 py-4 sm:px-6">
          <div className="flex items-center gap-3">
            <div className="flex size-10 items-center justify-center rounded-xl bg-primary/10 text-primary">
              <Bot className="size-5" aria-hidden="true" />
            </div>

            <div>
              <h2 className="text-sm font-semibold">ReceiptFlow Assistant</h2>
              <p className="text-xs text-muted-foreground">
                Answers are grounded in your indexed receipt evidence
              </p>
            </div>

            <span className="ml-auto flex items-center gap-1.5 rounded-full border border-emerald-500/20 bg-emerald-500/10 px-2.5 py-1 text-xs font-medium text-emerald-700 dark:text-emerald-300">
              <span className="size-1.5 rounded-full bg-emerald-500" />
              Ready
            </span>
          </div>
        </div>

        <CardContent className="p-0">
          <div className="min-h-[25rem] p-5 sm:p-7">
            <AssistantResponse
              assistant={assistant}
              onSuggestion={selectSuggestedQuestion}
            />
          </div>

          <AssistantComposer
            question={question}
            isPending={assistant.isPending}
            textareaRef={textareaRef}
            onQuestionChange={setQuestion}
            onSubmit={handleSubmit}
          />
        </CardContent>
      </Card>

      <AssistantPrivacyNotice />
    </div>
  );
}

function AssistantPageHeader() {
  return (
    <header className="relative isolate overflow-hidden rounded-3xl border border-primary/15 bg-gradient-to-br from-primary/[0.10] via-card to-accent/30 px-6 py-8 shadow-sm sm:px-8 sm:py-10">
      <div
        aria-hidden="true"
        className="absolute -right-24 -top-24 size-72 rounded-full bg-primary/10 blur-3xl"
      />

      <div
        aria-hidden="true"
        className="absolute -bottom-24 left-1/3 size-60 rounded-full bg-emerald-300/10 blur-3xl"
      />

      <div className="relative flex items-start gap-4">
        <div className="hidden size-14 shrink-0 items-center justify-center rounded-2xl bg-primary text-primary-foreground shadow-md shadow-primary/20 sm:flex">
          <Bot className="size-7" aria-hidden="true" />
        </div>

        <div>
          <div className="mb-2 flex w-fit items-center gap-2 rounded-full border border-primary/15 bg-background/70 px-3 py-1 text-xs font-medium text-primary backdrop-blur">
            <Sparkles className="size-3.5" aria-hidden="true" />
            Grounded receipt intelligence
          </div>

          <h1 className="text-3xl font-bold tracking-tight sm:text-4xl">
            AI receipt assistant
          </h1>

          <p className="mt-2 max-w-2xl text-sm leading-6 text-muted-foreground sm:text-base">
            Ask questions about your purchases, spending and merchants. Every
            answer is grounded in your indexed receipts and linked to its
            supporting evidence.
          </p>
        </div>
      </div>
    </header>
  );
}

interface AssistantResponseProps {
  assistant: ReturnType<typeof useReceiptAssistantMutation>;
  onSuggestion: (suggestion: string) => void;
}

/*
 * This helper is only used to infer the mutation type.
 * It is not called at runtime.
 */
// eslint-disable-next-line @typescript-eslint/no-unused-vars
function useReceiptAssistantMutation() {
  const { apiClient } = useAuth();

  return useMutation({
    mutationFn: ({ question, signal }: AskVariables) =>
      apiClient.askReceiptQuestion({ question }, signal),
  });
}

function AssistantResponse({
  assistant,
  onSuggestion,
}: AssistantResponseProps) {
  if (assistant.isPending) {
    return <AssistantLoading />;
  }

  if (assistant.isError) {
    return (
      <div className="py-6">
        <ErrorState
          title="Assistant unavailable"
          description={getSafeErrorMessage(
            assistant.error,
            'The receipt assistant is temporarily unavailable. Please try again.',
          )}
          onAction={() => {
            assistant.reset();
          }}
        />
      </div>
    );
  }

  if (assistant.data) {
    return (
      <AnswerCard
        sources={
          assistant.data.sources.length > 0 ? (
            <div className="grid gap-3">
              {assistant.data.sources.map((source) => (
                <AssistantSource
                  key={`${source.documentId}-${source.citation.toString()}`}
                  source={source}
                />
              ))}
            </div>
          ) : (
            <div className="rounded-xl border bg-muted/20 p-4">
              <p className="text-sm text-muted-foreground">
                No supporting receipt evidence was found.
              </p>
            </div>
          )
        }
      >
        <p className="whitespace-pre-wrap leading-7">{assistant.data.answer}</p>
      </AnswerCard>
    );
  }

  return <AssistantWelcome onSuggestion={onSuggestion} />;
}

function AssistantWelcome({
  onSuggestion,
}: {
  onSuggestion: (suggestion: string) => void;
}) {
  return (
    <div className="flex min-h-[21rem] flex-col items-center justify-center text-center">
      <div className="flex size-16 items-center justify-center rounded-2xl bg-primary/10 text-primary">
        <MessageCircleQuestion className="size-8" aria-hidden="true" />
      </div>

      <h2 className="mt-5 text-xl font-semibold tracking-tight">
        What would you like to know?
      </h2>

      <p className="mt-2 max-w-xl text-sm leading-6 text-muted-foreground">
        Ask about products, merchants, dates, spending or patterns across your
        confirmed receipt collection.
      </p>

      <div className="mt-7 w-full max-w-2xl">
        <p className="mb-3 text-xs font-medium uppercase tracking-wider text-muted-foreground">
          Suggested questions
        </p>

        <div className="grid gap-2 sm:grid-cols-2">
          {suggestedQuestions.map((suggestion) => (
            <Button
              key={suggestion}
              type="button"
              variant="outline"
              className="h-auto justify-start whitespace-normal rounded-xl bg-background px-4 py-3 text-left"
              onClick={() => {
                onSuggestion(suggestion);
              }}
            >
              <FileSearch
                className="size-4 shrink-0 text-primary"
                aria-hidden="true"
              />
              {suggestion}
            </Button>
          ))}
        </div>
      </div>
    </div>
  );
}

function AssistantLoading() {
  return (
    <div
      className="mx-auto max-w-3xl py-6"
      role="status"
      aria-label="Assistant is preparing an answer"
      aria-busy="true"
    >
      <div className="mb-5 flex items-center gap-3">
        <div className="flex size-10 items-center justify-center rounded-xl bg-primary/10 text-primary">
          <LoaderCircle
            className="size-5 animate-spin motion-reduce:animate-none"
            aria-hidden="true"
          />
        </div>

        <div>
          <p className="text-sm font-semibold">
            Reviewing your receipt evidence
          </p>
          <p className="mt-0.5 text-xs text-muted-foreground">
            Searching your documents and preparing a grounded answer…
          </p>
        </div>
      </div>

      <Card className="rounded-2xl border-border/70 p-5">
        <LoadingSkeleton lines={5} />
      </Card>
    </div>
  );
}

interface AssistantComposerProps {
  question: string;
  isPending: boolean;
  textareaRef: React.RefObject<HTMLTextAreaElement | null>;
  onQuestionChange: Dispatch<SetStateAction<string>>;
  onSubmit: (event: SyntheticEvent<HTMLFormElement>) => void;
}

function AssistantComposer({
  question,
  isPending,
  textareaRef,
  onQuestionChange,
  onSubmit,
}: AssistantComposerProps) {
  function appendTranscript(transcript: string) {
    onQuestionChange((current) =>
      appendQuestionText(current, transcript, maximumQuestionLength),
    );
    textareaRef.current?.focus();
  }

  return (
    <div className="border-t bg-muted/15 p-4 sm:p-5">
      <form
        onSubmit={onSubmit}
        className="rounded-2xl border bg-background p-3 shadow-sm transition focus-within:border-primary focus-within:ring-4 focus-within:ring-primary/10"
      >
        <label htmlFor="assistant-question" className="sr-only">
          Ask a question about your receipts
        </label>

        <textarea
          ref={textareaRef}
          id="assistant-question"
          value={question}
          onChange={(event) => {
            onQuestionChange(event.target.value);
          }}
          maxLength={maximumQuestionLength}
          rows={3}
          disabled={isPending}
          placeholder="For example: What electronics did I purchase and how much did I spend?"
          className="w-full resize-none border-0 bg-transparent p-2 text-sm leading-6 outline-none placeholder:text-muted-foreground disabled:cursor-not-allowed disabled:opacity-60"
        />

        <div className="flex flex-col gap-3 border-t px-1 pt-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <p className="flex items-center gap-1.5 text-xs text-muted-foreground">
              <Bot className="size-3.5" aria-hidden="true" />
              Answers use your indexed receipt evidence
            </p>

            <p className="mt-1 text-xs text-muted-foreground">
              {question.length.toLocaleString('en-GB')} / 1,000 characters
            </p>
          </div>

          <div className="flex w-full min-w-0 flex-col gap-2 sm:w-auto sm:flex-row sm:items-start">
            <VoiceInputButton
              disabled={isPending}
              onTranscript={appendTranscript}
            />
            <Button
              type="submit"
              disabled={!question.trim() || isPending}
              className="h-10 w-full min-w-24 gap-2 rounded-md bg-primary px-4 font-semibold text-primary-foreground hover:bg-primary/90 focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2 disabled:bg-primary/50 disabled:text-primary-foreground/80 [&_svg]:size-4 sm:w-auto"
            >
              {isPending ? (
                <LoaderCircle
                  className="animate-spin motion-reduce:animate-none"
                  aria-hidden="true"
                />
              ) : (
                <ArrowUp aria-hidden="true" />
              )}

              {isPending ? 'Thinking…' : 'Ask'}
            </Button>
          </div>
        </div>
      </form>
    </div>
  );
}

const maximumQuestionLength = 1000;

function appendQuestionText(
  current: string,
  transcript: string,
  limit: number,
) {
  const separator = current.length > 0 && !current.endsWith(' ') ? ' ' : '';
  return `${current}${separator}${transcript}`.slice(0, limit);
}

function AssistantPrivacyNotice() {
  return (
    <div className="flex items-start gap-3 rounded-2xl border border-primary/15 bg-primary/5 p-4">
      <ShieldCheck
        className="mt-0.5 size-5 shrink-0 text-primary"
        aria-hidden="true"
      />

      <div>
        <p className="text-sm font-medium">Private to your account</p>
        <p className="mt-1 text-xs leading-5 text-muted-foreground">
          ReceiptFlow restricts retrieval to receipts belonging to your
          authenticated account. Always verify important financial information
          against the linked source receipt.
        </p>
      </div>
    </div>
  );
}

function AssistantSource({ source }: { source: ReceiptAnswerSource }) {
  const documents = useReceiptDocuments(source.receiptId);

  const fileName = documents.data?.find(
    (document) => document.documentId === source.documentId,
  )?.originalFileName;

  return (
    <SourceCitationCard
      title={
        source.merchantName ?? `Receipt source ${source.citation.toString()}`
      }
      reference={formatSourceReference(source)}
      {...(fileName
        ? {
            excerpt: `Source file: ${fileName}`,
          }
        : {})}
      receiptId={source.receiptId}
      documentId={source.documentId}
    />
  );
}

function formatSourceReference(source: {
  citation: number;
  transactionDate: string | null;
  total: number | null;
  currency: string | null;
}) {
  const details = [`Source [${source.citation.toString()}]`];

  if (source.transactionDate) {
    details.push(
      new Intl.DateTimeFormat('en-GB', {
        dateStyle: 'medium',
      }).format(new Date(source.transactionDate)),
    );
  }

  if (source.total !== null && source.currency) {
    details.push(formatCurrency(source.total, source.currency));
  }

  return details.join(' · ');
}
