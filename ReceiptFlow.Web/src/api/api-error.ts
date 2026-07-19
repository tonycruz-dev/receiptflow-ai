import type { ProblemDetails } from '@/api/contracts';

export class ApiError extends Error {
  public constructor(
    message: string,
    public readonly status: number,
    public readonly problem?: ProblemDetails,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

export function isAbortError(error: unknown) {
  return error instanceof DOMException && error.name === 'AbortError';
}

export function shouldRetryRequest(failureCount: number, error: unknown) {
  if (isAbortError(error)) return false;
  if (error instanceof ApiError && [400, 401, 403].includes(error.status)) {
    return false;
  }
  return failureCount < 2;
}
