import { useQuery } from '@tanstack/react-query';
import { queryKeys } from '@/api/query-keys';
import { useAuth } from '@/providers/use-auth';

export function useCurrentUser() {
  const { apiClient } = useAuth();
  return useQuery({
    queryKey: queryKeys.currentUser,
    queryFn: ({ signal }) => apiClient.getCurrentUser(signal),
    staleTime: 5 * 60_000,
  });
}
