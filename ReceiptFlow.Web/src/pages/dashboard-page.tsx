import { Bot, FileClock, Plus, ReceiptText, WalletCards } from 'lucide-react';
import { Link } from 'react-router-dom';
import type { DashboardResponse } from '@/api/contracts';
import { getSafeErrorMessage } from '@/api/error-message';
import { mapReceiptSummary } from '@/api/map-receipt-summary';
import { useDashboard } from '@/api/use-dashboard';
import { EmptyState } from '@/components/shared/empty-state';
import { ErrorState } from '@/components/shared/error-state';
import { LoadingSkeleton } from '@/components/shared/loading-skeleton';
import { PageHeader } from '@/components/shared/page-header';
import { ReceiptCard } from '@/components/shared/receipt-card';
import { SummaryCard } from '@/components/shared/summary-card';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { formatCurrency } from '@/lib/utils';

export function Component() {
  const dashboard = useDashboard();

  return (
    <div className="space-y-8">
      <DashboardHeader />
      {dashboard.isLoading ? (
        <DashboardLoading />
      ) : dashboard.isError ? (
        <ErrorState
          title="Dashboard unavailable"
          description={getSafeErrorMessage(dashboard.error)}
          onAction={() => {
            void dashboard.refetch();
          }}
        />
      ) : dashboard.data ? (
        <DashboardContent dashboard={dashboard.data} />
      ) : null}
    </div>
  );
}

function DashboardHeader() {
  return (
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
  );
}

function DashboardContent({ dashboard }: { dashboard: DashboardResponse }) {
  return (
    <>
      <DashboardSummary dashboard={dashboard} />
      <AssistantCallout />
      <RecentReceipts dashboard={dashboard} />
    </>
  );
}

function DashboardSummary({ dashboard }: { dashboard: DashboardResponse }) {
  const spending = dashboard.spendingByCurrency;

  return (
    <section
      aria-label="Receipt overview"
      className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4"
    >
      <SummaryCard
        label="Total receipts"
        value={dashboard.totalReceipts.toLocaleString('en-GB')}
        detail="Your saved receipts"
        icon={ReceiptText}
      />
      {spending.length === 0 ? (
        <SummaryCard
          label="Total spending"
          value="—"
          detail="No recorded spending"
          icon={WalletCards}
          tone="success"
        />
      ) : (
        spending.map((total) => (
          <SummaryCard
            key={total.currency}
            label={
              spending.length === 1
                ? 'Total spending'
                : `Total spending · ${total.currency}`
            }
            value={formatCurrency(total.amount, total.currency)}
            detail="Across your receipts"
            icon={WalletCards}
            tone="success"
          />
        ))
      )}
      <SummaryCard
        label="Documents processing"
        value={dashboard.documentsProcessing.toLocaleString('en-GB')}
        detail="Pending, queued or processing"
        icon={FileClock}
        tone="processing"
      />
    </section>
  );
}

function AssistantCallout() {
  return (
    <Card className="overflow-hidden border-primary/20 bg-accent/45">
      <CardContent className="flex flex-col items-start justify-between gap-5 p-6 sm:flex-row sm:items-center">
        <div className="flex items-start gap-4">
          <div className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-primary text-primary-foreground">
            <Bot aria-hidden="true" />
          </div>
          <div>
            <h2 className="font-semibold">Ask your receipts</h2>
            <p className="mt-1 max-w-xl text-sm text-muted-foreground">
              Find spending details and supporting documents through a grounded
              receipt assistant.
            </p>
          </div>
        </div>
        <Button asChild variant="outline" className="bg-card">
          <Link to="/assistant">Open assistant</Link>
        </Button>
      </CardContent>
    </Card>
  );
}

function RecentReceipts({ dashboard }: { dashboard: DashboardResponse }) {
  return (
    <section aria-labelledby="recent-receipts-title">
      <div className="mb-4 flex items-center justify-between gap-4">
        <div>
          <h2 id="recent-receipts-title" className="text-lg font-semibold">
            Recent receipts
          </h2>
          <p className="text-sm text-muted-foreground">
            Your five most recent receipts.
          </p>
        </div>
        <Button asChild variant="ghost" size="sm">
          <Link to="/receipts">View all</Link>
        </Button>
      </div>
      {dashboard.recentReceipts.length === 0 ? (
        <EmptyState
          icon={ReceiptText}
          title="No receipts yet"
          description="Upload your first receipt to start tracking documents and spending."
          action={
            <Button asChild>
              <Link to="/receipts/new">Upload receipt</Link>
            </Button>
          }
        />
      ) : (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {dashboard.recentReceipts.map((receipt) => (
            <ReceiptCard
              key={receipt.receiptId}
              receipt={mapReceiptSummary(receipt)}
            />
          ))}
        </div>
      )}
    </section>
  );
}

function DashboardLoading() {
  return (
    <div className="space-y-6" aria-label="Loading dashboard">
      <div className="grid gap-4 sm:grid-cols-3">
        {Array.from({ length: 3 }, (_, index) => (
          <Card key={index}>
            <CardContent className="p-5">
              <LoadingSkeleton lines={3} />
            </CardContent>
          </Card>
        ))}
      </div>
      <Card>
        <CardContent className="p-6">
          <LoadingSkeleton lines={4} />
        </CardContent>
      </Card>
    </div>
  );
}
