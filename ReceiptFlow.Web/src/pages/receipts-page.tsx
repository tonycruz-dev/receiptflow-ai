import {
  ChevronLeft,
  ChevronRight,
  FileStack,
  Plus,
  ReceiptText,
  Sparkles,
  UploadCloud,
} from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { getSafeErrorMessage } from '@/api/error-message';
import { mapReceiptSummary } from '@/api/map-receipt-summary';
import { useReceipts } from '@/api/use-receipts';
import { EmptyState } from '@/components/shared/empty-state';
import { ErrorState } from '@/components/shared/error-state';
import { LoadingSkeleton } from '@/components/shared/loading-skeleton';
import { ReceiptCard } from '@/components/shared/receipt-card';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';

const pageSize = 12;

export function Component() {
  const [page, setPage] = useState(1);
  const receipts = useReceipts(page, pageSize);

  const totalPages = receipts.data
    ? Math.max(1, Math.ceil(receipts.data.total / receipts.data.pageSize))
    : 1;

  return (
    <div className="space-y-8">
      <ReceiptLibraryHeader totalReceipts={receipts.data?.total} />

      <section
        aria-labelledby="receipt-library-title"
        className="relative overflow-hidden rounded-3xl border bg-card/60 shadow-sm"
      >
        {receipts.isFetching && !receipts.isLoading ? (
          <div
            className="absolute inset-x-0 top-0 h-1 overflow-hidden bg-primary/10"
            role="status"
            aria-label="Refreshing receipts"
          >
            <div className="h-full w-1/3 animate-pulse rounded-full bg-primary" />
          </div>
        ) : null}

        <div className="flex flex-col gap-3 border-b bg-muted/20 px-5 py-5 sm:flex-row sm:items-center sm:justify-between sm:px-6">
          <div>
            <div className="flex items-center gap-2">
              <FileStack className="size-5 text-primary" aria-hidden="true" />
              <h2
                id="receipt-library-title"
                className="text-lg font-semibold tracking-tight"
              >
                Receipt library
              </h2>
            </div>

            <p className="mt-1 text-sm text-muted-foreground">
              Browse your uploaded receipts and monitor document processing.
            </p>
          </div>

          {receipts.data && receipts.data.total > 0 ? (
            <div className="w-fit rounded-full border bg-background px-3 py-1.5 text-sm font-medium shadow-sm">
              {receipts.data.total.toLocaleString('en-GB')}{' '}
              {receipts.data.total === 1 ? 'receipt' : 'receipts'}
            </div>
          ) : null}
        </div>

        <div className="p-5 sm:p-6">
          {receipts.isLoading ? (
            <ReceiptListLoading />
          ) : receipts.isError ? (
            <ErrorState
              title="Receipts unavailable"
              description={getSafeErrorMessage(receipts.error)}
              onAction={() => {
                void receipts.refetch();
              }}
            />
          ) : receipts.data?.items.length === 0 ? (
            <ReceiptEmptyState />
          ) : receipts.data ? (
            <div className="space-y-8">
              <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-3">
                {receipts.data.items.map((receipt) => (
                  <ReceiptCard
                    key={receipt.receiptId}
                    receipt={mapReceiptSummary(receipt)}
                  />
                ))}
              </div>

              <ReceiptPagination
                page={page}
                totalPages={totalPages}
                totalReceipts={receipts.data.total}
                isFetching={receipts.isFetching}
                onPrevious={() => {
                  setPage((current) => Math.max(1, current - 1));
                  window.scrollTo({ top: 0, behavior: 'smooth' });
                }}
                onNext={() => {
                  setPage((current) => Math.min(totalPages, current + 1));
                  window.scrollTo({ top: 0, behavior: 'smooth' });
                }}
              />
            </div>
          ) : null}
        </div>
      </section>
    </div>
  );
}

function ReceiptLibraryHeader({ totalReceipts }: { totalReceipts?: number | undefined }) {
  return (
    <section className="relative isolate overflow-hidden rounded-3xl border border-primary/15 bg-gradient-to-br from-primary/[0.12] via-card to-accent/40 px-6 py-8 shadow-sm sm:px-8 sm:py-10">
      <div
        aria-hidden="true"
        className="absolute -right-20 -top-24 size-72 rounded-full bg-primary/10 blur-3xl"
      />
      <div
        aria-hidden="true"
        className="absolute -bottom-28 left-1/3 size-64 rounded-full bg-emerald-300/10 blur-3xl"
      />

      <div className="relative flex flex-col justify-between gap-7 sm:flex-row sm:items-center">
        <div className="max-w-2xl">
          <div className="mb-4 flex w-fit items-center gap-2 rounded-full border border-primary/15 bg-background/70 px-3 py-1.5 text-xs font-medium text-primary shadow-sm backdrop-blur">
            <Sparkles className="size-3.5" aria-hidden="true" />
            Your digital receipt library
          </div>

          <h1 className="text-3xl font-bold tracking-tight sm:text-4xl">
            Receipts
          </h1>

          <p className="mt-3 max-w-xl text-base leading-7 text-muted-foreground">
            Keep every purchase organised, review extracted information and
            follow each document from upload to confirmation.
          </p>

          {typeof totalReceipts === 'number' ? (
            <p className="mt-4 text-sm font-medium text-foreground">
              {totalReceipts === 0
                ? 'Your library is ready for its first receipt.'
                : `${totalReceipts.toLocaleString('en-GB')} ${
                    totalReceipts === 1 ? 'receipt is' : 'receipts are'
                  } currently stored.`}
            </p>
          ) : null}
        </div>

        <div className="flex shrink-0 flex-col items-start gap-3 sm:items-end">
          <Button asChild size="lg" className="shadow-md shadow-primary/15">
            <Link to="/receipts/new">
              <UploadCloud aria-hidden="true" />
              Upload receipt
            </Link>
          </Button>

          <span className="text-xs text-muted-foreground">
            PDF, JPG or PNG · Maximum 10 MB
          </span>
        </div>
      </div>
    </section>
  );
}

function ReceiptEmptyState() {
  return (
    <div className="py-6">
      <EmptyState
        icon={ReceiptText}
        title="Your receipt library is empty"
        description="Upload a PDF or image and ReceiptFlow will extract the merchant, date, totals and line items for you to review."
        action={
          <Button asChild size="lg">
            <Link to="/receipts/new">
              <Plus aria-hidden="true" />
              Upload your first receipt
            </Link>
          </Button>
        }
      />
    </div>
  );
}

interface ReceiptPaginationProps {
  page: number;
  totalPages: number;
  totalReceipts: number;
  isFetching: boolean;
  onPrevious: () => void;
  onNext: () => void;
}

function ReceiptPagination({
  page,
  totalPages,
  totalReceipts,
  isFetching,
  onPrevious,
  onNext,
}: ReceiptPaginationProps) {
  const firstReceipt = (page - 1) * pageSize + 1;
  const lastReceipt = Math.min(page * pageSize, totalReceipts);

  return (
    <nav
      className="flex flex-col gap-4 rounded-2xl border bg-muted/20 p-3 sm:flex-row sm:items-center sm:justify-between"
      aria-label="Receipt pages"
    >
      <Button
        type="button"
        variant="outline"
        disabled={page === 1 || isFetching}
        onClick={onPrevious}
        className="bg-background"
      >
        <ChevronLeft aria-hidden="true" />
        Previous
      </Button>

      <div className="text-center">
        <p className="text-sm font-medium">
          Page {page.toLocaleString('en-GB')} of{' '}
          {totalPages.toLocaleString('en-GB')}
        </p>

        <p className="mt-0.5 text-xs text-muted-foreground">
          Showing {firstReceipt.toLocaleString('en-GB')}–
          {lastReceipt.toLocaleString('en-GB')} of{' '}
          {totalReceipts.toLocaleString('en-GB')}
        </p>
      </div>

      <Button
        type="button"
        variant="outline"
        disabled={page >= totalPages || isFetching}
        onClick={onNext}
        className="bg-background"
      >
        Next
        <ChevronRight aria-hidden="true" />
      </Button>
    </nav>
  );
}

function ReceiptListLoading() {
  return (
    <div
      className="grid gap-5 md:grid-cols-2 xl:grid-cols-3"
      aria-label="Loading receipts"
      aria-busy="true"
    >
      {Array.from({ length: 6 }, (_, index) => (
        <Card
          key={index}
          className="overflow-hidden border-border/70 shadow-sm"
        >
          <CardContent className="p-5">
            <div className="mb-5 flex items-center gap-3">
              <div className="size-11 animate-pulse rounded-xl bg-muted" />
              <div className="flex-1">
                <LoadingSkeleton lines={2} />
              </div>
            </div>

            <LoadingSkeleton lines={3} />
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
