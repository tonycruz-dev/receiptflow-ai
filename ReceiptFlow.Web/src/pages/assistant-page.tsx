import { ArrowUp, Bot, Sparkles } from 'lucide-react';
import { useState, type SyntheticEvent } from 'react';
import { AnswerCard } from '@/components/shared/answer-card';
import { EmptyState } from '@/components/shared/empty-state';
import { LoadingSkeleton } from '@/components/shared/loading-skeleton';
import { PageHeader } from '@/components/shared/page-header';
import { SourceCitationCard } from '@/components/shared/source-citation-card';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';

interface AssistantAnswer {
  text: string;
  sources: Array<{
    receiptId: string;
    title: string;
    excerpt: string;
    reference: string;
  }>;
}

export function Component() {
  const [question, setQuestion] = useState('');
  const [isLoading] = useState(false);
  const [answer] = useState<AssistantAnswer | null>(null);

  function handleSubmit(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault();
    // Intentionally no request until the assistant API hook is introduced.
  }

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <PageHeader
        title="AI receipt assistant"
        description="Ask questions grounded in your uploaded receipts and their source documents."
      />

      {isLoading ? (
        <Card aria-label="Assistant is preparing an answer">
          <CardContent className="p-6">
            <LoadingSkeleton lines={4} />
          </CardContent>
        </Card>
      ) : answer ? (
        <AnswerCard
          sources={answer.sources.map((source) => (
            <SourceCitationCard key={source.receiptId} {...source} />
          ))}
        >
          <p>{answer.text}</p>
        </AnswerCard>
      ) : (
        <EmptyState
          icon={Sparkles}
          title="What would you like to know?"
          description="Ask about merchants, totals, dates or patterns. Answers and supporting receipt citations will appear here after the assistant service is connected."
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
          rows={3}
          placeholder="For example: How much did I spend on travel last month?"
          className="w-full resize-none rounded-lg border-0 bg-transparent p-2 text-sm placeholder:text-muted-foreground focus-visible:ring-0 focus-visible:ring-offset-0"
        />
        <div className="flex items-center justify-between gap-3 border-t px-1 pt-3">
          <p className="flex items-center gap-1.5 text-xs text-muted-foreground">
            <Bot aria-hidden="true" className="size-3.5" />
            API connection coming next
          </p>
          <Button type="submit" size="sm" disabled>
            Ask
            <ArrowUp aria-hidden="true" />
          </Button>
        </div>
      </form>
    </div>
  );
}
