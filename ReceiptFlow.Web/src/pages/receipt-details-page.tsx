import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  ArrowLeft,
  CalendarDays,
  CheckCircle2,
  CircleAlert,
  Clock3,
  FileText,
  LoaderCircle,
  ReceiptText,
  ScanLine,
  Store,
  Tag,
  Upload,
  WalletCards,
  type LucideIcon,
} from 'lucide-react';
import { useState } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import type { ReceiptDocumentDetail, ReceiptResponse } from '@/api/contracts';
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
      <div className="space-y-8">
        <ReceiptDetailsHeader
          title={receipt.merchantName ?? 'Draft receipt'}
          status={toLifecycleStatus(receipt.lifecycleStatus)}
        />
        <div className="[&>section]:rounded-3xl [&>section]:border-border/70">
          <EmptyState
            icon={FileText}
            title="No document uploaded"
            description="This receipt does not have a source document to display or process."
          />
        </div>
      </div>
    );
  }

  const document = documentQuery.data;
  const isFailed = document.processingStatus === 'Failed';
  const requiresReview =
    document.confirmationRequired && document.processingStatus === 'Completed';

  return (
    <div className="space-y-8">
      <ReceiptDetailsHeader
        title={
          receipt.merchantName ??
          (requiresReview ? 'Review receipt' : 'Receipt processing')
        }
        sourceFilename={document.originalFileName}
        status={toLifecycleStatus(document.receiptLifecycleStatus)}
      />

      <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_22rem]">
        <Card className="overflow-hidden rounded-3xl border-border/70 shadow-sm">
          <CardHeader className="border-b bg-muted/20 p-6 sm:p-8">
            <div className="flex items-start gap-4">
              <div className="flex size-11 shrink-0 items-center justify-center rounded-2xl bg-primary/10 text-primary">
                {receipt.lifecycleStatus === 'Confirmed' ? (
                  <CheckCircle2 className="size-5" aria-hidden="true" />
                ) : (
                  <ScanLine className="size-5" aria-hidden="true" />
                )}
              </div>
              <div>
                <h2 className="text-lg font-semibold tracking-tight">
                  {receipt.lifecycleStatus === 'Confirmed'
                    ? 'Receipt information'
                    : isFailed
                      ? 'Recover this receipt'
                      : requiresReview
                        ? 'Review extracted details'
                        : 'Receipt processing'}
                </h2>
                <p className="mt-1 text-sm leading-6 text-muted-foreground">
                  {receipt.lifecycleStatus === 'Confirmed'
                    ? 'The confirmed purchase details saved to your receipt library.'
                    : isFailed
                      ? 'Choose how you would like to finish adding this receipt.'
                      : requiresReview
                        ? 'Check the extracted values carefully before confirming them.'
                        : 'ReceiptFlow will keep this page up to date while extraction runs.'}
                </p>
              </div>
            </div>
          </CardHeader>

          <CardContent className="space-y-7 p-6 sm:p-8">
            <ProcessingNotice
              status={document.processingStatus}
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
              <ManualEntryChoice
                onSelect={() => {
                  setManualEntry(true);
                }}
              />
            ) : null}
            {isFailed && manualEntry ? (
              <section
                className="rounded-2xl border bg-muted/20 p-5 sm:p-6"
                aria-label="Manual receipt entry"
              >
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
              </section>
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

        <ReceiptSidebar receipt={receipt} document={document} />
      </div>
    </div>
  );
}

function ReceiptDetailsHeader({
  title,
  sourceFilename,
  status,
}: {
  title: string;
  sourceFilename?: string;
  status: ReceiptStatus;
}) {
  return (
    <header className="relative isolate overflow-hidden rounded-3xl border border-primary/15 bg-gradient-to-br from-primary/[0.10] via-card to-accent/30 px-6 py-8 shadow-sm sm:px-8">
      <div
        aria-hidden="true"
        className="absolute -right-20 -top-24 size-64 rounded-full bg-primary/10 blur-3xl"
      />
      <div
        aria-hidden="true"
        className="absolute -bottom-24 left-1/3 size-56 rounded-full bg-emerald-300/10 blur-3xl"
      />

      <div className="relative">
        <Button
          asChild
          variant="ghost"
          size="sm"
          className="-ml-3 mb-5 text-muted-foreground hover:text-foreground"
        >
          <Link to="/receipts">
            <ArrowLeft aria-hidden="true" />
            Back to receipts
          </Link>
        </Button>

        <div className="flex flex-col justify-between gap-6 sm:flex-row sm:items-center">
          <div className="flex min-w-0 items-start gap-4">
            <div className="flex size-14 shrink-0 items-center justify-center rounded-2xl bg-primary text-primary-foreground shadow-md shadow-primary/20">
              <ReceiptText className="size-7" aria-hidden="true" />
            </div>
            <div className="min-w-0">
              <p className="mb-2 text-xs font-semibold uppercase tracking-[0.16em] text-primary">
                Receipt details
              </p>
              <h1 className="break-words text-3xl font-bold tracking-tight sm:text-4xl">
                {title}
              </h1>
              <p className="mt-2 break-all text-sm leading-6 text-muted-foreground sm:text-base">
                {sourceFilename
                  ? `Source: ${sourceFilename}`
                  : 'No source document'}
              </p>
            </div>
          </div>
          <StatusBadge
            status={status}
            className="shrink-0 self-start sm:self-center"
          />
        </div>
      </div>
    </header>
  );
}

function ProcessingNotice({
  status,
  confirmationRequired,
}: {
  status: string;
  confirmationRequired: boolean;
}) {
  if (status === 'Failed') {
    return (
      <div
        className="flex gap-3 rounded-2xl border border-destructive/30 bg-destructive/8 p-4 sm:p-5"
        role="alert"
      >
        <CircleAlert
          className="mt-0.5 size-5 shrink-0 text-destructive"
          aria-hidden="true"
        />
        <div>
          <p className="font-semibold text-destructive">Extraction failed</p>
          <p className="mt-1 text-sm leading-6 text-muted-foreground">
            ReceiptFlow could not read this document. Enter the purchase details
            manually or upload a clearer replacement.
          </p>
        </div>
      </div>
    );
  }

  if (status === 'Completed' && confirmationRequired) {
    return (
      <div
        className="flex gap-3 rounded-2xl border border-warning/30 bg-warning/8 p-4 sm:p-5"
        role="status"
        aria-live="polite"
      >
        <CircleAlert
          className="mt-0.5 size-5 shrink-0 text-warning"
          aria-hidden="true"
        />
        <div>
          <p className="font-semibold text-warning">Review required</p>
          <p className="mt-1 text-sm leading-6 text-muted-foreground">
            Extraction is complete. Review the suggested values before this
            receipt affects spending totals or search.
          </p>
        </div>
      </div>
    );
  }

  if (status === 'Completed') {
    return (
      <div
        className="flex gap-3 rounded-2xl border border-success/30 bg-success/8 p-4 sm:p-5"
        role="status"
        aria-live="polite"
      >
        <CheckCircle2
          className="mt-0.5 size-5 shrink-0 text-success"
          aria-hidden="true"
        />
        <div>
          <p className="font-semibold text-success">Extraction completed</p>
          <p className="mt-1 text-sm leading-6 text-muted-foreground">
            Receipt extraction is complete and the saved details are ready.
          </p>
        </div>
      </div>
    );
  }

  const isActivelyProcessing = status !== 'Pending';
  const Icon = isActivelyProcessing ? LoaderCircle : Clock3;
  return (
    <div
      className="flex gap-3 rounded-2xl border border-processing/30 bg-processing/8 p-4 sm:p-5"
      role="status"
      aria-live="polite"
    >
      <Icon
        className={`mt-0.5 size-5 shrink-0 text-processing ${
          isActivelyProcessing ? 'animate-spin motion-reduce:animate-none' : ''
        }`}
        aria-hidden="true"
      />
      <div>
        <p className="font-semibold">Document is {status.toLowerCase()}</p>
        <p className="mt-1 text-sm leading-6 text-muted-foreground">
          ReceiptFlow is extracting your receipt. This page checks
          automatically.
        </p>
      </div>
    </div>
  );
}

function ManualEntryChoice({ onSelect }: { onSelect: () => void }) {
  return (
    <section
      className="rounded-2xl border bg-muted/20 p-5 sm:p-6"
      aria-labelledby="manual-entry-heading"
    >
      <div className="flex items-start gap-3">
        <div className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-background text-primary shadow-sm ring-1 ring-border">
          <ReceiptText className="size-5" aria-hidden="true" />
        </div>
        <div>
          <h3 id="manual-entry-heading" className="font-semibold">
            Enter the receipt manually
          </h3>
          <p className="mt-1 text-sm leading-6 text-muted-foreground">
            Keep this document and type the merchant, date, totals and optional
            line items yourself.
          </p>
          <Button type="button" className="mt-4" onClick={onSelect}>
            Enter details manually
          </Button>
        </div>
      </div>
    </section>
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
      className="space-y-5 rounded-2xl border bg-muted/20 p-5 sm:p-6"
      aria-labelledby="replacement-heading"
    >
      <div className="flex items-start gap-3">
        <div className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-background text-primary shadow-sm ring-1 ring-border">
          <Upload className="size-5" aria-hidden="true" />
        </div>
        <div>
          <h3 id="replacement-heading" className="font-semibold">
            Upload a replacement
          </h3>
          <p className="mt-1 text-sm leading-6 text-muted-foreground">
            Choose a clearer PDF or image to restart automatic extraction for
            this draft receipt.
          </p>
        </div>
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
        {upload.isPending ? (
          <LoaderCircle
            className="animate-spin motion-reduce:animate-none"
            aria-hidden="true"
          />
        ) : (
          <Upload aria-hidden="true" />
        )}
        {upload.isPending ? 'Uploading…' : 'Upload replacement'}
      </Button>
    </section>
  );
}

function ConfirmedDetails({
  receipt,
  document,
}: {
  receipt: ReceiptResponse;
  document: ReceiptDocumentDetail;
}) {
  return (
    <section aria-labelledby="confirmed-heading" className="space-y-8">
      <div>
        <h3 id="confirmed-heading" className="text-lg font-semibold">
          Confirmed receipt
        </h3>
        <p className="mt-1 text-sm leading-6 text-muted-foreground">
          A clear breakdown of the confirmed purchase and its source.
        </p>
      </div>

      <dl className="grid gap-4 sm:grid-cols-3">
        <AmountCard
          label="Subtotal"
          value={formatOptionalCurrency(
            receipt.subtotalAmount,
            receipt.currency,
          )}
        />
        <AmountCard
          label="Tax"
          value={formatOptionalCurrency(receipt.taxAmount, receipt.currency)}
        />
        <AmountCard
          label="Total"
          value={formatOptionalCurrency(receipt.totalAmount, receipt.currency)}
          highlighted
        />
      </dl>

      <section aria-labelledby="purchase-information-heading">
        <h4 id="purchase-information-heading" className="font-semibold">
          Purchase information
        </h4>
        <dl className="mt-4 grid gap-x-6 gap-y-5 rounded-2xl border bg-muted/20 p-5 sm:grid-cols-2">
          <DataValue label="Merchant" value={receipt.merchantName} />
          <DataValue
            label="Purchase date"
            value={
              receipt.purchaseDate ? formatDate(receipt.purchaseDate) : null
            }
          />
          <DataValue label="Currency" value={receipt.currency} />
          <DataValue label="Category" value={receipt.category} />
          <div className="sm:col-span-2">
            <DataValue
              label="Source filename"
              value={document.originalFileName}
            />
          </div>
        </dl>
      </section>

      {receipt.lineItems.length ? (
        <section aria-labelledby="line-items-heading">
          <div className="flex items-center justify-between gap-3">
            <h4 id="line-items-heading" className="font-semibold">
              Line items
            </h4>
            <span className="text-xs font-medium text-muted-foreground">
              {receipt.lineItems.length.toLocaleString('en-GB')}{' '}
              {receipt.lineItems.length === 1 ? 'item' : 'items'}
            </span>
          </div>
          <ol className="mt-4 overflow-hidden rounded-2xl border bg-card">
            {receipt.lineItems.map((item, index) => (
              <li
                key={item.id}
                className="flex items-center gap-4 border-b p-4 last:border-b-0 sm:p-5"
              >
                <span className="flex size-8 shrink-0 items-center justify-center rounded-full bg-primary/10 text-xs font-bold text-primary">
                  {index + 1}
                </span>
                <div className="min-w-0 flex-1">
                  <p className="font-medium">{item.description}</p>
                  <p className="mt-0.5 text-xs text-muted-foreground">
                    Quantity {item.quantity.toLocaleString('en-GB')}
                  </p>
                </div>
                <span className="shrink-0 font-semibold tabular-nums">
                  {receipt.currency
                    ? formatCurrency(item.totalPrice, receipt.currency)
                    : item.totalPrice}
                </span>
              </li>
            ))}
          </ol>
        </section>
      ) : null}
    </section>
  );
}

function AmountCard({
  label,
  value,
  highlighted = false,
}: {
  label: string;
  value: string | null;
  highlighted?: boolean;
}) {
  return (
    <div
      className={
        highlighted
          ? 'rounded-2xl border border-primary/25 bg-primary/10 p-4'
          : 'rounded-2xl border bg-muted/20 p-4'
      }
    >
      <dt
        className={`text-xs font-semibold uppercase tracking-wide ${
          highlighted ? 'text-primary' : 'text-muted-foreground'
        }`}
      >
        {label}
      </dt>
      <dd
        className={`mt-2 text-xl font-bold tabular-nums ${
          highlighted ? 'text-primary' : ''
        }`}
      >
        {value ?? '—'}
      </dd>
    </div>
  );
}

function ReceiptSidebar({
  receipt,
  document,
}: {
  receipt: ReceiptResponse;
  document: ReceiptDocumentDetail;
}) {
  return (
    <aside className="space-y-6" aria-label="Receipt metadata">
      <Card className="overflow-hidden rounded-3xl border-border/70 shadow-sm">
        <CardHeader className="border-b bg-muted/20 p-6">
          <h2 className="text-lg font-semibold tracking-tight">
            Receipt summary
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            Key purchase details at a glance.
          </p>
        </CardHeader>
        <CardContent className="p-6">
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

      <Card className="overflow-hidden rounded-3xl border-border/70 shadow-sm">
        <CardHeader className="border-b bg-muted/20 p-6">
          <h2 className="text-lg font-semibold tracking-tight">
            Source document
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            The file used for this extraction.
          </p>
        </CardHeader>
        <CardContent className="space-y-5 p-6">
          <Detail
            icon={FileText}
            label="Filename"
            value={document.originalFileName}
            breakWords
          />
          <Detail
            icon={ScanLine}
            label="Processing status"
            value={formatStatus(document.processingStatus)}
          />
        </CardContent>
      </Card>
    </aside>
  );
}

function DetailsLoading() {
  return (
    <div
      className="space-y-8"
      role="status"
      aria-label="Loading receipt details"
      aria-busy="true"
    >
      <div className="rounded-3xl border border-primary/15 bg-gradient-to-br from-primary/[0.08] via-card to-accent/20 p-6 shadow-sm sm:p-8">
        <div className="flex items-center gap-4">
          <div className="size-14 animate-pulse rounded-2xl bg-primary/10" />
          <LoadingSkeleton className="max-w-xl flex-1" lines={3} />
        </div>
      </div>
      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_22rem]">
        <Card className="rounded-3xl border-border/70 p-6 shadow-sm sm:p-8">
          <LoadingSkeleton lines={8} />
        </Card>
        <div className="space-y-6">
          <Card className="rounded-3xl border-border/70 p-6 shadow-sm">
            <LoadingSkeleton lines={5} />
          </Card>
          <Card className="rounded-3xl border-border/70 p-6 shadow-sm">
            <LoadingSkeleton lines={3} />
          </Card>
        </div>
      </div>
    </div>
  );
}

function DataValue({ label, value }: { label: string; value: string | null }) {
  return (
    <div>
      <dt className="text-xs font-medium text-muted-foreground">{label}</dt>
      <dd className="mt-1 break-words text-sm font-semibold">{value ?? '—'}</dd>
    </div>
  );
}

function Detail({
  icon: Icon,
  label,
  value,
  breakWords = false,
}: {
  icon: LucideIcon;
  label: string;
  value: string;
  breakWords?: boolean;
}) {
  return (
    <div className="flex gap-3">
      <div className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-muted text-muted-foreground">
        <Icon aria-hidden="true" className="size-4" />
      </div>
      <div className="min-w-0">
        <dt className="text-xs font-medium text-muted-foreground">{label}</dt>
        <dd
          className={`mt-0.5 text-sm font-semibold ${
            breakWords ? 'break-all' : 'break-words'
          }`}
        >
          {value}
        </dd>
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

function formatStatus(value: string) {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2');
}
