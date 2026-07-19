import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { queryKeys } from '@/api/query-keys';
import { useAuth } from '@/providers/use-auth';

export function useReceipts(page: number, pageSize: number) {
  const { apiClient } = useAuth();

  return useQuery({
    queryKey: queryKeys.receipts(page, pageSize),
    queryFn: ({ signal }) => apiClient.listReceipts(page, pageSize, signal),
    placeholderData: keepPreviousData,
    staleTime: 60_000,
  });
}
