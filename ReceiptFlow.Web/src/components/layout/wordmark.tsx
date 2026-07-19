import { ReceiptText } from 'lucide-react';
import { cn } from '@/lib/utils';

interface WordmarkProps {
  compact?: boolean;
  inverse?: boolean;
}

export function Wordmark({ compact = false, inverse = false }: WordmarkProps) {
  return (
    <div className="flex items-center gap-2.5">
      <span
        className={cn(
          'flex size-9 items-center justify-center rounded-lg bg-primary text-primary-foreground shadow-sm',
          inverse && 'bg-white text-emerald-800',
        )}
      >
        <ReceiptText aria-hidden="true" className="size-5" />
      </span>
      {compact ? null : (
        <span className="text-base font-bold tracking-tight">
          ReceiptFlow <span className="text-primary">AI</span>
        </span>
      )}
    </div>
  );
}
