import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Package, Plus } from 'lucide-react';
import { useState, type SyntheticEvent } from 'react';
import { Link } from 'react-router-dom';
import { getSafeErrorMessage } from '@/api/error-message';
import { queryKeys } from '@/api/query-keys';
import { EmptyState } from '@/components/shared/empty-state';
import { ErrorState } from '@/components/shared/error-state';
import { LoadingSkeleton } from '@/components/shared/loading-skeleton';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { useAuth } from '@/providers/use-auth';

const inputClass =
  'h-11 w-full rounded-lg border bg-background px-3 text-sm shadow-sm';

export function Component() {
  const { apiClient } = useAuth();
  const queryClient = useQueryClient();
  const [showCreate, setShowCreate] = useState(false);
  const products = useQuery({
    queryKey: queryKeys.products,
    queryFn: ({ signal }) => apiClient.listProducts(signal),
  });
  const create = useMutation({
    mutationFn: (values: {
      manufacturer: string;
      name: string;
      modelNumber: string | null;
    }) => apiClient.createProduct(values),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.productLists });
      setShowCreate(false);
    },
  });

  return (
    <div className="space-y-8">
      <header className="flex flex-col justify-between gap-5 rounded-3xl border border-primary/15 bg-gradient-to-br from-primary/[0.12] via-card to-accent/40 px-6 py-8 shadow-sm sm:flex-row sm:items-center sm:px-8">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Products</h1>
          <p className="mt-2 text-muted-foreground">
            Keep product manuals and their version history together.
          </p>
        </div>
        <div className="flex gap-3">
          <Button
            variant="outline"
            onClick={() => {
              setShowCreate((value) => !value);
            }}
          >
            <Plus aria-hidden="true" />
            Create product
          </Button>
          <Button asChild>
            <Link to="/products/manuals/new">Upload manual</Link>
          </Button>
        </div>
      </header>

      {showCreate ? (
        <CreateProductForm
          pending={create.isPending}
          error={create.error}
          onSubmit={(values) => create.mutateAsync(values)}
        />
      ) : null}

      {products.isLoading ? (
        <LoadingSkeleton lines={4} />
      ) : products.isError ? (
        <ErrorState
          title="Products unavailable"
          description={getSafeErrorMessage(products.error)}
          onAction={() => void products.refetch()}
        />
      ) : products.data?.length ? (
        <section
          aria-label="Product library"
          className="grid gap-5 md:grid-cols-2 xl:grid-cols-3"
        >
          {products.data.map((product) => (
            <Link
              key={product.productId}
              to={`/products/${encodeURIComponent(product.productId)}`}
              className="rounded-2xl"
            >
              <Card className="h-full rounded-2xl transition hover:border-primary/40 hover:shadow-md">
                <CardContent className="flex items-start gap-4 p-6">
                  <div className="grid size-11 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary">
                    <Package aria-hidden="true" />
                  </div>
                  <div>
                    <p className="text-sm font-medium text-muted-foreground">
                      {product.manufacturer}
                    </p>
                    <h2 className="mt-1 text-lg font-semibold">
                      {product.name}
                    </h2>
                    <p className="mt-2 text-sm text-muted-foreground">
                      {product.modelNumber ?? 'No model number'}
                    </p>
                  </div>
                </CardContent>
              </Card>
            </Link>
          ))}
        </section>
      ) : (
        <EmptyState
          icon={Package}
          title="No products yet"
          description="Create a product before adding its first manual."
          action={
            <Button
              onClick={() => {
                setShowCreate(true);
              }}
            >
              <Plus aria-hidden="true" />
              Create product
            </Button>
          }
        />
      )}
    </div>
  );
}

function CreateProductForm({
  pending,
  error,
  onSubmit,
}: {
  pending: boolean;
  error: unknown;
  onSubmit: (values: {
    manufacturer: string;
    name: string;
    modelNumber: string | null;
  }) => Promise<unknown>;
}) {
  const [manufacturer, setManufacturer] = useState('');
  const [name, setName] = useState('');
  const [modelNumber, setModelNumber] = useState('');

  function submit(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!manufacturer.trim() || !name.trim()) return;
    void onSubmit({
      manufacturer: manufacturer.trim(),
      name: name.trim(),
      modelNumber: modelNumber.trim() || null,
    });
  }

  return (
    <Card className="rounded-2xl">
      <CardHeader>
        <h2 className="text-lg font-semibold">Create product</h2>
      </CardHeader>
      <CardContent>
        <form className="grid gap-4 md:grid-cols-3" onSubmit={submit}>
          <label className="space-y-2 text-sm font-medium">
            Manufacturer
            <input
              className={inputClass}
              required
              value={manufacturer}
              onChange={(event) => {
                setManufacturer(event.target.value);
              }}
            />
          </label>
          <label className="space-y-2 text-sm font-medium">
            Product name
            <input
              className={inputClass}
              required
              value={name}
              onChange={(event) => {
                setName(event.target.value);
              }}
            />
          </label>
          <label className="space-y-2 text-sm font-medium">
            Model number
            <input
              className={inputClass}
              value={modelNumber}
              onChange={(event) => {
                setModelNumber(event.target.value);
              }}
            />
          </label>
          <div className="md:col-span-3">
            {error ? (
              <p className="mb-3 text-sm text-destructive" role="alert">
                {getSafeErrorMessage(error)}
              </p>
            ) : null}
            <Button disabled={pending || !manufacturer.trim() || !name.trim()}>
              {pending ? 'Creating…' : 'Create product'}
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}
