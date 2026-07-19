import { LoadingSkeleton } from '@/components/shared/loading-skeleton';

export function RouteLoading() {
  return (
    <main className="mx-auto w-full max-w-7xl space-y-6 p-4 sm:p-6 lg:p-8">
      <LoadingSkeleton className="max-w-lg" lines={2} />
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <LoadingSkeleton className="rounded-xl border bg-card p-5" />
        <LoadingSkeleton className="rounded-xl border bg-card p-5" />
        <LoadingSkeleton className="rounded-xl border bg-card p-5" />
      </div>
    </main>
  );
}
