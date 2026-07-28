import { ApiError } from '@/api/api-error';
import { reportAuthDiagnostic } from '@/providers/auth-diagnostics';
import type {
  AskReceiptQuestionRequest,
  AskReceiptQuestionResponse,
  ConfirmReceiptRequest,
  ConfirmProductManualRequest,
  CreateProductRequest,
  CurrentUser,
  CreateReceiptRequest,
  DashboardResponse,
  ImportReceiptResponse,
  ProblemDetails,
  ProductManualResponse,
  ProductResponse,
  PurchaseListResponse,
  PurchaseResponse,
  ReceiptSearchRequest,
  ReceiptSearchResponse,
  ReceiptListResponse,
  ReceiptDocumentDetail,
  ReceiptDocumentSummary,
  ReceiptResponse,
  UploadReceiptDocumentResponse,
  UnlinkedReceiptLineItemResponse,
  LinkPurchaseRequest,
  ChangePurchaseManualRequest,
} from '@/api/contracts';

interface AuthenticatedApiClientOptions {
  baseUrl: string;
  getAccessToken: () => Promise<string>;
  onUnauthorized: () => Promise<void> | void;
  fetchImplementation?: typeof fetch;
}

export interface RequestOptions<TBody = never> {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE';
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
  createReceipt(
    request: CreateReceiptRequest,
    signal?: AbortSignal,
  ): Promise<ReceiptResponse>;
  getReceipt(receiptId: string, signal?: AbortSignal): Promise<ReceiptResponse>;
  uploadReceiptDocument(
    receiptId: string,
    file: File,
    signal?: AbortSignal,
  ): Promise<UploadReceiptDocumentResponse>;
  importReceipt(
    file: File,
    signal?: AbortSignal,
  ): Promise<ImportReceiptResponse>;
  confirmReceipt(
    receiptId: string,
    request: ConfirmReceiptRequest,
    signal?: AbortSignal,
  ): Promise<ReceiptResponse>;
  listReceiptDocuments(
    receiptId: string,
    signal?: AbortSignal,
  ): Promise<ReceiptDocumentSummary[]>;
  getReceiptDocument(
    receiptId: string,
    documentId: string,
    signal?: AbortSignal,
  ): Promise<ReceiptDocumentDetail>;
  searchReceipts(
    request: ReceiptSearchRequest,
    signal?: AbortSignal,
  ): Promise<ReceiptSearchResponse>;
  askReceiptQuestion(
    request: AskReceiptQuestionRequest,
    signal?: AbortSignal,
  ): Promise<AskReceiptQuestionResponse>;
  listProducts(signal?: AbortSignal): Promise<ProductResponse[]>;
  createProduct(
    request: CreateProductRequest,
    signal?: AbortSignal,
  ): Promise<ProductResponse>;
  getProduct(productId: string, signal?: AbortSignal): Promise<ProductResponse>;
  listProductManuals(
    productId: string,
    signal?: AbortSignal,
  ): Promise<ProductManualResponse[]>;
  getProductManual(
    productId: string,
    productManualId: string,
    signal?: AbortSignal,
  ): Promise<ProductManualResponse>;
  uploadProductManual(
    productId: string,
    file: File,
    options?: {
      supersedesProductManualId?: string;
      locale?: string;
    },
    signal?: AbortSignal,
  ): Promise<ProductManualResponse>;
  confirmProductManual(
    productId: string,
    productManualId: string,
    request: ConfirmProductManualRequest,
    signal?: AbortSignal,
  ): Promise<ProductManualResponse>;
  listUnlinkedReceiptItems(
    receiptId: string,
    signal?: AbortSignal,
  ): Promise<UnlinkedReceiptLineItemResponse[]>;
  listPurchases(signal?: AbortSignal): Promise<PurchaseListResponse>;
  linkPurchase(
    request: LinkPurchaseRequest,
    signal?: AbortSignal,
  ): Promise<PurchaseResponse>;
  unlinkPurchase(purchaseId: string, signal?: AbortSignal): Promise<void>;
  changePurchaseManual(
    purchaseId: string,
    request: ChangePurchaseManualRequest,
    signal?: AbortSignal,
  ): Promise<PurchaseResponse>;
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
    const body = options.body;
    const hasBody = body !== undefined;
    const isMultipart = body instanceof FormData;
    const requestInit: RequestInit = {
      method: options.method ?? 'GET',
      headers: {
        Accept: 'application/json, application/problem+json',
        Authorization: `Bearer ${token}`,
        ...(hasBody && !isMultipart
          ? { 'Content-Type': 'application/json' }
          : {}),
      },
      credentials: 'omit',
    };
    if (hasBody) {
      if (body instanceof FormData) requestInit.body = body;
      else requestInit.body = JSON.stringify(body);
    }
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
    createReceipt: (body, signal) =>
      request<ReceiptResponse, CreateReceiptRequest>('/api/receipts', {
        method: 'POST',
        body,
        signal,
      }),
    getReceipt: (receiptId, signal) =>
      request<ReceiptResponse>(
        `/api/receipts/${encodeURIComponent(receiptId)}`,
        {
          signal,
        },
      ),
    uploadReceiptDocument: (receiptId, file, signal) => {
      const body = new FormData();
      body.append('file', file);
      return request<UploadReceiptDocumentResponse, FormData>(
        `/api/receipts/${encodeURIComponent(receiptId)}/documents`,
        { method: 'POST', body, signal },
      );
    },
    importReceipt: (file, signal) => {
      const body = new FormData();
      body.append('file', file);
      return request<ImportReceiptResponse, FormData>('/api/receipts/import', {
        method: 'POST',
        body,
        signal,
      });
    },
    confirmReceipt: (receiptId, body, signal) =>
      request<ReceiptResponse, ConfirmReceiptRequest>(
        `/api/receipts/${encodeURIComponent(receiptId)}/confirmation`,
        { method: 'PUT', body, signal },
      ),
    listReceiptDocuments: (receiptId, signal) =>
      request<ReceiptDocumentSummary[]>(
        `/api/receipts/${encodeURIComponent(receiptId)}/documents`,
        { signal },
      ),
    getReceiptDocument: (receiptId, documentId, signal) =>
      request<ReceiptDocumentDetail>(
        `/api/receipts/${encodeURIComponent(receiptId)}/documents/${encodeURIComponent(documentId)}`,
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
    listProducts: (signal) =>
      request<ProductResponse[]>('/api/products', { signal }),
    createProduct: (body, signal) =>
      request<ProductResponse, CreateProductRequest>('/api/products', {
        method: 'POST',
        body,
        signal,
      }),
    getProduct: (productId, signal) =>
      request<ProductResponse>(
        `/api/products/${encodeURIComponent(productId)}`,
        { signal },
      ),
    listProductManuals: (productId, signal) =>
      request<ProductManualResponse[]>(
        `/api/products/${encodeURIComponent(productId)}/manuals`,
        { signal },
      ),
    getProductManual: (productId, productManualId, signal) =>
      request<ProductManualResponse>(
        `/api/products/${encodeURIComponent(productId)}/manuals/${encodeURIComponent(productManualId)}`,
        { signal },
      ),
    uploadProductManual: (productId, file, options, signal) => {
      const body = new FormData();
      body.append('file', file);
      if (options?.supersedesProductManualId) {
        body.append(
          'supersedesProductManualId',
          options.supersedesProductManualId,
        );
      }
      if (options?.locale) body.append('locale', options.locale);
      return request<ProductManualResponse, FormData>(
        `/api/products/${encodeURIComponent(productId)}/manuals`,
        { method: 'POST', body, signal },
      );
    },
    confirmProductManual: (productId, productManualId, body, signal) =>
      request<ProductManualResponse, ConfirmProductManualRequest>(
        `/api/products/${encodeURIComponent(productId)}/manuals/${encodeURIComponent(productManualId)}/confirmation`,
        { method: 'PUT', body, signal },
      ),
    listUnlinkedReceiptItems: (receiptId, signal) =>
      request<UnlinkedReceiptLineItemResponse[]>(
        `/api/receipts/${encodeURIComponent(receiptId)}/unlinked-items`,
        { signal },
      ),
    listPurchases: (signal) =>
      request<PurchaseListResponse>('/api/purchases', { signal }),
    linkPurchase: (body, signal) =>
      request<PurchaseResponse, LinkPurchaseRequest>('/api/purchases', {
        method: 'POST',
        body,
        signal,
      }),
    unlinkPurchase: (purchaseId, signal) =>
      request<undefined>(`/api/purchases/${encodeURIComponent(purchaseId)}`, {
        method: 'DELETE',
        signal,
      }),
    changePurchaseManual: (purchaseId, body, signal) =>
      request<PurchaseResponse, ChangePurchaseManualRequest>(
        `/api/purchases/${encodeURIComponent(purchaseId)}/manual`,
        { method: 'PUT', body, signal },
      ),
  };
}
