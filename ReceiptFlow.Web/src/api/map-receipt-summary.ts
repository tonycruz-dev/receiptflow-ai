import type { ReceiptSummary } from '@/api/contracts';
import type { ReceiptCardData } from '@/components/shared/receipt-card';
import type { ReceiptStatus } from '@/components/shared/status-badge';

export function mapReceiptSummary(receipt: ReceiptSummary): ReceiptCardData {
  return {
    id: receipt.receiptId,
    merchant: receipt.merchantName ?? 'Receipt awaiting extraction',
    date: receipt.purchaseDate,
    total: receipt.totalAmount,
    currency: receipt.currency,
    status: mapLifecycleStatus(receipt.lifecycleStatus),
    fileName: receipt.originalFileName ?? 'No document uploaded',
  };
}

function mapLifecycleStatus(status: string): ReceiptStatus {
  switch (status) {
    case 'Draft':
    case 'Processing':
    case 'Failed':
      return status;
    case 'ReviewRequired':
      return 'Review required';
    case 'Confirmed':
      return 'Confirmed';
    default:
      return 'Draft';
  }
}
