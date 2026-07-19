import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  CalendarDays,
  FileText,
  Store,
  Tag,
  Upload,
  WalletCards,
} from 'lucide-react';
import { useState } from 'react';
import { useLocation, useParams } from 'react-router-dom';
import { getSafeErrorMessage } from '@/api/error-message';
import { queryKeys } from '@/api/query-keys';
import {
  useReceipt,
  useReceiptDocument,
  useReceiptDocuments,
} from '@/api/use-receipt';
import { ReceiptConfirmationForm } from '@/components/receipts/receipt-confirmation-form';
import {
  ReceiptFilePicker,
  validateReceiptFile,
} from '@/components/receipts/receipt-file-picker';
import { EmptyState } from '@/components/shared/empty-state';
import { ErrorState } from '@/components/shared/error-state';
import { LoadingSkeleton } from '@/components/shared/loading-skeleton';
import { PageHeader } from '@/components/shared/page-header';
import {
  StatusBadge,
  type ReceiptStatus,
} from '@/components/shared/status-badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { formatCurrency } from '@/lib/utils';
import { useAuth } from '@/providers/use-auth';

interface ReceiptDetailsLocationState {
  documentId?: string;
}

export function Component() {
  const { receiptId = '' } = useParams();
  const location = useLocation();
  const preferredDocumentId = (
    location.state as ReceiptDetailsLocationState | null
  )?.documentId;
  const [replacementDocumentId, setReplacementDocumentId] = useState<string>();
  const receiptQuery = useReceipt(receiptId);
  const documentsQuery = useReceiptDocuments(receiptId);
  const documentId =
    replacementDocumentId ??
    preferredDocumentId ??
    documentsQuery.data?.[0]?.documentId;
  const documentQuery = useReceiptDocument(receiptId, documentId);
  const [manualEntry, setManualEntry] = useState(false);

  if (
    receiptQuery.isPending ||
    documentsQuery.isPending ||
    (documentId && documentQuery.isPending)
  ) {
    return <DetailsLoading />;
  }
  if (receiptQuery.isError || documentsQuery.isError || documentQuery.isError) {
    const error = receiptQuery.isError
      ? receiptQuery.error
      : documentsQuery.isError
        ? documentsQuery.error
        : documentQuery.error;
    return (
      <ErrorState
        title="Receipt unavailable"
        description={getSafeErrorMessage(error)}
        onAction={() => {
          void Promise.all([
            receiptQuery.refetch(),
            documentsQuery.refetch(),
            documentQuery.refetch(),
          ]);
        }}
      />
    );
  }

  const receipt = receiptQuery.data;
  if (!documentId || !documentQuery.data) {
    return (
      <div className="space-y-6">
        <PageHeader
          title={receipt.merchantName ?? 'Draft receipt'}
          description="Receipt details"
        />
        <EmptyState
          icon={FileText}
          title="No document uploaded"
          description="This receipt does not have a source document."
        />
      </div>
    );
  }

  const document = documentQuery.data;
  const isFailed = document.processingStatus === 'Failed';
  const requiresReview =
    document.confirmationRequired && document.processingStatus === 'Completed';

  return (
    <div className="space-y-6">
      <PageHeader
        title={
          receipt.merchantName ??
          (requiresReview ? 'Review receipt' : 'Receipt processing')
        }
        description={`Source: ${document.originalFileName}`}
        actions={
          <StatusBadge
            status={toLifecycleStatus(document.receiptLifecycleStatus)}
          />
        }
      />
      <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_22rem]">
        <Card>
          <CardHeader>
            <h2 className="font-semibold">Receipt processing</h2>
          </CardHeader>
          <CardContent className="space-y-6">
            <ProcessingNotice
              status={document.processingStatus}
              error={document.processingError}
              confirmationRequired={document.confirmationRequired}
            />
            {requiresReview ? (
              <ReceiptConfirmationForm
                key={document.documentId}
                receiptId={receiptId}
                extraction={document.extraction}
                onConfirmed={() => {
                  void Promise.all([
                    receiptQuery.refetch(),
                    documentQuery.refetch(),
                  ]);
                }}
              />
            ) : null}
            {isFailed && !manualEntry ? (
              <div className="flex flex-wrap gap-3">
                <Button
                  type="button"
                  onClick={() => {
                    setManualEntry(true);
                  }}
                >
                  Enter details manually
                </Button>
              </div>
            ) : null}
            {isFailed && manualEntry ? (
              <ReceiptConfirmationForm
                receiptId={receiptId}
                extraction={null}
                manualEntry
                onConfirmed={() => {
                  void Promise.all([
                    receiptQuery.refetch(),
                    documentQuery.refetch(),
                  ]);
                }}
              />
            ) : null}
            {isFailed ? (
              <ReplacementDocumentUpload
                receiptId={receiptId}
                onUploaded={setReplacementDocumentId}
              />
            ) : null}
            {receipt.lifecycleStatus === 'Confirmed' ? (
              <ConfirmedDetails receipt={receipt} document={document} />
            ) : null}
          </CardContent>
        </Card>
        <ReceiptMetadata receipt={receipt} />
      </div>
    </div>
  );
}

function ProcessingNotice({
  status,
  error,
  confirmationRequired,
}: {
  status: string;
  error: string | null;
  confirmationRequired: boolean;
}) {
  if (status === 'Failed')
    return (
      <div
        className="rounded-lg border border-destructive/30 bg-destructive/8 p-4"
        role="alert"
      >
        <p className="font-semibold text-destructive">Extraction failed</p>
        <p className="mt-1 text-sm text-muted-foreground">
          {error ?? 'Document processing failed.'}
        </p>
      </div>
    );
  if (status === 'Completed')
    return (
      <p
        className="rounded-lg border border-success/30 bg-success/8 p-4 text-sm"
        role="status"
      >
        {confirmationRequired
          ? 'Extraction completed. Review is required before this receipt affects spending or search.'
          : 'Receipt extraction is complete.'}
      </p>
    );
  return (
    <div
      className="rounded-lg border border-processing/30 bg-processing/8 p-4"
      role="status"
      aria-live="polite"
    >
      <p className="font-semibold">Document is {status.toLowerCase()}</p>
      <p className="mt-1 text-sm text-muted-foreground">
        ReceiptFlow is extracting your receipt. This page checks automatically.
      </p>
    </div>
  );
}

function ReplacementDocumentUpload({
  receiptId,
  onUploaded,
}: {
  receiptId: string;
  onUploaded: (documentId: string) => void;
}) {
  const { apiClient } = useAuth();
  const queryClient = useQueryClient();
  const [file, setFile] = useState<File | null>(null);
  const [error, setError] = useState<string>();
  const upload = useMutation({
    mutationFn: (selected: File) =>
      apiClient.uploadReceiptDocument(receiptId, selected),
  });
  return (
    <section
      className="space-y-3 border-t pt-5"
      aria-labelledby="replacement-heading"
    >
      <div>
        <h3 id="replacement-heading" className="text-sm font-semibold">
          Try another document
        </h3>
        <p className="mt-1 text-sm text-muted-foreground">
          Upload a clearer replacement to restart extraction for this draft.
        </p>
      </div>
      <ReceiptFilePicker
        file={file}
        error={error}
        disabled={upload.isPending}
        onChange={(selected) => {
          setFile(selected);
          setError(
            selected ? (validateReceiptFile(selected) ?? undefined) : undefined,
          );
        }}
      />
      <Button
        type="button"
        variant="outline"
        disabled={!file || upload.isPending}
        onClick={() => {
          if (!file) return;
          void upload
            .mutateAsync(file)
            .then(async (result) => {
              await queryClient.invalidateQueries({
                queryKey: queryKeys.receiptDocuments(receiptId),
              });
              onUploaded(result.documentId);
            })
            .catch((caught: unknown) => {
              setError(
                getSafeErrorMessage(
                  caught,
                  'The replacement could not be uploaded.',
                ),
              );
            });
        }}
      >
        <Upload aria-hidden="true" />
        {upload.isPending ? 'Uploading…' : 'Upload replacement'}
      </Button>
    </section>
  );
}

function ConfirmedDetails({
  receipt,
  document,
}: {
  receipt: NonNullable<ReturnType<typeof useReceipt>['data']>;
  document: NonNullable<ReturnType<typeof useReceiptDocument>['data']>;
}) {
  return (
    <section aria-labelledby="confirmed-heading" className="space-y-4">
      <h3 id="confirmed-heading" className="font-semibold">
        Confirmed receipt
      </h3>
      <dl className="grid gap-4 sm:grid-cols-2">
        <DataValue label="Merchant" value={receipt.merchantName} />
        <DataValue
          label="Purchase date"
          value={receipt.purchaseDate ? formatDate(receipt.purchaseDate) : null}
        />
        <DataValue
          label="Subtotal"
          value={formatOptionalCurrency(
            receipt.subtotalAmount,
            receipt.currency,
          )}
        />
        <DataValue
          label="Tax"
          value={formatOptionalCurrency(receipt.taxAmount, receipt.currency)}
        />
        <DataValue
          label="Total"
          value={formatOptionalCurrency(receipt.totalAmount, receipt.currency)}
        />
        <DataValue label="Currency" value={receipt.currency} />
        <DataValue label="Category" value={receipt.category} />
        <DataValue label="Source filename" value={document.originalFileName} />
      </dl>
      {receipt.lineItems.length ? (
        <div>
          <h4 className="text-sm font-semibold">Line items</h4>
          <ul className="mt-2 divide-y rounded-lg border">
            {receipt.lineItems.map((item) => (
              <li
                key={item.id}
                className="flex justify-between gap-3 p-3 text-sm"
              >
                <span>
                  {item.description} × {item.quantity}
                </span>
                <span className="font-semibold">
                  {receipt.currency
                    ? formatCurrency(item.totalPrice, receipt.currency)
                    : item.totalPrice}
                </span>
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </section>
  );
}

function ReceiptMetadata({
  receipt,
}: {
  receipt: NonNullable<ReturnType<typeof useReceipt>['data']>;
}) {
  return (
    <Card className="h-fit">
      <CardHeader>
        <h2 className="font-semibold">Receipt summary</h2>
      </CardHeader>
      <CardContent>
        <dl className="space-y-5">
          <Detail
            icon={Store}
            label="Merchant"
            value={receipt.merchantName ?? 'Awaiting extraction'}
          />
          <Detail
            icon={CalendarDays}
            label="Date"
            value={
              receipt.purchaseDate
                ? formatDate(receipt.purchaseDate)
                : 'Awaiting extraction'
            }
          />
          <Detail
            icon={WalletCards}
            label="Total"
            value={
              formatOptionalCurrency(receipt.totalAmount, receipt.currency) ??
              'Awaiting extraction'
            }
          />
          <Detail
            icon={Tag}
            label="Category"
            value={receipt.category ?? 'Awaiting review'}
          />
        </dl>
      </CardContent>
    </Card>
  );
}

function DetailsLoading() {
  return (
    <div
      className="space-y-6"
      role="status"
      aria-label="Loading receipt details"
    >
      <LoadingSkeleton lines={2} />
      <LoadingSkeleton className="rounded-xl border bg-card p-6" lines={7} />
    </div>
  );
}
function DataValue({ label, value }: { label: string; value: string | null }) {
  return (
    <div>
      <dt className="text-xs font-medium text-muted-foreground">{label}</dt>
      <dd className="mt-1 text-sm font-semibold">{value ?? '—'}</dd>
    </div>
  );
}
function Detail({
  icon: Icon,
  label,
  value,
}: {
  icon: typeof Store;
  label: string;
  value: string;
}) {
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
function toLifecycleStatus(status: string): ReceiptStatus {
  if (
    status === 'Draft' ||
    status === 'Processing' ||
    status === 'Failed' ||
    status === 'Confirmed'
  )
    return status;
  return status === 'ReviewRequired' ? 'Review required' : 'Draft';
}
function formatDate(value: string) {
  return new Intl.DateTimeFormat('en-GB', { dateStyle: 'long' }).format(
    new Date(value),
  );
}
function formatOptionalCurrency(value: number | null, currency: string | null) {
  return value === null || currency === null
    ? null
    : formatCurrency(value, currency);
}
