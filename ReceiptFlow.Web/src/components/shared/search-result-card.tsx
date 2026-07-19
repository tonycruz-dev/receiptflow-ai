import { CalendarDays, Tag, WalletCards } from 'lucide-react';
import type { ReceiptSearchMatch } from '@/api/contracts';
import { Card, CardContent } from '@/components/ui/card';
import { formatCurrency } from '@/lib/utils';

interface SearchResultCardProps {
  result: ReceiptSearchMatch;
}

function safeSnippet(content: string) {
  const normalized = content.replace(/\s+/g, ' ').trim();
  return normalized.length > 240 ? `${normalized.slice(0, 240)}…` : normalized;
}

export function SearchResultCard({ result }: SearchResultCardProps) {
  const score = Math.round(
    Math.min(Math.max(result.relevanceScore, 0), 1) * 100,
  );

  return (
    <Card>
      <CardContent className="p-5">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <h2 className="font-semibold">
              {result.merchantName ?? 'Unknown merchant'}
            </h2>
            <div className="mt-2 flex flex-wrap gap-x-4 gap-y-2 text-xs text-muted-foreground">
              {result.transactionDate ? (
                <span className="flex items-center gap-1.5">
                  <CalendarDays aria-hidden="true" className="size-3.5" />
                  {new Intl.DateTimeFormat('en-GB', {
                    dateStyle: 'medium',
                  }).format(new Date(result.transactionDate))}
                </span>
              ) : null}
              {result.category ? (
                <span className="flex items-center gap-1.5">
                  <Tag aria-hidden="true" className="size-3.5" />
                  {result.category}
                </span>
              ) : null}
              {result.total !== null && result.currency ? (
                <span className="flex items-center gap-1.5 font-medium text-foreground">
                  <WalletCards aria-hidden="true" className="size-3.5" />
                  {formatCurrency(result.total, result.currency)}
                </span>
              ) : null}
            </div>
          </div>
          <span className="w-fit rounded-full bg-accent px-2.5 py-1 text-xs font-semibold text-accent-foreground">
            {score}% match
          </span>
        </div>
        <p className="mt-4 text-sm leading-6 text-muted-foreground">
          {safeSnippet(result.content)}
        </p>
      </CardContent>
    </Card>
  );
}
