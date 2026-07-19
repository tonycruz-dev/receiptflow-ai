import { useMutation, useQueryClient } from '@tanstack/react-query';
import { CircleAlert, LoaderCircle, Upload } from 'lucide-react';
import { useState, type SyntheticEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { queryKeys } from '@/api/query-keys';
import { getUploadErrorMessage } from '@/api/upload-error-message';
import {
  ReceiptFilePicker,
  validateReceiptFile,
} from '@/components/receipts/receipt-file-picker';
import { PageHeader } from '@/components/shared/page-header';
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
        queryClient.invalidateQueries({ queryKey: queryKeys.dashboard }),
        queryClient.invalidateQueries({ queryKey: queryKeys.receiptLists }),
      ]);
      await navigate(`/receipts/${encodeURIComponent(intake.receiptId)}`, {
        state: { documentId: intake.documentId },
      });
    } catch (error) {
      setRequestError(getUploadErrorMessage(error));
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Upload receipt"
        description="Upload a document first. ReceiptFlow will extract the details for you to review."
      />
      <Card className="max-w-3xl">
        <CardHeader>
          <h2 className="text-lg font-semibold">Choose your receipt</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            You will confirm the merchant, date and amounts after extraction
            completes.
          </p>
        </CardHeader>
        <CardContent>
          <form
            className="space-y-6"
            noValidate
            onSubmit={(event) => {
              void handleSubmit(event);
            }}
          >
            {requestError ? (
              <div
                className="flex gap-3 rounded-lg border border-destructive/30 bg-destructive/8 p-4"
                role="alert"
              >
                <CircleAlert
                  aria-hidden="true"
                  className="mt-0.5 size-5 text-destructive"
                />
                <div>
                  <p className="text-sm font-semibold">Receipt upload failed</p>
                  <p className="mt-1 text-sm text-muted-foreground">
                    {requestError}
                  </p>
                </div>
              </div>
            ) : null}
            <ReceiptFilePicker
              file={file}
              error={fileError}
              disabled={importReceipt.isPending}
              onChange={handleFileChange}
            />
            <div className="flex flex-wrap items-center gap-3 border-t pt-5">
              <Button type="submit" disabled={importReceipt.isPending}>
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
              <p className="text-xs text-muted-foreground" aria-live="polite">
                {importReceipt.isPending
                  ? 'Securely uploading your document.'
                  : 'Extraction starts automatically after upload.'}
              </p>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
