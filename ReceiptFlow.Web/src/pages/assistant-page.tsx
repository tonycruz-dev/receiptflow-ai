import { useMutation } from '@tanstack/react-query';
import { ArrowUp, Bot, Sparkles } from 'lucide-react';
import { useEffect, useRef, useState, type SyntheticEvent } from 'react';
import type {
  AskReceiptQuestionRequest,
  ReceiptAnswerSource,
} from '@/api/contracts';
import { getSafeErrorMessage } from '@/api/error-message';
import { useReceiptDocuments } from '@/api/use-receipt';
import { AnswerCard } from '@/components/shared/answer-card';
import { EmptyState } from '@/components/shared/empty-state';
import { ErrorState } from '@/components/shared/error-state';
import { LoadingSkeleton } from '@/components/shared/loading-skeleton';
import { PageHeader } from '@/components/shared/page-header';
import { SourceCitationCard } from '@/components/shared/source-citation-card';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { formatCurrency } from '@/lib/utils';
import { useAuth } from '@/providers/use-auth';

interface AskVariables extends AskReceiptQuestionRequest {
  signal: AbortSignal;
}

export function Component() {
  const { apiClient } = useAuth();
  const [question, setQuestion] = useState('');
  const controllerRef = useRef<AbortController | null>(null);
  const assistant = useMutation({
    mutationFn: ({ question: submittedQuestion, signal }: AskVariables) =>
      apiClient.askReceiptQuestion({ question: submittedQuestion }, signal),
  });

  useEffect(
    () => () => {
      controllerRef.current?.abort();
    },
    [],
  );

  function handleSubmit(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault();
    const submittedQuestion = question.trim();
    if (!submittedQuestion) return;

    controllerRef.current?.abort();
    const controller = new AbortController();
    controllerRef.current = controller;
    assistant.mutate({
      question: submittedQuestion,
      signal: controller.signal,
    });
  }

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <PageHeader
        title="AI receipt assistant"
        description="Ask questions grounded in your uploaded receipts and their source documents."
      />

      {assistant.isPending ? (
        <Card aria-label="Assistant is preparing an answer">
          <CardContent className="p-6">
            <div className="mb-4 flex items-center gap-2 text-sm font-medium">
              <Bot aria-hidden="true" className="size-4 text-primary" />
              Reviewing your receipt evidence…
            </div>
            <LoadingSkeleton lines={4} />
          </CardContent>
        </Card>
      ) : assistant.isError ? (
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
      ) : assistant.data ? (
        <AnswerCard
          sources={
            assistant.data.sources.length > 0 ? (
              assistant.data.sources.map((source) => (
                <AssistantSource
                  key={`${source.documentId}-${source.citation.toString()}`}
                  source={source}
                />
              ))
            ) : (
              <p className="text-sm text-muted-foreground">
                No supporting receipt evidence was found.
              </p>
            )
          }
        >
          <p className="whitespace-pre-wrap">{assistant.data.answer}</p>
        </AnswerCard>
      ) : (
        <EmptyState
          icon={Sparkles}
          title="What would you like to know?"
          description="Ask about merchants, totals, dates or patterns. Answers are grounded in your receipts and include trusted source citations when evidence is available."
        />
      )}

      <form
        onSubmit={handleSubmit}
        className="rounded-xl border bg-card p-3 shadow-sm"
      >
        <label htmlFor="assistant-question" className="sr-only">
          Ask a question about your receipts
        </label>
        <textarea
          id="assistant-question"
          value={question}
          onChange={(event) => {
            setQuestion(event.target.value);
          }}
          maxLength={1000}
          rows={3}
          placeholder="For example: What electronics did I purchase and how much did I spend?"
          className="w-full resize-none rounded-lg border-0 bg-transparent p-2 text-sm placeholder:text-muted-foreground focus-visible:ring-0 focus-visible:ring-offset-0"
        />
        <div className="flex items-center justify-between gap-3 border-t px-1 pt-3">
          <p className="flex items-center gap-1.5 text-xs text-muted-foreground">
            <Bot aria-hidden="true" className="size-3.5" />
            Answers use your indexed receipt evidence
          </p>
          <Button
            type="submit"
            size="sm"
            disabled={!question.trim() || assistant.isPending}
          >
            Ask
            <ArrowUp aria-hidden="true" />
          </Button>
        </div>
      </form>
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
      {...(fileName ? { excerpt: `Source file: ${fileName}` } : {})}
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
      new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium' }).format(
        new Date(source.transactionDate),
      ),
    );
  }
  if (source.total !== null && source.currency) {
    details.push(formatCurrency(source.total, source.currency));
  }
  return details.join(' · ');
}
