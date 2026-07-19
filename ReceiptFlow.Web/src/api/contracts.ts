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
  merchantName: string;
  purchaseDate: string;
  totalAmount: number;
  currency: string;
  category: string;
  documentId: string | null;
  originalFileName: string | null;
  processingStatus: string | null;
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
