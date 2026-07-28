import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  CalendarDays,
  Package,
  ReceiptText,
  ShieldCheck,
  Trash2,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { Link } from 'react-router-dom';
import type { PurchaseResponse } from '@/api/contracts';
import { getSafeErrorMessage } from '@/api/error-message';
import { queryKeys } from '@/api/query-keys';
import { EmptyState } from '@/components/shared/empty-state';
import { ErrorState } from '@/components/shared/error-state';
import { LoadingSkeleton } from '@/components/shared/loading-skeleton';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { formatCurrency } from '@/lib/utils';
import { useAuth } from '@/providers/use-auth';

export function Component() {
  const { apiClient } = useAuth();
  const queryClient = useQueryClient();
  const purchases = useQuery({
    queryKey: queryKeys.purchases,
    queryFn: ({ signal }) => apiClient.listPurchases(signal),
  });
  const unlink = useMutation({
    mutationFn: (purchaseId: string) => apiClient.unlinkPurchase(purchaseId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.purchases });
    },
  });

  if (purchases.isLoading) {
    return <LoadingSkeleton lines={7} />;
  }

  if (purchases.isError) {
    return (
      <ErrorState
        title="Purchases unavailable"
        description={getSafeErrorMessage(purchases.error)}
        onAction={() => {
          void purchases.refetch();
        }}
      />
    );
  }

  const items = purchases.data?.purchases ?? [];

  return (
    <div className="space-y-8">
      <header className="rounded-3xl border border-primary/15 bg-gradient-to-br from-primary/[0.10] via-card to-accent/30 px-6 py-8 shadow-sm sm:px-8">
        <div className="flex items-start gap-4">
          <div className="flex size-14 shrink-0 items-center justify-center rounded-2xl bg-primary text-primary-foreground shadow-md shadow-primary/20">
            <ShieldCheck className="size-7" aria-hidden="true" />
          </div>
          <div>
            <p className="mb-2 text-xs font-semibold uppercase tracking-[0.16em] text-primary">
              Purchases and warranties
            </p>
            <h1 className="text-3xl font-bold tracking-tight sm:text-4xl">
              Warranty tracking
            </h1>
            <p className="mt-2 max-w-2xl text-sm leading-6 text-muted-foreground sm:text-base">
              Confirmed receipt items linked to products, manual versions and
              deterministic warranty expiry dates.
            </p>
          </div>
        </div>
      </header>

      {items.length === 0 ? (
        <EmptyState
          icon={ShieldCheck}
          title="No purchases linked"
          description="Open a confirmed receipt and link a line item to start warranty tracking."
        />
      ) : (
        <section className="grid gap-4" aria-label="Tracked purchases">
          {items.map((purchase) => (
            <PurchaseCard
              key={purchase.purchaseId}
              purchase={purchase}
              isRemoving={unlink.isPending}
              onRemove={() => {
                if (window.confirm('Unlink this purchase from its product?')) {
                  unlink.mutate(purchase.purchaseId);
                }
              }}
            />
          ))}
        </section>
      )}
    </div>
  );
}

function PurchaseCard({
  purchase,
  isRemoving,
  onRemove,
}: {
  purchase: PurchaseResponse;
  isRemoving: boolean;
  onRemove: () => void;
}) {
  return (
    <Card className="rounded-2xl border-border/70 shadow-sm">
      <CardHeader className="flex flex-col gap-4 border-b bg-muted/20 p-5 sm:flex-row sm:items-start sm:justify-between">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <h2 className="text-lg font-semibold">
              {purchase.productManufacturer} {purchase.productName}
            </h2>
            <WarrantyStatusBadge status={purchase.warrantyStatus} />
          </div>
          <p className="mt-1 text-sm text-muted-foreground">
            {purchase.modelNumber ?? 'No model'} ·{' '}
            {purchase.receiptLineItemDescription ?? 'Receipt purchase'}
          </p>
        </div>
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={isRemoving}
          onClick={onRemove}
        >
          <Trash2 aria-hidden="true" />
          Unlink
        </Button>
      </CardHeader>
      <CardContent className="grid gap-5 p-5 sm:grid-cols-2 lg:grid-cols-4">
        <Detail
          icon={CalendarDays}
          label="Purchase date"
          value={formatDate(purchase.purchaseDate)}
        />
        <Detail
          icon={Package}
          label="Amount"
          value={formatCurrency(purchase.amount, purchase.currency)}
        />
        <Detail
          icon={ShieldCheck}
          label="Warranty"
          value={
            purchase.warrantyDurationMonthsSnapshot
              ? `${purchase.warrantyDurationMonthsSnapshot.toString()} months`
              : 'Unknown'
          }
        />
        <Detail
          icon={CalendarDays}
          label="Expiry"
          value={
            purchase.warrantyExpiresOn
              ? formatDate(purchase.warrantyExpiresOn)
              : 'Unknown'
          }
        />
        <div className="flex flex-wrap gap-3 sm:col-span-2 lg:col-span-4">
          <Button asChild variant="ghost" size="sm">
            <Link to={`/receipts/${purchase.receiptId}`}>
              <ReceiptText aria-hidden="true" />
              View receipt
            </Link>
          </Button>
          <Button asChild variant="ghost" size="sm">
            <Link to={`/products/${purchase.productId}`}>
              <Package aria-hidden="true" />
              View product
            </Link>
          </Button>
          {purchase.warrantySourceProductManualId ? (
            <Button asChild variant="ghost" size="sm">
              <Link
                to={`/products/${purchase.productId}?manualId=${purchase.warrantySourceProductManualId}`}
              >
                <ShieldCheck aria-hidden="true" />
                Manual {purchase.manualVersionLabel ?? 'version'}
              </Link>
            </Button>
          ) : null}
        </div>
      </CardContent>
    </Card>
  );
}

function Detail({
  icon: Icon,
  label,
  value,
}: {
  icon: LucideIcon;
  label: string;
  value: string;
}) {
  return (
    <div className="flex gap-3">
      <div className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-muted text-muted-foreground">
        <Icon aria-hidden="true" className="size-4" />
      </div>
      <div>
        <p className="text-xs font-medium text-muted-foreground">{label}</p>
        <p className="mt-0.5 text-sm font-semibold">{value}</p>
      </div>
    </div>
  );
}

function WarrantyStatusBadge({
  status,
}: {
  status: PurchaseResponse['warrantyStatus'];
}) {
  const className =
    status === 'Expired'
      ? 'border-destructive/30 bg-destructive/10 text-destructive'
      : status === 'ExpiringSoon'
        ? 'border-warning/30 bg-warning/10 text-warning'
        : status === 'Active'
          ? 'border-success/30 bg-success/10 text-success'
          : 'border-muted bg-muted text-muted-foreground';
  return (
    <span
      className={`rounded-full border px-2.5 py-1 text-xs font-semibold ${className}`}
    >
      {status.replace(/([a-z])([A-Z])/g, '$1 $2')}
    </span>
  );
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium' }).format(
    new Date(value),
  );
}
