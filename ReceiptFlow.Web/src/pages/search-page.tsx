import { keepPreviousData, useQuery } from '@tanstack/react-query';
import {
  ChevronLeft,
  ChevronRight,
  FileSearch,
  LoaderCircle,
  Search,
  Sparkles,
} from 'lucide-react';
import { useState, type SyntheticEvent } from 'react';
import { getSafeErrorMessage } from '@/api/error-message';
import { queryKeys } from '@/api/query-keys';
import { EmptyState } from '@/components/shared/empty-state';
import { ErrorState } from '@/components/shared/error-state';
import { LoadingSkeleton } from '@/components/shared/loading-skeleton';
import { SearchResultCard } from '@/components/shared/search-result-card';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { useAuth } from '@/providers/use-auth';

const pageSize = 10;

const suggestedSearches = [
  'Electronics purchases',
  'USB cables',
  'Receipts from July',
  'Delivery charges',
];

export function Component() {
  const { apiClient } = useAuth();

  const [draft, setDraft] = useState('');
  const [query, setQuery] = useState('');
  const [page, setPage] = useState(1);

  const request = {
    query,
    page,
    pageSize,
  };

  const search = useQuery({
    queryKey: queryKeys.receiptSearch(request),
    queryFn: ({ signal }) => apiClient.searchReceipts(request, signal),
    enabled: query.length > 0,
    placeholderData: keepPreviousData,
    staleTime: 30_000,
  });

  function performSearch(nextQuery: string) {
    const trimmedQuery = nextQuery.trim();

    if (!trimmedQuery) return;

    setDraft(trimmedQuery);
    setPage(1);

    if (trimmedQuery === query && page === 1) {
      void search.refetch();
    } else {
      setQuery(trimmedQuery);
    }
  }

  function handleSubmit(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault();
    performSearch(draft);
  }

  const totalPages = search.data
    ? Math.max(1, Math.ceil(search.data.total / search.data.pageSize))
    : 1;

  return (
    <div className="space-y-8">
      <SearchPageHeader
        draft={draft}
        isSearching={search.isFetching}
        onDraftChange={setDraft}
        onSubmit={handleSubmit}
      />

      {!query ? (
        <SearchWelcome
          onSuggestion={(suggestion) => {
            performSearch(suggestion);
          }}
        />
      ) : (
        <SearchResultsPanel
          query={query}
          page={page}
          totalPages={totalPages}
          search={search}
          onPreviousPage={() => {
            setPage((current) => Math.max(1, current - 1));
            window.scrollTo({ top: 0, behavior: 'smooth' });
          }}
          onNextPage={() => {
            setPage((current) => Math.min(totalPages, current + 1));
            window.scrollTo({ top: 0, behavior: 'smooth' });
          }}
        />
      )}
    </div>
  );
}

interface SearchPageHeaderProps {
  draft: string;
  isSearching: boolean;
  onDraftChange: (value: string) => void;
  onSubmit: (event: SyntheticEvent<HTMLFormElement>) => void;
}

function SearchPageHeader({
  draft,
  isSearching,
  onDraftChange,
  onSubmit,
}: SearchPageHeaderProps) {
  return (
    <header className="relative isolate overflow-hidden rounded-3xl border border-primary/15 bg-gradient-to-br from-primary/[0.10] via-card to-accent/30 px-6 py-8 shadow-sm sm:px-8 sm:py-10">
      <div
        aria-hidden="true"
        className="absolute -right-24 -top-24 size-72 rounded-full bg-primary/10 blur-3xl"
      />

      <div
        aria-hidden="true"
        className="absolute -bottom-24 left-1/3 size-60 rounded-full bg-emerald-300/10 blur-3xl"
      />

      <div className="relative">
        <div className="flex items-start gap-4">
          <div className="hidden size-14 shrink-0 items-center justify-center rounded-2xl bg-primary text-primary-foreground shadow-md shadow-primary/20 sm:flex">
            <FileSearch className="size-7" aria-hidden="true" />
          </div>

          <div>
            <div className="mb-2 flex w-fit items-center gap-2 rounded-full border border-primary/15 bg-background/70 px-3 py-1 text-xs font-medium text-primary backdrop-blur">
              <Sparkles className="size-3.5" aria-hidden="true" />
              AI-powered hybrid search
            </div>

            <h1 className="text-3xl font-bold tracking-tight sm:text-4xl">
              Search your receipts
            </h1>

            <p className="mt-2 max-w-2xl text-sm leading-6 text-muted-foreground sm:text-base">
              Search by merchant, product, category, date, amount or information
              contained inside your uploaded receipt documents.
            </p>
          </div>
        </div>

        <form
          className="mt-7 flex max-w-4xl flex-col gap-3 sm:flex-row"
          role="search"
          onSubmit={onSubmit}
        >
          <label htmlFor="receipt-search" className="sr-only">
            Search receipts
          </label>

          <div className="relative flex-1">
            <Search
              aria-hidden="true"
              className="absolute left-4 top-1/2 size-5 -translate-y-1/2 text-muted-foreground"
            />

            <input
              id="receipt-search"
              type="search"
              value={draft}
              onChange={(event) => {
                onDraftChange(event.target.value);
              }}
              maxLength={1000}
              placeholder="For example: electronics purchases with delivery..."
              autoComplete="off"
              className="h-14 w-full rounded-2xl border border-border/80 bg-background/90 pl-12 pr-4 text-sm shadow-md outline-none backdrop-blur transition placeholder:text-muted-foreground focus:border-primary focus:ring-4 focus:ring-primary/10"
            />
          </div>

          <Button
            type="submit"
            size="lg"
            disabled={!draft.trim() || isSearching}
            className="h-14 min-w-32 rounded-2xl shadow-md shadow-primary/15"
          >
            {isSearching ? (
              <LoaderCircle
                className="animate-spin motion-reduce:animate-none"
                aria-hidden="true"
              />
            ) : (
              <Search aria-hidden="true" />
            )}

            {isSearching ? 'Searching…' : 'Search'}
          </Button>
        </form>

        <p className="mt-3 text-xs text-muted-foreground">
          Search results are restricted to receipts belonging to your account.
        </p>
      </div>
    </header>
  );
}

function SearchWelcome({
  onSuggestion,
}: {
  onSuggestion: (suggestion: string) => void;
}) {
  return (
    <Card className="overflow-hidden rounded-3xl border-border/70 shadow-sm">
      <div className="px-6 py-10 text-center sm:px-10 sm:py-14">
        <div className="mx-auto flex size-16 items-center justify-center rounded-2xl bg-primary/10 text-primary">
          <Search className="size-8" aria-hidden="true" />
        </div>

        <h2 className="mt-5 text-xl font-semibold tracking-tight">
          What would you like to find?
        </h2>

        <p className="mx-auto mt-2 max-w-xl text-sm leading-6 text-muted-foreground">
          ReceiptFlow combines document text and semantic meaning to find
          relevant purchases, even when your wording differs from the receipt.
        </p>

        <div className="mt-7">
          <p className="mb-3 text-xs font-medium uppercase tracking-wider text-muted-foreground">
            Try a suggested search
          </p>

          <div className="flex flex-wrap justify-center gap-2">
            {suggestedSearches.map((suggestion) => (
              <Button
                key={suggestion}
                type="button"
                variant="outline"
                className="rounded-full bg-background"
                onClick={() => {
                  onSuggestion(suggestion);
                }}
              >
                <Search className="size-3.5" aria-hidden="true" />
                {suggestion}
              </Button>
            ))}
          </div>
        </div>
      </div>
    </Card>
  );
}

interface SearchResultsPanelProps {
  query: string;
  page: number;
  totalPages: number;
  search: ReturnType<typeof useReceiptSearchResult>;
  onPreviousPage: () => void;
  onNextPage: () => void;
}

/*
 * This helper only provides the TypeScript type used by SearchResultsPanel.
 * It is never called at runtime.
 */
// eslint-disable-next-line @typescript-eslint/no-unused-vars
function useReceiptSearchResult() {
  const { apiClient } = useAuth();

  return useQuery({
    queryKey: ['receipt-search-type'],
    queryFn: ({ signal }) =>
      apiClient.searchReceipts(
        {
          query: '',
          page: 1,
          pageSize,
        },
        signal,
      ),
    enabled: false,
  });
}

function SearchResultsPanel({
  query,
  page,
  totalPages,
  search,
  onPreviousPage,
  onNextPage,
}: SearchResultsPanelProps) {
  return (
    <section
      aria-live="polite"
      aria-labelledby="search-results-title"
      className="relative overflow-hidden rounded-3xl border bg-card shadow-sm"
    >
      {search.isFetching && !search.isLoading ? (
        <div
          className="absolute inset-x-0 top-0 h-1 overflow-hidden bg-primary/10"
          role="status"
          aria-label="Updating search results"
        >
          <div className="h-full w-1/3 animate-pulse rounded-full bg-primary" />
        </div>
      ) : null}

      <div className="flex flex-col gap-3 border-b bg-muted/20 px-5 py-5 sm:flex-row sm:items-center sm:justify-between sm:px-6">
        <div>
          <h2
            id="search-results-title"
            className="text-lg font-semibold tracking-tight"
          >
            Search results
          </h2>

          <p className="mt-1 text-sm text-muted-foreground">
            Results for{' '}
            <span className="font-medium text-foreground">“{query}”</span>
          </p>
        </div>

        {search.data ? (
          <span className="w-fit rounded-full border bg-background px-3 py-1.5 text-sm font-medium shadow-sm">
            {search.data.total.toLocaleString('en-GB')}{' '}
            {search.data.total === 1 ? 'match' : 'matches'}
          </span>
        ) : null}
      </div>

      <div className="p-5 sm:p-6">
        {search.isLoading ? (
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
            description={`No receipt evidence matched “${query}”. Try a merchant, product name or broader description.`}
          />
        ) : search.data ? (
          <div className="space-y-6">
            <div className="space-y-4">
              {search.data.matches.map((match) => (
                <SearchResultCard
                  key={`${match.documentId}-${match.chunkIndex.toString()}`}
                  result={match}
                />
              ))}
            </div>

            <SearchPagination
              page={page}
              totalPages={totalPages}
              totalResults={search.data.total}
              isFetching={search.isFetching}
              onPrevious={onPreviousPage}
              onNext={onNextPage}
            />
          </div>
        ) : null}
      </div>
    </section>
  );
}

interface SearchPaginationProps {
  page: number;
  totalPages: number;
  totalResults: number;
  isFetching: boolean;
  onPrevious: () => void;
  onNext: () => void;
}

function SearchPagination({
  page,
  totalPages,
  totalResults,
  isFetching,
  onPrevious,
  onNext,
}: SearchPaginationProps) {
  const firstResult = (page - 1) * pageSize + 1;
  const lastResult = Math.min(page * pageSize, totalResults);

  return (
    <nav
      aria-label="Search result pages"
      className="flex flex-col gap-4 rounded-2xl border bg-muted/20 p-3 sm:flex-row sm:items-center sm:justify-between"
    >
      <Button
        type="button"
        variant="outline"
        disabled={page <= 1 || isFetching}
        onClick={onPrevious}
        className="bg-background"
      >
        <ChevronLeft aria-hidden="true" />
        Previous
      </Button>

      <div className="text-center">
        <p className="text-sm font-medium">
          Page {page.toLocaleString('en-GB')} of{' '}
          {totalPages.toLocaleString('en-GB')}
        </p>

        <p className="mt-0.5 text-xs text-muted-foreground">
          Showing {firstResult.toLocaleString('en-GB')}–
          {lastResult.toLocaleString('en-GB')} of{' '}
          {totalResults.toLocaleString('en-GB')}
        </p>
      </div>

      <Button
        type="button"
        variant="outline"
        disabled={page >= totalPages || isFetching}
        onClick={onNext}
        className="bg-background"
      >
        Next
        <ChevronRight aria-hidden="true" />
      </Button>
    </nav>
  );
}

function SearchLoading() {
  return (
    <div
      className="space-y-4"
      role="status"
      aria-label="Searching receipts"
      aria-busy="true"
    >
      {[1, 2, 3].map((item) => (
        <Card key={item} className="rounded-2xl border-border/70 p-5 shadow-sm">
          <LoadingSkeleton lines={3} />
        </Card>
      ))}
    </div>
  );
}
