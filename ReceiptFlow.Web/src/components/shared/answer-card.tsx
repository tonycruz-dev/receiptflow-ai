import { Bot } from 'lucide-react';
import type { ReactNode } from 'react';
import { Card, CardContent } from '@/components/ui/card';

interface AnswerCardProps {
  children: ReactNode;
  sources?: ReactNode;
}

export function AnswerCard({ children, sources }: AnswerCardProps) {
  return (
    <Card aria-label="Assistant answer">
      <CardContent className="p-5 sm:p-6">
        <div className="flex gap-3">
          <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-accent text-accent-foreground">
            <Bot aria-hidden="true" className="size-5" />
          </span>
          <div className="min-w-0 flex-1">
            <p className="text-sm font-semibold">ReceiptFlow Assistant</p>
            <div className="mt-3 text-sm leading-6 text-foreground">
              {children}
            </div>
          </div>
        </div>
        {sources ? (
          <section
            className="mt-6 border-t pt-5"
            aria-labelledby="sources-title"
          >
            <h3 id="sources-title" className="mb-3 text-sm font-semibold">
              Sources
            </h3>
            <div className="grid gap-3">{sources}</div>
          </section>
        ) : null}
      </CardContent>
    </Card>
  );
}
