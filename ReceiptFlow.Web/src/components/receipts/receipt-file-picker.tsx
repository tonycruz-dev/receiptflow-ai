import { FileText, RefreshCw, Upload, X } from 'lucide-react';
import { useEffect, useRef, useState, type DragEvent } from 'react';
import { Button } from '@/components/ui/button';

export const maximumReceiptFileSize = 10 * 1024 * 1024;
export const acceptedReceiptFileTypes = [
  'application/pdf',
  'image/jpeg',
  'image/png',
] as const;

const acceptedExtensions: Record<string, readonly string[]> = {
  'application/pdf': ['.pdf'],
  'image/jpeg': ['.jpg', '.jpeg'],
  'image/png': ['.png'],
};

export function validateReceiptFile(file: File) {
  if (file.size <= 0) return 'Choose a non-empty receipt file.';
  if (file.size > maximumReceiptFileSize) {
    return 'The receipt file must be 10 MB or smaller.';
  }

  const extensions = acceptedExtensions[file.type];
  const lowerName = file.name.toLowerCase();
  if (!extensions?.some((extension) => lowerName.endsWith(extension))) {
    return 'Choose a PDF, JPEG or PNG file with a matching file extension.';
  }

  return null;
}

interface ReceiptFilePickerProps {
  file: File | null;
  error?: string | undefined;
  disabled?: boolean | undefined;
  onChange: (file: File | null) => void;
}

export function ReceiptFilePicker({
  file,
  error,
  disabled = false,
  onChange,
}: ReceiptFilePickerProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  function selectFirst(files: FileList | null) {
    onChange(files?.[0] ?? null);
  }

  function handleDrop(event: DragEvent<HTMLButtonElement>) {
    event.preventDefault();
    if (!disabled) selectFirst(event.dataTransfer.files);
  }

  return (
    <div className="space-y-3">
      <label className="text-sm font-semibold" htmlFor="receipt-file">
        Receipt file
      </label>
      <input
        ref={inputRef}
        id="receipt-file"
        className="sr-only"
        type="file"
        accept=".pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png"
        disabled={disabled}
        aria-describedby="receipt-file-help receipt-file-error"
        onChange={(event) => {
          selectFirst(event.currentTarget.files);
        }}
      />
      {file ? (
        <div className="flex flex-col gap-4 rounded-xl border bg-muted/30 p-4 sm:flex-row sm:items-center">
          {file.type.startsWith('image/') ? (
            <ImagePreview
              key={`${file.name}-${file.lastModified.toString()}`}
              file={file}
            />
          ) : (
            <div className="grid size-16 place-items-center rounded-lg bg-accent text-accent-foreground">
              <FileText aria-hidden="true" className="size-7" />
            </div>
          )}
          <div className="min-w-0 flex-1">
            <p className="truncate text-sm font-semibold">{file.name}</p>
            <p className="mt-1 text-xs text-muted-foreground">
              {formatFileSize(file.size)} · {file.type || 'Unknown type'}
            </p>
          </div>
          <div className="flex gap-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={disabled}
              onClick={() => {
                inputRef.current?.click();
              }}
            >
              <RefreshCw aria-hidden="true" />
              Change
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              disabled={disabled}
              onClick={() => {
                onChange(null);
              }}
            >
              <X aria-hidden="true" />
              Remove
            </Button>
          </div>
        </div>
      ) : (
        <button
          type="button"
          className="flex w-full flex-col items-center rounded-xl border border-dashed p-8 text-center transition-colors hover:bg-muted/40 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring disabled:opacity-50"
          disabled={disabled}
          onClick={() => {
            inputRef.current?.click();
          }}
          onDragOver={(event) => {
            event.preventDefault();
          }}
          onDrop={handleDrop}
        >
          <span className="grid size-12 place-items-center rounded-xl bg-accent text-accent-foreground">
            <Upload aria-hidden="true" />
          </span>
          <span className="mt-3 text-sm font-semibold">
            Choose a file or drag it here
          </span>
          <span
            id="receipt-file-help"
            className="mt-1 text-xs text-muted-foreground"
          >
            PDF, JPEG or PNG, up to 10 MB
          </span>
        </button>
      )}
      {error ? (
        <p id="receipt-file-error" className="text-sm text-destructive">
          {error}
        </p>
      ) : null}
    </div>
  );
}

export function formatFileSize(bytes: number) {
  if (bytes < 1024) return `${bytes.toString()} bytes`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function ImagePreview({ file }: { file: File }) {
  const [previewUrl] = useState(() => URL.createObjectURL(file));
  useEffect(
    () => () => {
      URL.revokeObjectURL(previewUrl);
    },
    [previewUrl],
  );
  return (
    <img
      src={previewUrl}
      alt="Selected receipt preview"
      className="size-24 rounded-lg border bg-background object-cover"
    />
  );
}
