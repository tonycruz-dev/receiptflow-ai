import { CircleAlert } from 'lucide-react';
import { Button } from '@/components/ui/button';

interface ErrorStateProps {
  title?: string;
  description?: string;
  actionLabel?: string;
  onAction?: () => void;
}

export function ErrorState({
  title = 'Something went wrong',
  description = 'We could not complete this request. Please try again.',
  actionLabel = 'Try again',
  onAction,
}: ErrorStateProps) {
  return (
    <section
      className="w-full max-w-lg rounded-xl border bg-card p-8 text-center shadow-sm"
      role="alert"
    >
      <div className="mx-auto flex size-12 items-center justify-center rounded-xl bg-destructive/10 text-destructive">
        <CircleAlert aria-hidden="true" />
      </div>
      <h1 className="mt-4 text-xl font-bold">{title}</h1>
      <p className="mt-2 text-sm text-muted-foreground">{description}</p>
      {onAction ? (
        <Button className="mt-5" onClick={onAction}>
          {actionLabel}
        </Button>
      ) : null}
    </section>
  );
}
