import { Bot, FileClock, Plus, ReceiptText, WalletCards } from 'lucide-react';
import { Link } from 'react-router-dom';
import { PageHeader } from '@/components/shared/page-header';
import { ReceiptCard } from '@/components/shared/receipt-card';
import { SummaryCard } from '@/components/shared/summary-card';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  dashboardFixture,
  recentReceiptFixtures,
} from '@/data/receipt-fixtures';
import { formatCurrency } from '@/lib/utils';

export function Component() {
  return (
    <div className="space-y-8">
      <PageHeader
        title="Good morning"
        description="A clear view of your receipts, spending and documents in progress."
        actions={
          <Button asChild>
            <Link to="/receipts/new">
              <Plus aria-hidden="true" />
              Upload receipt
            </Link>
          </Button>
        }
      />

      <section
        aria-label="Receipt overview"
        className="grid gap-4 sm:grid-cols-3"
      >
        <SummaryCard
          label="Total receipts"
          value={dashboardFixture.totalReceipts.toLocaleString('en-GB')}
          detail="Visual development fixture"
          icon={ReceiptText}
        />
        <SummaryCard
          label="Total spending"
          value={formatCurrency(dashboardFixture.totalSpending)}
          detail="Across completed receipts"
          icon={WalletCards}
          tone="success"
        />
        <SummaryCard
          label="Documents processing"
          value={dashboardFixture.processingDocuments.toString()}
          detail="Awaiting extraction or review"
          icon={FileClock}
          tone="processing"
        />
      </section>

      <Card className="overflow-hidden border-primary/20 bg-accent/45">
        <CardContent className="flex flex-col items-start justify-between gap-5 p-6 sm:flex-row sm:items-center">
          <div className="flex items-start gap-4">
            <div className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-primary text-primary-foreground">
              <Bot aria-hidden="true" />
            </div>
            <div>
              <h2 className="font-semibold">Ask your receipts</h2>
              <p className="mt-1 max-w-xl text-sm text-muted-foreground">
                Find spending details and supporting documents through a
                grounded receipt assistant.
              </p>
            </div>
          </div>
          <Button asChild variant="outline" className="bg-card">
            <Link to="/assistant">Open assistant</Link>
          </Button>
        </CardContent>
      </Card>

      <section aria-labelledby="recent-receipts-title">
        <div className="mb-4 flex items-center justify-between gap-4">
          <div>
            <h2 id="recent-receipts-title" className="text-lg font-semibold">
              Recent receipts
            </h2>
            <p className="text-sm text-muted-foreground">
              Local fixture data for layout development.
            </p>
          </div>
          <Button asChild variant="ghost" size="sm">
            <Link to="/receipts">View all</Link>
          </Button>
        </div>
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {recentReceiptFixtures.map((receipt) => (
            <ReceiptCard key={receipt.id} receipt={receipt} />
          ))}
        </div>
      </section>
    </div>
  );
}
