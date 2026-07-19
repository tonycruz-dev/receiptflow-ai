import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Search } from 'lucide-react';
import { useState, type SyntheticEvent } from 'react';
import { getSafeErrorMessage } from '@/api/error-message';
import { queryKeys } from '@/api/query-keys';
import { EmptyState } from '@/components/shared/empty-state';
import { ErrorState } from '@/components/shared/error-state';
import { LoadingSkeleton } from '@/components/shared/loading-skeleton';
import { PageHeader } from '@/components/shared/page-header';
import { SearchResultCard } from '@/components/shared/search-result-card';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { useAuth } from '@/providers/use-auth';

const pageSize = 10;

export function Component() {
  const { apiClient } = useAuth();
  const [draft, setDraft] = useState('');
  const [query, setQuery] = useState('');
  const [page, setPage] = useState(1);
  const request = { query, page, pageSize };
  const search = useQuery({
    queryKey: queryKeys.receiptSearch(request),
    queryFn: ({ signal }) => apiClient.searchReceipts(request, signal),
    enabled: query.length > 0,
    placeholderData: keepPreviousData,
    staleTime: 30_000,
  });

  function handleSubmit(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault();
    const nextQuery = draft.trim();
    if (!nextQuery) return;
    setPage(1);
    if (nextQuery === query && page === 1) void search.refetch();
    else setQuery(nextQuery);
  }

  const totalPages = search.data
    ? Math.max(1, Math.ceil(search.data.total / search.data.pageSize))
    : 1;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Receipt search"
        description="Find receipts by merchant, date, amount or document content."
      />
      <form className="flex max-w-3xl gap-2" onSubmit={handleSubmit}>
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
            value={draft}
            onChange={(event) => {
              setDraft(event.target.value);
            }}
            maxLength={1000}
            placeholder="Search receipts"
            className="h-11 w-full rounded-lg border bg-card pr-4 pl-10 text-sm shadow-sm placeholder:text-muted-foreground"
          />
        </div>
        <Button type="submit" disabled={!draft.trim() || search.isFetching}>
          Search
        </Button>
      </form>

      {!query ? (
        <EmptyState
          icon={Search}
          title="Search your receipt library"
          description="Enter a merchant, product or expense to search your indexed receipt documents."
        />
      ) : search.isLoading ? (
        <SearchLoading />
      ) : search.isError ? (
        <ErrorState
          title="Search unavailable"
          description={getSafeErrorMessage(search.error)}
          onAction={() => {
            void search.refetch();
          }}
        />
      ) : search.data?.matches.length === 0 ? (
        <EmptyState
          icon={Search}
          title="No matching receipts"
          description={`No receipt evidence matched “${query}”. Try a broader search.`}
        />
      ) : search.data ? (
        <section aria-live="polite" aria-label="Receipt search results">
          <div className="mb-4 flex items-center justify-between gap-3">
            <p className="text-sm text-muted-foreground">
              {search.data.total.toLocaleString('en-GB')} results for{' '}
              <span className="font-medium text-foreground">“{query}”</span>
            </p>
            {search.isFetching ? (
              <span className="text-xs text-muted-foreground">Updating…</span>
            ) : null}
          </div>
          <div className="space-y-3">
            {search.data.matches.map((match) => (
              <SearchResultCard
                key={`${match.documentId}-${match.chunkIndex.toString()}`}
                result={match}
              />
            ))}
          </div>
          <nav
            aria-label="Search result pages"
            className="mt-5 flex items-center justify-between rounded-lg border bg-card p-3"
          >
            <Button
              variant="outline"
              size="sm"
              disabled={page <= 1 || search.isFetching}
              onClick={() => {
                setPage((current) => Math.max(1, current - 1));
              }}
            >
              Previous
            </Button>
            <span className="text-sm text-muted-foreground">
              Page {page} of {totalPages}
            </span>
            <Button
              variant="outline"
              size="sm"
              disabled={page >= totalPages || search.isFetching}
              onClick={() => {
                setPage((current) => Math.min(totalPages, current + 1));
              }}
            >
              Next
            </Button>
          </nav>
        </section>
      ) : null}
    </div>
  );
}

function SearchLoading() {
  return (
    <div className="space-y-3" role="status" aria-label="Searching receipts">
      {[1, 2, 3].map((item) => (
        <Card key={item} className="p-5">
          <LoadingSkeleton lines={3} />
        </Card>
      ))}
    </div>
  );
}
