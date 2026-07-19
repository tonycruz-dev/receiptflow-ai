import { FileText, SquareArrowOutUpRight } from 'lucide-react';
import { Link } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';

interface SourceCitationCardProps {
  receiptId: string;
  title: string;
  excerpt: string;
  reference: string;
}

export function SourceCitationCard({
  receiptId,
  title,
  excerpt,
  reference,
}: SourceCitationCardProps) {
  return (
    <Card>
      <CardContent className="flex gap-3 p-4">
        <div className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-muted text-muted-foreground">
          <FileText aria-hidden="true" className="size-4" />
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex items-start justify-between gap-3">
            <div>
              <p className="font-semibold">{title}</p>
              <p className="mt-0.5 text-xs text-muted-foreground">
                {reference}
              </p>
            </div>
            <Link
              to={`/receipts/${receiptId}`}
              aria-label={`Open source ${title}`}
              className="rounded-md p-1.5 text-muted-foreground hover:bg-muted hover:text-foreground"
            >
              <SquareArrowOutUpRight aria-hidden="true" className="size-4" />
            </Link>
          </div>
          <p className="mt-2 text-sm text-muted-foreground">{excerpt}</p>
        </div>
      </CardContent>
    </Card>
  );
}
