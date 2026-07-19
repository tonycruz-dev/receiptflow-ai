import { AtSign, Fingerprint, UserRound } from 'lucide-react';
import { getSafeErrorMessage } from '@/api/error-message';
import { useCurrentUser } from '@/api/use-current-user';
import { ErrorState } from '@/components/shared/error-state';
import { LoadingSkeleton } from '@/components/shared/loading-skeleton';
import { PageHeader } from '@/components/shared/page-header';
import { Card, CardContent, CardHeader } from '@/components/ui/card';

export function Component() {
  const currentUser = useCurrentUser();

  return (
    <div className="space-y-6">
      <PageHeader
        title="Profile"
        description="Your authenticated ReceiptFlow account details."
      />
      {currentUser.isLoading ? (
        <Card className="max-w-2xl">
          <CardContent className="p-6">
            <LoadingSkeleton lines={4} />
          </CardContent>
        </Card>
      ) : currentUser.isError ? (
        <ErrorState
          title="Profile unavailable"
          description={getSafeErrorMessage(currentUser.error)}
          onAction={() => {
            void currentUser.refetch();
          }}
        />
      ) : currentUser.data ? (
        <Card className="max-w-2xl">
          <CardHeader className="flex flex-row items-center gap-4">
            <span className="flex size-12 items-center justify-center rounded-xl bg-accent text-accent-foreground">
              <UserRound aria-hidden="true" />
            </span>
            <div>
              <h2 className="font-semibold">
                {currentUser.data.username ?? 'ReceiptFlow user'}
              </h2>
              <p className="text-sm text-muted-foreground">
                Authenticated account
              </p>
            </div>
          </CardHeader>
          <CardContent>
            <dl className="divide-y rounded-lg border">
              <ProfileField
                icon={UserRound}
                label="Username"
                value={currentUser.data.username ?? 'Not provided'}
              />
              <ProfileField
                icon={AtSign}
                label="Email"
                value={currentUser.data.email ?? 'Not provided'}
              />
              <ProfileField
                icon={Fingerprint}
                label="User ID"
                value={currentUser.data.userId}
              />
            </dl>
          </CardContent>
        </Card>
      ) : null}
    </div>
  );
}

interface ProfileFieldProps {
  icon: typeof UserRound;
  label: string;
  value: string;
}

function ProfileField({ icon: Icon, label, value }: ProfileFieldProps) {
  return (
    <div className="grid gap-1 px-4 py-4 sm:grid-cols-[10rem_1fr] sm:items-center">
      <dt className="flex items-center gap-2 text-sm text-muted-foreground">
        <Icon aria-hidden="true" className="size-4" />
        {label}
      </dt>
      <dd className="break-all text-sm font-medium sm:text-right">{value}</dd>
    </div>
  );
}
