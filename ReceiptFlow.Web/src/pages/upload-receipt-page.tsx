import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  ArrowLeft,
  CheckCircle2,
  CircleAlert,
  FileCheck2,
  LoaderCircle,
  LockKeyhole,
  ScanLine,
  Sparkles,
  Upload,
  UploadCloud,
} from 'lucide-react';
import { useState, type SyntheticEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { queryKeys } from '@/api/query-keys';
import { getUploadErrorMessage } from '@/api/upload-error-message';
import {
  ReceiptFilePicker,
  validateReceiptFile,
} from '@/components/receipts/receipt-file-picker';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { useAuth } from '@/providers/use-auth';

export function Component() {
  const { apiClient } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [file, setFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState<string>();
  const [requestError, setRequestError] = useState<string>();

  const importReceipt = useMutation({
    mutationFn: (selectedFile: File) => apiClient.importReceipt(selectedFile),
  });

  function handleFileChange(selectedFile: File | null) {
    setFile(selectedFile);

    setFileError(
      selectedFile
        ? (validateReceiptFile(selectedFile) ?? undefined)
        : undefined,
    );

    setRequestError(undefined);
  }

  async function handleSubmit(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault();

    const validationError = file
      ? validateReceiptFile(file)
      : 'Choose a receipt file.';

    setFileError(validationError ?? undefined);
    setRequestError(undefined);

    if (!file || validationError) return;

    try {
      const intake = await importReceipt.mutateAsync(file);

      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: queryKeys.dashboard,
        }),
        queryClient.invalidateQueries({
          queryKey: queryKeys.receiptLists,
        }),
      ]);

      await navigate(`/receipts/${encodeURIComponent(intake.receiptId)}`, {
        state: {
          documentId: intake.documentId,
        },
      });
    } catch (error) {
      setRequestError(getUploadErrorMessage(error));
    }
  }

  return (
    <div className="space-y-8">
      <UploadPageHeader />

      <div className="grid items-start gap-6 xl:grid-cols-[minmax(0,1fr)_22rem]">
        <Card className="overflow-hidden rounded-3xl border-border/70 shadow-sm">
          {importReceipt.isPending ? (
            <div
              className="h-1 overflow-hidden bg-primary/10"
              role="status"
              aria-label="Uploading receipt"
            >
              <div className="h-full w-1/3 animate-pulse rounded-full bg-primary" />
            </div>
          ) : null}

          <CardHeader className="border-b bg-muted/20 p-6 sm:p-8">
            <div className="flex items-start gap-4">
              <div className="flex size-12 shrink-0 items-center justify-center rounded-2xl bg-primary/10 text-primary">
                <UploadCloud className="size-6" aria-hidden="true" />
              </div>

              <div>
                <h2 className="text-xl font-semibold tracking-tight">
                  Choose your receipt
                </h2>

                <p className="mt-1 max-w-2xl text-sm leading-6 text-muted-foreground">
                  Select a receipt image or PDF. ReceiptFlow will automatically
                  extract the merchant, purchase date, amounts and line items.
                </p>
              </div>
            </div>
          </CardHeader>

          <CardContent className="p-6 sm:p-8">
            <form
              className="space-y-6"
              noValidate
              onSubmit={(event) => {
                void handleSubmit(event);
              }}
            >
              {requestError ? <UploadError message={requestError} /> : null}

              <ReceiptFilePicker
                file={file}
                error={fileError}
                disabled={importReceipt.isPending}
                onChange={handleFileChange}
              />

              {file && !fileError ? (
                <div
                  className="flex items-start gap-3 rounded-2xl border border-emerald-500/20 bg-emerald-500/8 p-4"
                  role="status"
                >
                  <CheckCircle2
                    className="mt-0.5 size-5 shrink-0 text-emerald-600 dark:text-emerald-400"
                    aria-hidden="true"
                  />

                  <div>
                    <p className="text-sm font-semibold">Ready to upload</p>
                    <p className="mt-1 text-sm text-muted-foreground">
                      Your file passed the initial validation checks.
                    </p>
                  </div>
                </div>
              ) : null}

              <div className="flex flex-col gap-4 border-t pt-6 sm:flex-row sm:items-center sm:justify-between">
                <div className="flex items-center gap-2 text-xs text-muted-foreground">
                  <LockKeyhole
                    className="size-4 shrink-0 text-primary"
                    aria-hidden="true"
                  />
                  Your receipt is uploaded securely to your account.
                </div>

                <Button
                  type="submit"
                  size="lg"
                  disabled={importReceipt.isPending}
                  className="min-w-44 shadow-sm"
                >
                  {importReceipt.isPending ? (
                    <LoaderCircle
                      aria-hidden="true"
                      className="animate-spin motion-reduce:animate-none"
                    />
                  ) : (
                    <Upload aria-hidden="true" />
                  )}

                  {importReceipt.isPending
                    ? 'Uploading receipt…'
                    : 'Upload receipt'}
                </Button>
              </div>

              <p
                className="text-right text-xs text-muted-foreground"
                aria-live="polite"
              >
                {importReceipt.isPending
                  ? 'Please keep this page open while your document is uploaded.'
                  : file
                    ? 'Extraction will begin immediately after upload.'
                    : 'Choose a document to continue.'}
              </p>
            </form>
          </CardContent>
        </Card>

        <UploadProcessCard />
      </div>
    </div>
  );
}

function UploadPageHeader() {
  return (
    <header className="relative isolate overflow-hidden rounded-3xl border border-primary/15 bg-gradient-to-br from-primary/[0.10] via-card to-accent/30 px-6 py-8 shadow-sm sm:px-8">
      <div
        aria-hidden="true"
        className="absolute -right-20 -top-24 size-64 rounded-full bg-primary/10 blur-3xl"
      />

      <div
        aria-hidden="true"
        className="absolute -bottom-24 left-1/3 size-56 rounded-full bg-emerald-300/10 blur-3xl"
      />

      <div className="relative">
        <Button
          asChild
          variant="ghost"
          size="sm"
          className="-ml-3 mb-5 text-muted-foreground hover:text-foreground"
        >
          <Link to="/receipts">
            <ArrowLeft aria-hidden="true" />
            Back to receipts
          </Link>
        </Button>

        <div className="flex flex-col justify-between gap-6 sm:flex-row sm:items-center">
          <div className="flex items-start gap-4">
            <div className="flex size-14 shrink-0 items-center justify-center rounded-2xl bg-primary text-primary-foreground shadow-md shadow-primary/20">
              <UploadCloud className="size-7" aria-hidden="true" />
            </div>

            <div>
              <div className="mb-2 flex w-fit items-center gap-2 rounded-full border border-primary/15 bg-background/70 px-3 py-1 text-xs font-medium text-primary backdrop-blur">
                <Sparkles className="size-3.5" aria-hidden="true" />
                AI-powered receipt extraction
              </div>

              <h1 className="text-3xl font-bold tracking-tight sm:text-4xl">
                Upload a receipt
              </h1>

              <p className="mt-2 max-w-2xl text-sm leading-6 text-muted-foreground sm:text-base">
                Upload the document first. ReceiptFlow will read it and present
                the extracted information for you to review and confirm.
              </p>
            </div>
          </div>
        </div>
      </div>
    </header>
  );
}

function UploadProcessCard() {
  const steps = [
    {
      icon: UploadCloud,
      title: 'Upload',
      description: 'Choose a PDF, JPG or PNG receipt.',
    },
    {
      icon: ScanLine,
      title: 'Extract',
      description: 'AI reads the merchant, totals and line items.',
    },
    {
      icon: FileCheck2,
      title: 'Review and confirm',
      description: 'Check the information before saving it.',
    },
  ];

  return (
    <Card className="overflow-hidden rounded-3xl border-border/70 shadow-sm">
      <CardHeader className="border-b bg-muted/20 p-6">
        <h2 className="text-lg font-semibold tracking-tight">
          What happens next?
        </h2>

        <p className="mt-1 text-sm leading-6 text-muted-foreground">
          You do not need to enter the receipt details manually.
        </p>
      </CardHeader>

      <CardContent className="p-6">
        <ol className="space-y-6">
          {steps.map((step, index) => {
            const Icon = step.icon;

            return (
              <li key={step.title} className="relative flex gap-4">
                {index < steps.length - 1 ? (
                  <div
                    aria-hidden="true"
                    className="absolute left-5 top-11 h-[calc(100%+0.5rem)] w-px bg-border"
                  />
                ) : null}

                <div className="relative flex size-10 shrink-0 items-center justify-center rounded-xl border bg-background text-primary shadow-sm">
                  <Icon className="size-5" aria-hidden="true" />
                </div>

                <div>
                  <p className="text-sm font-semibold">
                    <span className="mr-1 text-muted-foreground">
                      {index + 1}.
                    </span>
                    {step.title}
                  </p>

                  <p className="mt-1 text-sm leading-5 text-muted-foreground">
                    {step.description}
                  </p>
                </div>
              </li>
            );
          })}
        </ol>

        <div className="mt-7 rounded-2xl border border-primary/15 bg-primary/5 p-4">
          <p className="text-sm font-medium">Supported documents</p>
          <p className="mt-1 text-xs leading-5 text-muted-foreground">
            PDF, JPG and PNG files up to the limit configured by the ReceiptFlow
            API.
          </p>
        </div>
      </CardContent>
    </Card>
  );
}

function UploadError({ message }: { message: string }) {
  return (
    <div
      className="flex gap-3 rounded-2xl border border-destructive/30 bg-destructive/10 p-4"
      role="alert"
    >
      <CircleAlert
        aria-hidden="true"
        className="mt-0.5 size-5 shrink-0 text-destructive"
      />

      <div>
        <p className="text-sm font-semibold">Receipt upload failed</p>
        <p className="mt-1 text-sm leading-6 text-muted-foreground">
          {message}
        </p>
      </div>
    </div>
  );
}
