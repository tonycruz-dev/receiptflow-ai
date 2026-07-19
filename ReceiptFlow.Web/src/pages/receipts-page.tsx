import { Plus, ReceiptText } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { getSafeErrorMessage } from '@/api/error-message';
import { mapReceiptSummary } from '@/api/map-receipt-summary';
import { useReceipts } from '@/api/use-receipts';
import { EmptyState } from '@/components/shared/empty-state';
import { ErrorState } from '@/components/shared/error-state';
import { LoadingSkeleton } from '@/components/shared/loading-skeleton';
import { PageHeader } from '@/components/shared/page-header';
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
    <div className="space-y-6">
      <PageHeader
        title="Receipts"
        description="Review uploaded documents and track their processing status."
        actions={
          <Button asChild>
            <Link to="/receipts/new">
              <Plus aria-hidden="true" />
              Upload receipt
            </Link>
          </Button>
        }
      />
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
        <EmptyState
          icon={ReceiptText}
          title="No receipts yet"
          description="Upload your first receipt to begin building your document history."
          action={
            <Button asChild>
              <Link to="/receipts/new">Upload receipt</Link>
            </Button>
          }
        />
      ) : receipts.data ? (
        <>
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {receipts.data.items.map((receipt) => (
              <ReceiptCard
                key={receipt.receiptId}
                receipt={mapReceiptSummary(receipt)}
              />
            ))}
          </div>
          <nav
            className="flex items-center justify-between border-t pt-4"
            aria-label="Receipt pages"
          >
            <Button
              type="button"
              variant="outline"
              disabled={page === 1 || receipts.isFetching}
              onClick={() => {
                setPage((current) => current - 1);
              }}
            >
              Previous
            </Button>
            <p className="text-sm text-muted-foreground">
              Page {page.toLocaleString('en-GB')} of{' '}
              {totalPages.toLocaleString('en-GB')}
            </p>
            <Button
              type="button"
              variant="outline"
              disabled={page >= totalPages || receipts.isFetching}
              onClick={() => {
                setPage((current) => current + 1);
              }}
            >
              Next
            </Button>
          </nav>
        </>
      ) : null}
    </div>
  );
}

function ReceiptListLoading() {
  return (
    <div
      className="grid gap-4 md:grid-cols-2 xl:grid-cols-3"
      aria-label="Loading receipts"
    >
      {Array.from({ length: 6 }, (_, index) => (
        <Card key={index}>
          <CardContent className="p-5">
            <LoadingSkeleton lines={4} />
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
