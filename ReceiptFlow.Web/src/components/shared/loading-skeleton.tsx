import { cn } from '@/lib/utils';

interface LoadingSkeletonProps {
  className?: string;
  lines?: number;
}

export function LoadingSkeleton({
  className,
  lines = 3,
}: LoadingSkeletonProps) {
  return (
    <div
      className={cn('space-y-3', className)}
      role="status"
      aria-label="Loading content"
    >
      {Array.from({ length: lines }, (_, index) => (
        <div
          // Skeleton lines are presentation-only and have no stable data id.
          key={index}
          className={cn(
            'h-4 animate-pulse rounded-md bg-muted',
            index === lines - 1 && 'w-2/3',
          )}
        />
      ))}
      <span className="sr-only">Loading</span>
    </div>
  );
}
