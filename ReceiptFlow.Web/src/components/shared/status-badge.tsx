import { cva, type VariantProps } from 'class-variance-authority';
import { CircleCheck, CircleX, Clock3, LoaderCircle } from 'lucide-react';
import { cn } from '@/lib/utils';

export const receiptStatuses = [
  'Draft',
  'Pending',
  'Processing',
  'Review required',
  'Completed',
  'Confirmed',
  'Failed',
] as const;

export type ReceiptStatus = (typeof receiptStatuses)[number];

const statusVariants = cva(
  'inline-flex w-fit items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-semibold',
  {
    variants: {
      status: {
        Draft: 'border-border bg-muted text-muted-foreground',
        Pending: 'border-warning/35 bg-warning/12 text-warning',
        Processing: 'border-processing/35 bg-processing/12 text-processing',
        'Review required': 'border-warning/35 bg-warning/12 text-warning',
        Completed: 'border-success/35 bg-success/12 text-success',
        Confirmed: 'border-success/35 bg-success/12 text-success',
        Failed: 'border-destructive/35 bg-destructive/10 text-destructive',
      },
    },
  },
);

const statusIcons = {
  Draft: Clock3,
  Pending: Clock3,
  Processing: LoaderCircle,
  'Review required': Clock3,
  Completed: CircleCheck,
  Confirmed: CircleCheck,
  Failed: CircleX,
} satisfies Record<ReceiptStatus, typeof Clock3>;

interface StatusBadgeProps extends VariantProps<typeof statusVariants> {
  status: ReceiptStatus;
  className?: string;
}

export function StatusBadge({ status, className }: StatusBadgeProps) {
  const Icon = statusIcons[status];
  return (
    <span className={cn(statusVariants({ status }), className)}>
      <Icon aria-hidden="true" className="size-3.5" />
      {status}
    </span>
  );
}
