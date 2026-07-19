import { ApiError } from '@/api/api-error';
import { reportAuthDiagnostic } from '@/providers/auth-diagnostics';
import type {
  AskReceiptQuestionRequest,
  AskReceiptQuestionResponse,
  CurrentUser,
  DashboardResponse,
  ProblemDetails,
  ReceiptSearchRequest,
  ReceiptSearchResponse,
  ReceiptListResponse,
} from '@/api/contracts';

interface AuthenticatedApiClientOptions {
  baseUrl: string;
  getAccessToken: () => Promise<string>;
  onUnauthorized: () => Promise<void> | void;
  fetchImplementation?: typeof fetch;
}

export interface RequestOptions<TBody = never> {
  method?: 'GET' | 'POST';
  body?: TBody;
  signal?: AbortSignal | undefined;
}

export interface ReceiptFlowApiClient {
  getCurrentUser(signal?: AbortSignal): Promise<CurrentUser>;
  getDashboard(signal?: AbortSignal): Promise<DashboardResponse>;
  listReceipts(
    page: number,
    pageSize: number,
    signal?: AbortSignal,
  ): Promise<ReceiptListResponse>;
  searchReceipts(
    request: ReceiptSearchRequest,
    signal?: AbortSignal,
  ): Promise<ReceiptSearchResponse>;
  askReceiptQuestion(
    request: AskReceiptQuestionRequest,
    signal?: AbortSignal,
  ): Promise<AskReceiptQuestionResponse>;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function parseProblemDetails(value: unknown): ProblemDetails | undefined {
  if (!isRecord(value)) return undefined;

  const problem: ProblemDetails = {};
  if (typeof value.type === 'string') problem.type = value.type;
  if (typeof value.title === 'string') problem.title = value.title;
  if (typeof value.status === 'number') problem.status = value.status;
  if (typeof value.detail === 'string') problem.detail = value.detail;
  if (typeof value.instance === 'string') problem.instance = value.instance;

  if (isRecord(value.errors)) {
    const errors = Object.fromEntries(
      Object.entries(value.errors).flatMap(([key, messages]) =>
        Array.isArray(messages) &&
        messages.every((item) => typeof item === 'string')
          ? [[key, messages]]
          : [],
      ),
    );
    if (Object.keys(errors).length > 0) problem.errors = errors;
  }

  return problem;
}

export function createApiClient({
  baseUrl,
  getAccessToken,
  onUnauthorized,
  fetchImplementation = fetch,
}: AuthenticatedApiClientOptions): ReceiptFlowApiClient {
  async function request<TResponse, TBody = never>(
    path: string,
    options: RequestOptions<TBody> = {},
  ): Promise<TResponse> {
    const token = await getAccessToken();
    const hasBody = options.body !== undefined;
    const requestInit: RequestInit = {
      method: options.method ?? 'GET',
      headers: {
        Accept: 'application/json, application/problem+json',
        Authorization: `Bearer ${token}`,
        ...(hasBody ? { 'Content-Type': 'application/json' } : {}),
      },
      credentials: 'omit',
    };
    if (hasBody) requestInit.body = JSON.stringify(options.body);
    if (options.signal) requestInit.signal = options.signal;

    const response = await fetchImplementation(
      `${baseUrl}${path}`,
      requestInit,
    );

    const contentType =
      response.headers.get('content-type')?.toLowerCase() ?? '';
    const text = response.status === 204 ? '' : await response.text();
    let parsed: unknown;
    if (
      text &&
      (contentType.includes('json') || contentType.includes('+json'))
    ) {
      try {
        parsed = JSON.parse(text) as unknown;
      } catch {
        parsed = undefined;
      }
    }

    if (!response.ok) {
      const problem = contentType.includes('application/problem+json')
        ? parseProblemDetails(parsed)
        : undefined;

      if (response.status === 401) {
        reportAuthDiagnostic('API response', { status: 401 });
        await onUnauthorized();
      } else if (response.status === 403) {
        reportAuthDiagnostic('API response', { status: 403 });
      }

      throw new ApiError(
        problem?.title ??
          `Request failed with status ${response.status.toString()}.`,
        response.status,
        problem,
      );
    }

    if (!text) return undefined as TResponse;
    if (parsed === undefined) {
      throw new ApiError(
        'The server returned an invalid response.',
        response.status,
      );
    }

    return parsed as TResponse;
  }

  return {
    getCurrentUser: (signal) =>
      request<CurrentUser>('/api/auth/me', { signal }),
    getDashboard: (signal) =>
      request<DashboardResponse>('/api/dashboard', { signal }),
    listReceipts: (page, pageSize, signal) =>
      request<ReceiptListResponse>(
        `/api/receipts?page=${page.toString()}&pageSize=${pageSize.toString()}`,
        { signal },
      ),
    searchReceipts: (body, signal) =>
      request<ReceiptSearchResponse, ReceiptSearchRequest>(
        '/api/search/receipts',
        { method: 'POST', body, signal },
      ),
    askReceiptQuestion: (body, signal) =>
      request<AskReceiptQuestionResponse, AskReceiptQuestionRequest>(
        '/api/assistant/receipts/ask',
        { method: 'POST', body, signal },
      ),
  };
}
