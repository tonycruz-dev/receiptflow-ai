export interface CurrentUser {
  userId: string;
  username: string | null;
  email: string | null;
}

export interface ProductResponse {
  productId: string;
  manufacturer: string;
  name: string;
  modelNumber: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface CreateProductRequest {
  manufacturer: string;
  name: string;
  modelNumber: string | null;
}

export interface ProductManualResponse {
  productManualId: string;
  productId: string;
  manufacturer: string;
  productName: string;
  modelNumber: string | null;
  documentId: string;
  originalFileName: string;
  contentType: string;
  fileSize: number;
  documentProcessingStatus: string;
  manualLifecycleStatus: string;
  manualKind: string;
  locale: string;
  versionLabel: string | null;
  warrantyDurationMonths: number | null;
  supersedesProductManualId: string | null;
  uploadedAtUtc: string;
  confirmedAtUtc: string | null;
  supersededAtUtc: string | null;
  extraction: ManualExtractionResponse | null;
  sections: ManualSectionResponse[];
}

export interface ManualExtractionResponse {
  suggestedManufacturer: string | null;
  suggestedProductName: string | null;
  suggestedModelNumber: string | null;
  suggestedVersionLabel: string | null;
  suggestedWarrantyDurationMonths: number | null;
  overallConfidence: number | null;
  extractedAtUtc: string;
}

export interface ManualSectionResponse {
  ordinal: number;
  headingPath: string;
  pageStart: number | null;
  pageEnd: number | null;
  content: string;
}

export interface ConfirmProductManualRequest {
  manufacturer: string;
  productName: string;
  modelNumber: string | null;
  versionLabel: string;
  locale: string;
  warrantyDurationMonths: number | null;
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
  documentType?: 'Receipt' | 'ProductManual' | 'All';
}

export interface ReceiptSearchResponse {
  page: number;
  pageSize: number;
  total: number;
  matches: ReceiptSearchMatch[];
}

export interface ReceiptSearchMatch {
  documentType: 'Receipt' | 'ProductManual';
  receiptId: string;
  productId: string | null;
  productManualId: string | null;
  documentId: string;
  chunkIndex: number;
  merchantName: string | null;
  transactionDate: string | null;
  category: string | null;
  currency: string | null;
  total: number | null;
  productManufacturer: string | null;
  productName: string | null;
  modelNumber: string | null;
  manualVersion: string | null;
  locale: string | null;
  warrantyDurationMonths: number | null;
  sectionHeading: string | null;
  isActiveManual: boolean;
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
  sourceType: 'Receipt' | 'ProductManual';
  receiptId: string;
  productId: string | null;
  productManualId: string | null;
  documentId: string;
  merchantName: string | null;
  transactionDate: string | null;
  total: number | null;
  currency: string | null;
  productManufacturer: string | null;
  productName: string | null;
  modelNumber: string | null;
  manualVersion: string | null;
  locale: string | null;
  warrantyDurationMonths: number | null;
  sectionHeading: string | null;
  isActiveManual: boolean;
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
}
