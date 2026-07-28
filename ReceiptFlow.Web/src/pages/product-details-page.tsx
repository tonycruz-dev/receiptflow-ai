import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ArrowLeft,
  BookOpen,
  CircleAlert,
  Clock3,
  FileText,
  RefreshCw,
  Upload,
} from 'lucide-react';
import { useState, type SyntheticEvent } from 'react';
import { Link, useParams } from 'react-router-dom';
import type {
  ConfirmProductManualRequest,
  ProductManualResponse,
} from '@/api/contracts';
import { getSafeErrorMessage } from '@/api/error-message';
import { queryKeys } from '@/api/query-keys';
import { ErrorState } from '@/components/shared/error-state';
import { LoadingSkeleton } from '@/components/shared/loading-skeleton';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { useAuth } from '@/providers/use-auth';

const inputClass =
  'h-11 w-full rounded-lg border bg-background px-3 text-sm shadow-sm';

export function Component() {
  const { productId = '' } = useParams();
  const { apiClient } = useAuth();
  const product = useQuery({
    queryKey: queryKeys.product(productId),
    queryFn: ({ signal }) => apiClient.getProduct(productId, signal),
    enabled: Boolean(productId),
  });
  const manuals = useQuery({
    queryKey: queryKeys.productManuals(productId),
    queryFn: ({ signal }) => apiClient.listProductManuals(productId, signal),
    enabled: Boolean(productId),
    refetchInterval: (query) =>
      query.state.data?.some((manual) =>
        ['Queued', 'Processing'].includes(manual.documentProcessingStatus),
      )
        ? 2000
        : false,
  });

  if (product.isLoading || manuals.isLoading)
    return <LoadingSkeleton lines={5} />;
  if (product.isError || manuals.isError) {
    return (
      <ErrorState
        title="Product unavailable"
        description={getSafeErrorMessage(product.error ?? manuals.error)}
        onAction={() => {
          void Promise.all([product.refetch(), manuals.refetch()]);
        }}
      />
    );
  }
  if (!product.data) return null;

  return (
    <div className="space-y-8">
      <header className="rounded-3xl border border-primary/15 bg-gradient-to-br from-primary/[0.12] via-card to-accent/40 px-6 py-8 shadow-sm sm:px-8">
        <Button asChild variant="ghost" size="sm" className="-ml-3 mb-4">
          <Link to="/products">
            <ArrowLeft aria-hidden="true" />
            Products
          </Link>
        </Button>
        <div className="flex flex-col justify-between gap-5 sm:flex-row sm:items-end">
          <div>
            <p className="font-medium text-primary">
              {product.data.manufacturer}
            </p>
            <h1 className="mt-1 text-3xl font-bold tracking-tight">
              {product.data.name}
            </h1>
            <p className="mt-2 text-muted-foreground">
              {product.data.modelNumber ?? 'No model number'}
            </p>
          </div>
          <Button asChild>
            <Link
              to={`/products/manuals/new?productId=${encodeURIComponent(productId)}`}
            >
              <Upload aria-hidden="true" />
              Upload manual
            </Link>
          </Button>
        </div>
      </header>

      <section aria-labelledby="manual-versions-title" className="space-y-5">
        <div>
          <h2 id="manual-versions-title" className="text-2xl font-semibold">
            Manual versions
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            Previous versions remain available when a replacement is uploaded.
          </p>
        </div>

        {manuals.data?.length ? (
          manuals.data.map((manual) => (
            <ManualVersion
              key={manual.productManualId}
              manual={manual}
              productId={productId}
            />
          ))
        ) : (
          <Card className="rounded-2xl">
            <CardContent className="p-8 text-center">
              <BookOpen
                className="mx-auto size-10 text-muted-foreground"
                aria-hidden="true"
              />
              <h3 className="mt-4 font-semibold">No manuals uploaded</h3>
              <p className="mt-2 text-sm text-muted-foreground">
                Upload a PDF to begin extraction.
              </p>
            </CardContent>
          </Card>
        )}
      </section>
    </div>
  );
}

function ManualVersion({
  manual,
  productId,
}: {
  manual: ProductManualResponse;
  productId: string;
}) {
  const isProcessing = ['Queued', 'Processing'].includes(
    manual.documentProcessingStatus,
  );
  const previewSections = manual.sections.slice(0, 3);

  return (
    <Card className="overflow-hidden rounded-2xl">
      <CardHeader className="border-b bg-muted/20 p-5">
        <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-start">
          <div className="flex items-start gap-3">
            <div className="grid size-10 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary">
              <FileText aria-hidden="true" />
            </div>
            <div>
              <h3 className="font-semibold">
                {manual.versionLabel
                  ? `Version ${manual.versionLabel}`
                  : manual.originalFileName}
              </h3>
              <p className="mt-1 text-sm text-muted-foreground">
                {manual.locale} · Uploaded{' '}
                {new Date(manual.uploadedAtUtc).toLocaleDateString('en-GB')}
              </p>
            </div>
          </div>
          <ManualStatus status={manual.manualLifecycleStatus} />
        </div>
      </CardHeader>
      <CardContent className="space-y-5 p-5">
        {isProcessing ? (
          <div
            className="flex items-center gap-3 rounded-xl border border-processing/30 bg-processing/10 p-4 text-sm"
            role="status"
          >
            <RefreshCw
              className="size-4 animate-spin text-processing motion-reduce:animate-none"
              aria-hidden="true"
            />
            Processing manual. This page refreshes automatically.
          </div>
        ) : null}

        {manual.manualLifecycleStatus === 'Failed' ? (
          <div
            className="flex flex-col justify-between gap-3 rounded-xl border border-destructive/30 bg-destructive/10 p-4 sm:flex-row sm:items-center"
            role="alert"
          >
            <div className="flex gap-3">
              <CircleAlert
                className="size-5 text-destructive"
                aria-hidden="true"
              />
              <p className="text-sm">
                Extraction failed. Check the PDF and try uploading it again.
              </p>
            </div>
            <Button asChild variant="outline" size="sm">
              <Link
                to={`/products/manuals/new?productId=${encodeURIComponent(productId)}`}
              >
                Try another PDF
              </Link>
            </Button>
          </div>
        ) : null}

        <dl className="grid gap-4 text-sm sm:grid-cols-3">
          <div>
            <dt className="text-muted-foreground">Document status</dt>
            <dd className="mt-1 font-medium">
              {splitStatus(manual.documentProcessingStatus)}
            </dd>
          </div>
          <div>
            <dt className="text-muted-foreground">Warranty duration</dt>
            <dd className="mt-1 font-medium">
              {manual.warrantyDurationMonths
                ? `${manual.warrantyDurationMonths.toString()} months`
                : 'Not confirmed'}
            </dd>
          </div>
          <div>
            <dt className="text-muted-foreground">Sections</dt>
            <dd className="mt-1 font-medium">
              {manual.sections.length.toString()}
            </dd>
          </div>
        </dl>

        {previewSections.length ? (
          <div>
            <h4 className="text-sm font-semibold">Section preview</h4>
            <ul className="mt-2 space-y-2 text-sm text-muted-foreground">
              {previewSections.map((section) => (
                <li
                  key={section.ordinal}
                  className="rounded-lg bg-muted/40 px-3 py-2"
                >
                  <p className="font-medium text-foreground">
                    {section.headingPath}
                  </p>
                  <p className="mt-1 line-clamp-2">{section.content}</p>
                </li>
              ))}
            </ul>
          </div>
        ) : null}

        {manual.manualLifecycleStatus === 'ReviewRequired' ? (
          <ManualConfirmationForm manual={manual} />
        ) : null}

        {manual.manualLifecycleStatus === 'Active' ? (
          <Button asChild variant="outline">
            <Link
              to={`/products/manuals/new?productId=${encodeURIComponent(productId)}&supersedesProductManualId=${encodeURIComponent(manual.productManualId)}`}
            >
              Upload replacement
            </Link>
          </Button>
        ) : null}
      </CardContent>
    </Card>
  );
}

function ManualConfirmationForm({ manual }: { manual: ProductManualResponse }) {
  const { apiClient } = useAuth();
  const queryClient = useQueryClient();
  const suggestion = manual.extraction;
  const [manufacturer, setManufacturer] = useState(
    suggestion?.suggestedManufacturer ?? manual.manufacturer,
  );
  const [productName, setProductName] = useState(
    suggestion?.suggestedProductName ?? manual.productName,
  );
  const [modelNumber, setModelNumber] = useState(
    suggestion?.suggestedModelNumber ?? manual.modelNumber ?? '',
  );
  const [versionLabel, setVersionLabel] = useState(
    suggestion?.suggestedVersionLabel ?? '',
  );
  const [locale, setLocale] = useState(manual.locale);
  const [warranty, setWarranty] = useState(
    suggestion?.suggestedWarrantyDurationMonths?.toString() ?? '',
  );
  const confirm = useMutation({
    mutationFn: (request: ConfirmProductManualRequest) =>
      apiClient.confirmProductManual(
        manual.productId,
        manual.productManualId,
        request,
      ),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: queryKeys.product(manual.productId),
        }),
        queryClient.invalidateQueries({
          queryKey: queryKeys.productManuals(manual.productId),
        }),
      ]);
    },
  });

  function submit(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault();
    if (
      !manufacturer.trim() ||
      !productName.trim() ||
      !versionLabel.trim() ||
      !locale.trim()
    )
      return;
    const warrantyDurationMonths = warranty ? Number(warranty) : null;
    confirm.mutate({
      manufacturer: manufacturer.trim(),
      productName: productName.trim(),
      modelNumber: modelNumber.trim() || null,
      versionLabel: versionLabel.trim(),
      locale: locale.trim(),
      warrantyDurationMonths,
    });
  }

  return (
    <form
      className="space-y-4 rounded-2xl border border-warning/30 bg-warning/5 p-5"
      onSubmit={submit}
    >
      <div>
        <h4 className="font-semibold">Review extracted details</h4>
        <p className="mt-1 text-sm text-muted-foreground">
          Edit any suggestion before activating this manual.
        </p>
      </div>
      <div className="grid gap-4 md:grid-cols-2">
        <ManualField
          label="Manufacturer"
          value={manufacturer}
          onChange={setManufacturer}
          required
        />
        <ManualField
          label="Product name"
          value={productName}
          onChange={setProductName}
          required
        />
        <ManualField
          label="Model number"
          value={modelNumber}
          onChange={setModelNumber}
        />
        <ManualField
          label="Manual version"
          value={versionLabel}
          onChange={setVersionLabel}
          required
        />
        <ManualField
          label="Locale"
          value={locale}
          onChange={setLocale}
          required
        />
        <label className="space-y-2 text-sm font-medium">
          Warranty duration (months)
          <input
            className={inputClass}
            type="number"
            min={1}
            max={1200}
            value={warranty}
            onChange={(event) => {
              setWarranty(event.target.value);
            }}
          />
        </label>
      </div>
      {confirm.isError ? (
        <p className="text-sm text-destructive" role="alert">
          {getSafeErrorMessage(confirm.error)}
        </p>
      ) : null}
      <Button disabled={confirm.isPending}>
        {confirm.isPending ? 'Confirming…' : 'Confirm and activate'}
      </Button>
    </form>
  );
}

function ManualField({
  label,
  value,
  onChange,
  required = false,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  required?: boolean;
}) {
  return (
    <label className="space-y-2 text-sm font-medium">
      {label}
      <input
        className={inputClass}
        required={required}
        value={value}
        onChange={(event) => {
          onChange(event.target.value);
        }}
      />
    </label>
  );
}

function ManualStatus({ status }: { status: string }) {
  const active = status === 'Active';
  const failed = status === 'Failed';
  return (
    <span
      className={`inline-flex w-fit items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-semibold ${
        active
          ? 'border-success/35 bg-success/10 text-success'
          : failed
            ? 'border-destructive/35 bg-destructive/10 text-destructive'
            : 'border-warning/35 bg-warning/10 text-warning'
      }`}
    >
      <Clock3 className="size-3.5" aria-hidden="true" />
      {splitStatus(status)}
    </span>
  );
}

function splitStatus(status: string) {
  return status.replace(/([a-z])([A-Z])/g, '$1 $2');
}
