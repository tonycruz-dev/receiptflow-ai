import { useQuery } from '@tanstack/react-query';
import { queryKeys } from '@/api/query-keys';
import { useAuth } from '@/providers/use-auth';

const pollingIntervalMilliseconds = 2_500;

export function useReceipt(receiptId: string) {
  const { apiClient } = useAuth();
  return useQuery({
    queryKey: queryKeys.receipt(receiptId),
    queryFn: ({ signal }) => apiClient.getReceipt(receiptId, signal),
    enabled: receiptId.length > 0,
  });
}

export function useReceiptDocuments(receiptId: string) {
  const { apiClient } = useAuth();
  return useQuery({
    queryKey: queryKeys.receiptDocuments(receiptId),
    queryFn: ({ signal }) => apiClient.listReceiptDocuments(receiptId, signal),
    enabled: receiptId.length > 0,
  });
}

export function useReceiptDocument(receiptId: string, documentId?: string) {
  const { apiClient } = useAuth();
  return useQuery({
    queryKey: queryKeys.receiptDocument(receiptId, documentId ?? ''),
    queryFn: ({ signal }) =>
      apiClient.getReceiptDocument(receiptId, documentId ?? '', signal),
    enabled: receiptId.length > 0 && Boolean(documentId),
    refetchInterval: (query) => {
      const status = query.state.data?.processingStatus;
      return status === 'Completed' || status === 'Failed'
        ? false
        : pollingIntervalMilliseconds;
    },
    refetchIntervalInBackground: false,
  });
}
