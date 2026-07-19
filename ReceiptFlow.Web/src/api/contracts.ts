export interface CurrentUser {
  userId: string;
  username: string | null;
  email: string | null;
}

export interface DashboardResponse {
  totalReceipts: number;
  spendingByCurrency: CurrencyAmount[];
  documentsProcessing: number;
  recentReceipts: ReceiptSummary[];
}

export interface CurrencyAmount {
  currency: string;
  amount: number;
}

export interface ReceiptListResponse {
  page: number;
  pageSize: number;
  total: number;
  items: ReceiptSummary[];
}

export interface ReceiptSummary {
  receiptId: string;
  merchantName: string | null;
  purchaseDate: string | null;
  totalAmount: number | null;
  currency: string | null;
  category: string | null;
  lifecycleStatus: string;
  documentId: string | null;
  originalFileName: string | null;
  processingStatus: string | null;
}

export interface CreateReceiptRequest {
  merchantName: string;
  purchaseDate: string;
  totalAmount: number;
  currency: string;
  category: string;
}

export interface ReceiptResponse {
  id: string;
  merchantName: string | null;
  purchaseDate: string | null;
  subtotalAmount: number | null;
  taxAmount: number | null;
  totalAmount: number | null;
  currency: string | null;
  category: string | null;
  lifecycleStatus: string;
  createdAtUtc: string;
  lineItems: ReceiptLineItem[];
}

export interface ReceiptLineItem extends ReceiptDocumentLineItem {
  id: string;
}

export interface UploadReceiptDocumentResponse {
  documentId: string;
  receiptId: string;
  originalFileName: string;
  contentType: string;
  fileSize: number;
  processingStatus: string;
}

export interface ImportReceiptResponse {
  receiptId: string;
  documentId: string;
  processingStatus: string;
}

export interface ReceiptDocumentSummary {
  documentId: string;
  originalFileName: string;
  contentType: string;
  fileSize: number;
  uploadedAtUtc: string;
  processingStatus: string;
  hasExtraction: boolean;
}

export interface ReceiptDocumentDetail {
  documentId: string;
  receiptId: string;
  originalFileName: string;
  contentType: string;
  fileSize: number;
  uploadedAtUtc: string;
  processingStatus: string;
  processingError: string | null;
  receiptLifecycleStatus: string;
  confirmationRequired: boolean;
  extraction: ReceiptDocumentExtraction | null;
}

export interface ReceiptDocumentExtraction {
  merchantName: string | null;
  transactionDate: string | null;
  subtotal: number | null;
  tax: number | null;
  total: number | null;
  currency: string | null;
  category: string | null;
  overallConfidence: number | null;
  provider: string;
  modelId: string;
  extractedAtUtc: string;
  lineItems: ReceiptDocumentLineItem[];
}

export interface ConfirmReceiptRequest {
  merchantName: string;
  purchaseDate: string;
  subtotal: number | null;
  tax: number | null;
  totalAmount: number;
  currency: string;
  category: string;
  lineItems: ConfirmReceiptLineItem[];
  manualEntry?: boolean;
}

export interface ConfirmReceiptLineItem {
  description: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number | null;
  tax: number | null;
}

export interface ReceiptDocumentLineItem {
  description: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
  tax: number | null;
  displayOrder: number;
}

export interface ReceiptSearchRequest {
  query: string;
  page: number;
  pageSize: number;
}

export interface ReceiptSearchResponse {
  page: number;
  pageSize: number;
  total: number;
  matches: ReceiptSearchMatch[];
}

export interface ReceiptSearchMatch {
  receiptId: string;
  documentId: string;
  chunkIndex: number;
  merchantName: string | null;
  transactionDate: string | null;
  category: string | null;
  currency: string | null;
  total: number | null;
  content: string;
  relevanceScore: number;
}

export interface AskReceiptQuestionRequest {
  question: string;
}

export interface AskReceiptQuestionResponse {
  answer: string;
  sources: ReceiptAnswerSource[];
}

export interface ReceiptAnswerSource {
  citation: number;
  receiptId: string;
  documentId: string;
  merchantName: string | null;
  transactionDate: string | null;
  total: number | null;
  currency: string | null;
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
}
