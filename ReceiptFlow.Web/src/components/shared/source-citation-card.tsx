import { ArrowRight, FileText } from 'lucide-react';
import { Link } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';

interface SourceCitationCardProps {
  title: string;
  reference: string;
  excerpt?: string;
  receiptId?: string;
  documentId?: string;
}

export function SourceCitationCard({
  title,
  excerpt,
  reference,
  receiptId,
  documentId,
}: SourceCitationCardProps) {
  return (
    <Card>
      <CardContent className="flex gap-3 p-4">
        <div className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-muted text-muted-foreground">
          <FileText aria-hidden="true" className="size-4" />
        </div>
        <div className="min-w-0 flex-1">
          <div>
            <p className="font-semibold">{title}</p>
            <p className="mt-0.5 text-xs text-muted-foreground">{reference}</p>
          </div>
          {excerpt ? (
            <p className="mt-2 text-sm text-muted-foreground">{excerpt}</p>
          ) : null}
          {receiptId ? (
            <Link
              to={`/receipts/${receiptId}`}
              state={documentId ? { documentId } : undefined}
              className="mt-3 inline-flex items-center gap-1.5 rounded-sm text-sm font-semibold text-primary underline-offset-4 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
            >
              View receipt
              <ArrowRight aria-hidden="true" className="size-3.5" />
            </Link>
          ) : null}
        </div>
      </CardContent>
    </Card>
  );
}
