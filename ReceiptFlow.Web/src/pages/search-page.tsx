import { Search } from 'lucide-react';
import { EmptyState } from '@/components/shared/empty-state';
import { PageHeader } from '@/components/shared/page-header';
import { Button } from '@/components/ui/button';

export function Component() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Receipt search"
        description="Find receipts by merchant, date, amount or document content."
      />
      <form
        className="flex max-w-3xl gap-2"
        onSubmit={(event) => {
          event.preventDefault();
        }}
      >
        <label htmlFor="receipt-search" className="sr-only">
          Search receipts
        </label>
        <div className="relative flex-1">
          <Search
            aria-hidden="true"
            className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground"
          />
          <input
            id="receipt-search"
            type="search"
            placeholder="Search receipts"
            className="h-11 w-full rounded-lg border bg-card pr-4 pl-10 text-sm shadow-sm placeholder:text-muted-foreground"
          />
        </div>
        <Button type="submit" disabled>
          Search
        </Button>
      </form>
      <EmptyState
        icon={Search}
        title="Search your receipt library"
        description="Search results will appear here once the API connection is added. No live search is made from this foundation."
      />
    </div>
  );
}
