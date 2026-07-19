import { ApiError } from '@/api/api-error';

const messagesByStatus: Record<number, string> = {
  400: 'The receipt or file is invalid. Check the details and try again.',
  401: 'Your session has expired. Sign in again to continue.',
  403: 'You do not have permission to upload to this receipt.',
  404: 'The receipt or document is no longer available.',
  413: 'The receipt file exceeds the 10 MB limit.',
  415: 'That file type is not supported. Choose a PDF, JPEG or PNG file.',
  503: 'Document storage or processing is temporarily unavailable. Try again shortly.',
};

export function getUploadErrorMessage(error: unknown) {
  if (error instanceof ApiError) {
    return (
      messagesByStatus[error.status] ??
      error.problem?.detail ??
      error.problem?.title ??
      'The upload could not be completed. Please try again.'
    );
  }

  return 'The service could not be reached. Check your connection and try again.';
}
