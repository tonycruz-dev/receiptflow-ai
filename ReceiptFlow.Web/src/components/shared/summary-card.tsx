import type { LucideIcon } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';

interface SummaryCardProps {
  label: string;
  value: string;
  detail: string;
  icon: LucideIcon;
  tone?: 'primary' | 'success' | 'processing';
}

const tones = {
  primary: 'bg-accent text-accent-foreground',
  success: 'bg-success/10 text-success',
  processing: 'bg-processing/10 text-processing',
};

export function SummaryCard({
  label,
  value,
  detail,
  icon: Icon,
  tone = 'primary',
}: SummaryCardProps) {
  return (
    <Card>
      <CardContent className="flex items-start justify-between gap-4 p-5">
        <div>
          <p className="text-sm font-medium text-muted-foreground">{label}</p>
          <p className="mt-2 text-2xl font-bold tracking-tight tabular-nums">
            {value}
          </p>
          <p className="mt-1 text-xs text-muted-foreground">{detail}</p>
        </div>
        <div
          className={`flex size-10 shrink-0 items-center justify-center rounded-lg ${tones[tone]}`}
        >
          <Icon aria-hidden="true" className="size-5" />
        </div>
      </CardContent>
    </Card>
  );
}
