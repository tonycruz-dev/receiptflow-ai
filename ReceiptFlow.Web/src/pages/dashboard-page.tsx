import {
  ArrowRight,
  Bot,
  FileClock,
  //Plus,
  ReceiptText,
  Search,
  Sparkles,
  UploadCloud,
  WalletCards,
} from 'lucide-react';
import { Link } from 'react-router-dom';
import type { DashboardResponse } from '@/api/contracts';
import { getSafeErrorMessage } from '@/api/error-message';
import { mapReceiptSummary } from '@/api/map-receipt-summary';
import { useDashboard } from '@/api/use-dashboard';
import { EmptyState } from '@/components/shared/empty-state';
import { ErrorState } from '@/components/shared/error-state';
import { LoadingSkeleton } from '@/components/shared/loading-skeleton';
//import { PageHeader } from '@/components/shared/page-header';
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
  const greeting = getGreeting();
  const formattedDate = new Intl.DateTimeFormat('en-GB', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
  }).format(new Date());

  return (
    <section className="relative isolate overflow-hidden rounded-3xl border border-primary/15 bg-gradient-to-br from-primary/[0.12] via-card to-accent/40 px-6 py-8 shadow-sm sm:px-8 sm:py-10">
      {/* Decorative background */}
      <div
        aria-hidden="true"
        className="absolute -right-24 -top-24 size-72 rounded-full bg-primary/10 blur-3xl"
      />
      <div
        aria-hidden="true"
        className="absolute -bottom-28 left-1/3 size-64 rounded-full bg-emerald-300/10 blur-3xl"
      />

      <div className="relative flex flex-col justify-between gap-8 lg:flex-row lg:items-center">
        <div className="max-w-2xl">
          <div className="mb-4 flex w-fit items-center gap-2 rounded-full border border-primary/15 bg-background/70 px-3 py-1.5 text-xs font-medium text-primary shadow-sm backdrop-blur">
            <Sparkles className="size-3.5" aria-hidden="true" />
            {formattedDate}
          </div>

          <h1 className="text-3xl font-bold tracking-tight text-foreground sm:text-4xl">
            {greeting}
          </h1>

          <p className="mt-3 max-w-xl text-base leading-7 text-muted-foreground">
            Track your spending, manage receipt documents and ask questions
            grounded in your own purchase history.
          </p>

          <div className="mt-6 flex flex-wrap gap-3">
            <Button asChild size="lg" className="shadow-sm">
              <Link to="/receipts/new">
                <UploadCloud aria-hidden="true" />
                Upload receipt
              </Link>
            </Button>

            <Button asChild size="lg" variant="outline" className="bg-card/70">
              <Link to="/search">
                <Search aria-hidden="true" />
                Search receipts
              </Link>
            </Button>
          </div>
        </div>

        <div className="hidden shrink-0 lg:block">
          <div className="relative grid size-40 place-items-center rounded-[2rem] border border-primary/15 bg-background/60 shadow-lg backdrop-blur">
            <div className="absolute inset-4 rounded-3xl border border-dashed border-primary/20" />
            <ReceiptText
              className="relative size-16 text-primary"
              strokeWidth={1.4}
              aria-hidden="true"
            />
          </div>
        </div>
      </div>
    </section>
  );
}

function getGreeting() {
  const hour = new Date().getHours();

  if (hour < 12) return 'Good morning';
  if (hour < 18) return 'Good afternoon';
  return 'Good evening';
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
    <section aria-labelledby="dashboard-overview-title">
      <div className="mb-4">
        <h2
          id="dashboard-overview-title"
          className="text-xl font-semibold tracking-tight"
        >
          Your overview
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">
          A summary of your receipt activity and recorded spending.
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <SummaryCard
          label="Total receipts"
          value={dashboard.totalReceipts.toLocaleString('en-GB')}
          detail="Saved to your library"
          icon={ReceiptText}
        />

        {spending.length === 0 ? (
          <SummaryCard
            label="Total spending"
            value="—"
            detail="No confirmed spending yet"
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
                  : `Spending · ${total.currency}`
              }
              value={formatCurrency(total.amount, total.currency)}
              detail="Across confirmed receipts"
              icon={WalletCards}
              tone="success"
            />
          ))
        )}

        <SummaryCard
          label="In progress"
          value={dashboard.documentsProcessing.toLocaleString('en-GB')}
          detail="Pending, queued or processing"
          icon={FileClock}
          tone="processing"
        />
      </div>
    </section>
  );
}
function AssistantCallout() {
  return (
    <Card className="group relative overflow-hidden border-primary/20 bg-primary text-primary-foreground shadow-lg shadow-primary/10">
      <div
        aria-hidden="true"
        className="absolute -right-16 -top-20 size-64 rounded-full bg-white/10 blur-2xl transition-transform duration-500 group-hover:scale-110"
      />

      <CardContent className="relative flex flex-col items-start justify-between gap-6 p-6 sm:flex-row sm:items-center sm:p-8">
        <div className="flex items-start gap-4">
          <div className="flex size-12 shrink-0 items-center justify-center rounded-2xl bg-white/15 ring-1 ring-white/20">
            <Bot className="size-6" aria-hidden="true" />
          </div>

          <div>
            <div className="mb-1 flex items-center gap-2">
              <h2 className="text-lg font-semibold">Ask ReceiptFlow AI</h2>
              <Sparkles
                className="size-4 text-emerald-200"
                aria-hidden="true"
              />
            </div>

            <p className="max-w-xl text-sm leading-6 text-primary-foreground/75">
              Ask about purchases, merchants, totals or dates and receive
              answers supported by evidence from your indexed receipts.
            </p>
          </div>
        </div>

        <Button
          asChild
          variant="secondary"
          size="lg"
          className="shrink-0 shadow-sm"
        >
          <Link to="/assistant">
            Open assistant
            <ArrowRight aria-hidden="true" />
          </Link>
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
