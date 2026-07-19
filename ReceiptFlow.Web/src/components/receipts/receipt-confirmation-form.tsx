import { useMutation, useQueryClient } from '@tanstack/react-query';
import { CircleCheck, LoaderCircle, Plus, Trash2 } from 'lucide-react';
import { useState, type SyntheticEvent } from 'react';
import type {
  ConfirmReceiptLineItem,
  ConfirmReceiptRequest,
  ReceiptDocumentExtraction,
} from '@/api/contracts';
import { getSafeErrorMessage } from '@/api/error-message';
import { queryKeys } from '@/api/query-keys';
import { Button } from '@/components/ui/button';
import { useAuth } from '@/providers/use-auth';

interface ReceiptConfirmationFormProps {
  receiptId: string;
  extraction: ReceiptDocumentExtraction | null;
  manualEntry?: boolean;
  onConfirmed: () => void;
}

interface ReviewValues {
  merchantName: string;
  purchaseDate: string;
  subtotal: string;
  tax: string;
  totalAmount: string;
  currency: string;
  category: string;
  lineItems: ReviewLineItem[];
}

interface ReviewLineItem {
  key: string;
  description: string;
  quantity: string;
  unitPrice: string;
  totalPrice: string;
  tax: string;
}

const inputClassName =
  'mt-1.5 h-10 w-full rounded-md border bg-background px-3 text-sm outline-none focus-visible:border-ring focus-visible:ring-2 focus-visible:ring-ring/30';

export function ReceiptConfirmationForm({
  receiptId,
  extraction,
  manualEntry = false,
  onConfirmed,
}: ReceiptConfirmationFormProps) {
  const { apiClient } = useAuth();
  const queryClient = useQueryClient();
  const [values, setValues] = useState(() => createInitialValues(extraction));
  const [error, setError] = useState<string>();
  const [confirmed, setConfirmed] = useState(false);
  const confirmation = useMutation({
    mutationFn: (request: ConfirmReceiptRequest) =>
      apiClient.confirmReceipt(receiptId, request),
  });

  async function handleSubmit(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault();
    const request = toRequest(values, manualEntry);
    if (typeof request === 'string') {
      setError(request);
      return;
    }

    setError(undefined);
    try {
      const receipt = await confirmation.mutateAsync(request);
      queryClient.setQueryData(queryKeys.receipt(receiptId), receipt);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.dashboard }),
        queryClient.invalidateQueries({ queryKey: queryKeys.receiptLists }),
        queryClient.invalidateQueries({
          queryKey: queryKeys.receiptDocuments(receiptId),
        }),
        queryClient.invalidateQueries({ queryKey: queryKeys.receiptSearches }),
      ]);
      setConfirmed(true);
      onConfirmed();
    } catch (caught) {
      setError(
        getSafeErrorMessage(
          caught,
          'The receipt could not be confirmed. Check the values and try again.',
        ),
      );
    }
  }

  function updateField(
    name: keyof Omit<ReviewValues, 'lineItems'>,
    value: string,
  ) {
    setValues((current) => ({ ...current, [name]: value }));
  }

  function updateLineItem(
    key: string,
    name: keyof Omit<ReviewLineItem, 'key'>,
    value: string,
  ) {
    setValues((current) => ({
      ...current,
      lineItems: current.lineItems.map((item) =>
        item.key === key ? { ...item, [name]: value } : item,
      ),
    }));
  }

  if (confirmed) {
    return (
      <div
        className="rounded-lg border border-success/30 bg-success/8 p-4"
        role="status"
      >
        <div className="flex items-center gap-2 font-semibold text-success">
          <CircleCheck aria-hidden="true" />
          Receipt confirmed
        </div>
        <p className="mt-1 text-sm text-muted-foreground">
          Your corrected receipt is saved and queued for search indexing.
        </p>
      </div>
    );
  }

  return (
    <form
      className="space-y-6"
      noValidate
      onSubmit={(event) => {
        void handleSubmit(event);
      }}
    >
      <div>
        <h3 className="text-base font-semibold">
          {manualEntry
            ? 'Enter receipt details'
            : 'Review AI-extracted suggestions'}
        </h3>
        <p className="mt-1 text-sm text-muted-foreground">
          {manualEntry
            ? 'Extraction failed, so enter the receipt values manually before confirming.'
            : 'Check every value and correct anything necessary. Nothing is confirmed automatically.'}
        </p>
        {!manualEntry && extraction?.overallConfidence != null ? (
          <p className="mt-2 text-xs font-medium text-processing">
            Overall extraction confidence:{' '}
            {(extraction.overallConfidence * 100).toFixed(0)}%
          </p>
        ) : null}
      </div>

      {error ? (
        <div
          className="rounded-lg border border-destructive/30 bg-destructive/8 p-3 text-sm"
          role="alert"
        >
          {error}
        </div>
      ) : null}

      <div className="grid gap-4 sm:grid-cols-2">
        <Field
          label="Merchant name"
          value={values.merchantName}
          required
          onChange={(value) => {
            updateField('merchantName', value);
          }}
        />
        <Field
          label="Purchase date"
          type="date"
          value={values.purchaseDate}
          required
          onChange={(value) => {
            updateField('purchaseDate', value);
          }}
        />
        <Field
          label="Subtotal"
          type="number"
          value={values.subtotal}
          onChange={(value) => {
            updateField('subtotal', value);
          }}
        />
        <Field
          label="Tax"
          type="number"
          value={values.tax}
          onChange={(value) => {
            updateField('tax', value);
          }}
        />
        <Field
          label="Total amount"
          type="number"
          value={values.totalAmount}
          required
          onChange={(value) => {
            updateField('totalAmount', value);
          }}
        />
        <Field
          label="ISO currency"
          value={values.currency}
          required
          maxLength={3}
          onChange={(value) => {
            updateField('currency', value.toUpperCase());
          }}
        />
        <div className="sm:col-span-2">
          <Field
            label="Category"
            value={values.category}
            required
            onChange={(value) => {
              updateField('category', value);
            }}
          />
        </div>
      </div>

      <section aria-labelledby="review-line-items">
        <div className="flex items-center justify-between gap-3">
          <h3 id="review-line-items" className="text-sm font-semibold">
            Line items
          </h3>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => {
              setValues((current) => ({
                ...current,
                lineItems: [...current.lineItems, emptyLineItem()],
              }));
            }}
          >
            <Plus aria-hidden="true" /> Add item
          </Button>
        </div>
        {values.lineItems.length === 0 ? (
          <p className="mt-2 text-sm text-muted-foreground">
            No line items were extracted. Adding them is optional.
          </p>
        ) : (
          <div className="mt-3 space-y-3">
            {values.lineItems.map((item, index) => (
              <fieldset
                key={item.key}
                className="grid gap-3 rounded-lg border p-3 sm:grid-cols-2 xl:grid-cols-5"
              >
                <legend className="sr-only">
                  Line item {(index + 1).toString()}
                </legend>
                <div className="sm:col-span-2 xl:col-span-1">
                  <Field
                    id={`line-${item.key}-description`}
                    label="Description"
                    value={item.description}
                    required
                    onChange={(value) => {
                      updateLineItem(item.key, 'description', value);
                    }}
                  />
                </div>
                <Field
                  id={`line-${item.key}-quantity`}
                  label="Quantity"
                  type="number"
                  value={item.quantity}
                  required
                  onChange={(value) => {
                    updateLineItem(item.key, 'quantity', value);
                  }}
                />
                <Field
                  id={`line-${item.key}-unit-price`}
                  label="Unit price"
                  type="number"
                  value={item.unitPrice}
                  required
                  onChange={(value) => {
                    updateLineItem(item.key, 'unitPrice', value);
                  }}
                />
                <Field
                  id={`line-${item.key}-total`}
                  label="Line total"
                  type="number"
                  value={item.totalPrice}
                  onChange={(value) => {
                    updateLineItem(item.key, 'totalPrice', value);
                  }}
                />
                <div className="flex items-end">
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    onClick={() => {
                      setValues((current) => ({
                        ...current,
                        lineItems: current.lineItems.filter(
                          (candidate) => candidate.key !== item.key,
                        ),
                      }));
                    }}
                  >
                    <Trash2 aria-hidden="true" /> Remove
                  </Button>
                </div>
              </fieldset>
            ))}
          </div>
        )}
      </section>

      <Button type="submit" disabled={confirmation.isPending}>
        {confirmation.isPending ? (
          <LoaderCircle
            aria-hidden="true"
            className="animate-spin motion-reduce:animate-none"
          />
        ) : (
          <CircleCheck aria-hidden="true" />
        )}
        {confirmation.isPending ? 'Confirming…' : 'Confirm receipt'}
      </Button>
    </form>
  );
}

interface FieldProps {
  id?: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  type?: 'text' | 'date' | 'number';
  required?: boolean;
  maxLength?: number;
}

function Field({
  id: providedId,
  label,
  value,
  onChange,
  type = 'text',
  required,
  maxLength,
}: FieldProps) {
  const id = providedId ?? `review-${label.toLowerCase().replaceAll(' ', '-')}`;
  return (
    <div>
      <label className="text-xs font-semibold" htmlFor={id}>
        {label} {required ? <span aria-hidden="true">*</span> : null}
      </label>
      <input
        id={id}
        className={inputClassName}
        type={type}
        value={value}
        required={required}
        maxLength={maxLength}
        min={type === 'number' ? '0' : undefined}
        step={type === 'number' ? '0.01' : undefined}
        onChange={(event) => {
          onChange(event.currentTarget.value);
        }}
      />
    </div>
  );
}

function createInitialValues(
  extraction: ReceiptDocumentExtraction | null,
): ReviewValues {
  return {
    merchantName: extraction?.merchantName ?? '',
    purchaseDate: extraction?.transactionDate?.slice(0, 10) ?? '',
    subtotal: toInput(extraction?.subtotal),
    tax: toInput(extraction?.tax),
    totalAmount: toInput(extraction?.total),
    currency: extraction?.currency ?? '',
    category: extraction?.category ?? '',
    lineItems:
      extraction?.lineItems.map((item) => ({
        key: crypto.randomUUID(),
        description: item.description,
        quantity: item.quantity.toString(),
        unitPrice: item.unitPrice.toString(),
        totalPrice: item.totalPrice.toString(),
        tax: toInput(item.tax),
      })) ?? [],
  };
}

function emptyLineItem(): ReviewLineItem {
  return {
    key: crypto.randomUUID(),
    description: '',
    quantity: '1',
    unitPrice: '',
    totalPrice: '',
    tax: '',
  };
}

function toRequest(
  values: ReviewValues,
  manualEntry: boolean,
): ConfirmReceiptRequest | string {
  if (!values.merchantName.trim()) return 'Enter a merchant name.';
  if (!values.purchaseDate) return 'Enter the purchase date.';
  if (!values.totalAmount || Number(values.totalAmount) < 0)
    return 'Enter a valid non-negative total.';
  if (!/^[A-Za-z]{3}$/.test(values.currency.trim()))
    return 'Enter a three-letter ISO currency code.';
  if (!values.category.trim()) return 'Enter a category.';

  const lineItems: ConfirmReceiptLineItem[] = [];
  for (const item of values.lineItems) {
    if (
      !item.description.trim() ||
      Number(item.quantity) <= 0 ||
      Number(item.unitPrice) < 0
    ) {
      return 'Each line item needs a description, positive quantity and non-negative unit price.';
    }
    lineItems.push({
      description: item.description.trim(),
      quantity: Number(item.quantity),
      unitPrice: Number(item.unitPrice),
      totalPrice: nullableNumber(item.totalPrice),
      tax: nullableNumber(item.tax),
    });
  }

  return {
    merchantName: values.merchantName.trim(),
    purchaseDate: `${values.purchaseDate}T12:00:00.000Z`,
    subtotal: nullableNumber(values.subtotal),
    tax: nullableNumber(values.tax),
    totalAmount: Number(values.totalAmount),
    currency: values.currency.trim().toUpperCase(),
    category: values.category.trim(),
    lineItems,
    ...(manualEntry ? { manualEntry: true } : {}),
  };
}

function nullableNumber(value: string) {
  return value.trim() ? Number(value) : null;
}

function toInput(value: number | null | undefined) {
  return value === null || value === undefined ? '' : value.toString();
}
