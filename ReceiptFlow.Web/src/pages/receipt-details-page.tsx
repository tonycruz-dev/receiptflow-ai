import { CalendarDays, FileText, Store, WalletCards } from 'lucide-react';
import { useParams } from 'react-router-dom';
import { PageHeader } from '@/components/shared/page-header';
import { StatusBadge } from '@/components/shared/status-badge';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { recentReceiptFixtures } from '@/data/receipt-fixtures';
import { formatCurrency } from '@/lib/utils';

export function Component() {
  const { receiptId } = useParams();
  const receipt =
    recentReceiptFixtures.find((item) => item.id === receiptId) ??
    recentReceiptFixtures[0];

  if (!receipt) return null;

  return (
    <div className="space-y-6">
      <PageHeader
        title={receipt.merchant}
        description="Representative receipt detail layout using local fixture data."
        actions={<StatusBadge status={receipt.status} />}
      />
      <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_22rem]">
        <Card>
          <CardHeader>
            <h2 className="font-semibold">Document preview</h2>
          </CardHeader>
          <CardContent>
            <div className="grid min-h-96 place-items-center rounded-lg border bg-muted/60 text-muted-foreground">
              <div className="text-center">
                <FileText aria-hidden="true" className="mx-auto size-10" />
                <p className="mt-3 text-sm">
                  Preview available after API integration
                </p>
              </div>
            </div>
          </CardContent>
        </Card>
        <Card className="h-fit">
          <CardHeader>
            <h2 className="font-semibold">Receipt summary</h2>
          </CardHeader>
          <CardContent className="space-y-5">
            <Detail icon={Store} label="Merchant" value={receipt.merchant} />
            <Detail
              icon={CalendarDays}
              label="Date"
              value={new Intl.DateTimeFormat('en-GB', {
                dateStyle: 'long',
              }).format(new Date(`${receipt.date}T12:00:00`))}
            />
            <Detail
              icon={WalletCards}
              label="Total"
              value={formatCurrency(receipt.total, receipt.currency)}
            />
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

interface DetailProps {
  icon: typeof Store;
  label: string;
  value: string;
}

function Detail({ icon: Icon, label, value }: DetailProps) {
  return (
    <div className="flex gap-3">
      <Icon
        aria-hidden="true"
        className="mt-0.5 size-4 text-muted-foreground"
      />
      <div>
        <dt className="text-xs font-medium text-muted-foreground">{label}</dt>
        <dd className="mt-0.5 text-sm font-semibold">{value}</dd>
      </div>
    </div>
  );
}
