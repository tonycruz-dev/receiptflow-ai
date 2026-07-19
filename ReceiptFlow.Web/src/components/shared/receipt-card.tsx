import { CalendarDays, FileText, Store } from 'lucide-react';
import { Link } from 'react-router-dom';
import { StatusBadge, type ReceiptStatus } from './status-badge';
import { Card, CardContent } from '@/components/ui/card';
import { formatCurrency } from '@/lib/utils';

export interface ReceiptCardData {
  id: string;
  merchant: string;
  date: string;
  total: number;
  currency?: string;
  status: ReceiptStatus;
  fileName: string;
}

interface ReceiptCardProps {
  receipt: ReceiptCardData;
}

export function ReceiptCard({ receipt }: ReceiptCardProps) {
  return (
    <Card className="transition-shadow hover:shadow-md">
      <CardContent className="p-5">
        <div className="flex items-start justify-between gap-3">
          <div className="flex min-w-0 gap-3">
            <div className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-accent text-accent-foreground">
              <Store aria-hidden="true" className="size-5" />
            </div>
            <div className="min-w-0">
              <Link
                to={`/receipts/${receipt.id}`}
                className="font-semibold hover:text-primary hover:underline"
              >
                {receipt.merchant}
              </Link>
              <p className="mt-0.5 truncate text-sm text-muted-foreground">
                {receipt.fileName}
              </p>
            </div>
          </div>
          <p className="shrink-0 font-bold tabular-nums">
            {formatCurrency(receipt.total, receipt.currency)}
          </p>
        </div>
        <div className="mt-5 flex flex-wrap items-center justify-between gap-3 border-t pt-4">
          <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
            <CalendarDays aria-hidden="true" className="size-3.5" />
            <time dateTime={receipt.date}>
              {new Intl.DateTimeFormat('en-GB', {
                day: 'numeric',
                month: 'short',
                year: 'numeric',
              }).format(new Date(receipt.date))}
            </time>
          </div>
          <StatusBadge status={receipt.status} />
        </div>
      </CardContent>
    </Card>
  );
}

export function ReceiptFileIcon() {
  return <FileText aria-hidden="true" />;
}
