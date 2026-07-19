import { UserRound } from 'lucide-react';
import { EmptyState } from '@/components/shared/empty-state';
import { PageHeader } from '@/components/shared/page-header';

export function Component() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Profile"
        description="Account details and preferences will live here after authentication is connected."
      />
      <EmptyState
        icon={UserRound}
        title="Profile not connected"
        description="The authentication-provider boundary is ready for Keycloak, but this frontend task performs no login or identity calls."
      />
    </div>
  );
}
