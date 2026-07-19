import { ApiError } from '@/api/api-error';

export function getSafeErrorMessage(
  error: unknown,
  fallback = 'The request could not be completed. Please try again.',
) {
  if (!(error instanceof ApiError)) return fallback;
  return error.problem?.detail ?? error.problem?.title ?? fallback;
}
