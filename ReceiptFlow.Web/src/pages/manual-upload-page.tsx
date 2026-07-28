import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CircleAlert, FileUp, Plus } from 'lucide-react';
import { useState, type SyntheticEvent } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { getSafeErrorMessage } from '@/api/error-message';
import { queryKeys } from '@/api/query-keys';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { useAuth } from '@/providers/use-auth';

const inputClass =
  'h-11 w-full rounded-lg border bg-background px-3 text-sm shadow-sm';

export function Component() {
  const { apiClient } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [searchParams] = useSearchParams();
  const [selectedProductId, setSelectedProductId] = useState(
    searchParams.get('productId') ?? '',
  );
  const [file, setFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState<string>();
  const [showCreate, setShowCreate] = useState(false);
  const supersedesProductManualId =
    searchParams.get('supersedesProductManualId') ?? undefined;
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
    onSuccess: async (product) => {
      setSelectedProductId(product.productId);
      setShowCreate(false);
      await queryClient.invalidateQueries({ queryKey: queryKeys.productLists });
    },
  });
  const upload = useMutation({
    mutationFn: (selected: File) =>
      apiClient.uploadProductManual(selectedProductId, selected, {
        ...(supersedesProductManualId ? { supersedesProductManualId } : {}),
      }),
    onSuccess: async (manual) => {
      await queryClient.invalidateQueries({
        queryKey: queryKeys.productManuals(manual.productId),
      });
      await navigate(
        `/products/${encodeURIComponent(manual.productId)}?manualId=${encodeURIComponent(manual.productManualId)}`,
      );
    },
  });

  function chooseFile(selected: File | null) {
    setFile(selected);
    const error =
      selected && selected.type !== 'application/pdf'
        ? 'Choose a PDF product manual.'
        : selected && selected.size > 10 * 1024 * 1024
          ? 'The product manual must be 10 MB or smaller.'
          : undefined;
    setFileError(error);
  }

  function submit(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selectedProductId || !file || fileError) return;
    upload.mutate(file);
  }

  return (
    <div className="space-y-8">
      <header className="rounded-3xl border border-primary/15 bg-gradient-to-br from-primary/[0.12] via-card to-accent/40 px-6 py-8 shadow-sm sm:px-8">
        <h1 className="text-3xl font-bold tracking-tight">
          {supersedesProductManualId
            ? 'Upload replacement manual'
            : 'Upload product manual'}
        </h1>
        <p className="mt-2 text-muted-foreground">
          Select or create a product, then upload its PDF manual.
        </p>
      </header>

      <Card className="rounded-3xl">
        <CardHeader className="border-b bg-muted/20 p-6">
          <h2 className="text-xl font-semibold">Product and PDF</h2>
        </CardHeader>
        <CardContent className="p-6">
          <form className="space-y-6" onSubmit={submit}>
            <div>
              <label
                htmlFor="manual-product"
                className="mb-2 block text-sm font-medium"
              >
                Product
              </label>
              <div className="flex flex-col gap-3 sm:flex-row">
                <select
                  id="manual-product"
                  className={inputClass}
                  required
                  value={selectedProductId}
                  onChange={(event) => {
                    setSelectedProductId(event.target.value);
                  }}
                >
                  <option value="">Select a product</option>
                  {products.data?.map((product) => (
                    <option key={product.productId} value={product.productId}>
                      {product.manufacturer} {product.name}
                      {product.modelNumber ? ` · ${product.modelNumber}` : ''}
                    </option>
                  ))}
                </select>
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => {
                    setShowCreate((value) => !value);
                  }}
                >
                  <Plus aria-hidden="true" />
                  Create product
                </Button>
              </div>
            </div>

            {showCreate ? (
              <InlineCreateProduct
                pending={create.isPending}
                onCreate={(values) => {
                  create.mutate(values);
                }}
              />
            ) : null}

            <label className="block space-y-2 text-sm font-medium">
              Product manual PDF
              <input
                className={inputClass}
                type="file"
                accept="application/pdf,.pdf"
                required
                onChange={(event) => {
                  chooseFile(event.currentTarget.files?.[0] ?? null);
                }}
              />
            </label>
            {fileError ? (
              <p className="text-sm text-destructive" role="alert">
                {fileError}
              </p>
            ) : null}
            {upload.isError ? (
              <div
                className="flex gap-3 rounded-xl border border-destructive/30 bg-destructive/10 p-4"
                role="alert"
              >
                <CircleAlert
                  className="size-5 text-destructive"
                  aria-hidden="true"
                />
                <p className="text-sm">{getSafeErrorMessage(upload.error)}</p>
              </div>
            ) : null}

            <div className="flex items-center justify-between border-t pt-5">
              <Button asChild variant="ghost">
                <Link to="/products">Cancel</Link>
              </Button>
              <Button
                type="submit"
                disabled={
                  upload.isPending ||
                  !selectedProductId ||
                  !file ||
                  Boolean(fileError)
                }
              >
                <FileUp aria-hidden="true" />
                {upload.isPending ? 'Uploading…' : 'Upload manual'}
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}

function InlineCreateProduct({
  pending,
  onCreate,
}: {
  pending: boolean;
  onCreate: (values: {
    manufacturer: string;
    name: string;
    modelNumber: string | null;
  }) => void;
}) {
  const [manufacturer, setManufacturer] = useState('');
  const [name, setName] = useState('');
  const [modelNumber, setModelNumber] = useState('');
  return (
    <fieldset className="grid gap-3 rounded-2xl border bg-muted/20 p-4 md:grid-cols-3">
      <legend className="px-2 text-sm font-semibold">New product</legend>
      <label className="space-y-2 text-sm">
        Manufacturer
        <input
          className={inputClass}
          value={manufacturer}
          onChange={(event) => {
            setManufacturer(event.target.value);
          }}
        />
      </label>
      <label className="space-y-2 text-sm">
        Product name
        <input
          className={inputClass}
          value={name}
          onChange={(event) => {
            setName(event.target.value);
          }}
        />
      </label>
      <label className="space-y-2 text-sm">
        Model number
        <input
          className={inputClass}
          value={modelNumber}
          onChange={(event) => {
            setModelNumber(event.target.value);
          }}
        />
      </label>
      <Button
        type="button"
        className="md:col-span-3 md:w-fit"
        disabled={pending || !manufacturer.trim() || !name.trim()}
        onClick={() => {
          onCreate({
            manufacturer: manufacturer.trim(),
            name: name.trim(),
            modelNumber: modelNumber.trim() || null,
          });
        }}
      >
        {pending ? 'Creating…' : 'Create and select'}
      </Button>
    </fieldset>
  );
}
