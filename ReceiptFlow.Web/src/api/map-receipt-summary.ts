import type { ReceiptSummary } from '@/api/contracts';
import type { ReceiptCardData } from '@/components/shared/receipt-card';
import type { ReceiptStatus } from '@/components/shared/status-badge';

export function mapReceiptSummary(receipt: ReceiptSummary): ReceiptCardData {
  return {
    id: receipt.receiptId,
    merchant: receipt.merchantName,
    date: receipt.purchaseDate,
    total: receipt.totalAmount,
    currency: receipt.currency,
    status: mapProcessingStatus(receipt.processingStatus),
    fileName: receipt.originalFileName ?? 'No document uploaded',
  };
}

function mapProcessingStatus(status: string | null): ReceiptStatus {
  switch (status) {
    case 'Completed':
    case 'Failed':
    case 'Pending':
      return status;
    case 'AwaitingReview':
    case 'Processing':
    case 'Queued':
      return 'Processing';
    default:
      return 'Pending';
  }
}
