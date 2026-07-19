import { useQuery } from '@tanstack/react-query';
import { queryKeys } from '@/api/query-keys';
import { useAuth } from '@/providers/use-auth';

export function useDashboard() {
  const { apiClient } = useAuth();

  return useQuery({
    queryKey: queryKeys.dashboard,
    queryFn: ({ signal }) => apiClient.getDashboard(signal),
    staleTime: 60_000,
  });
}
