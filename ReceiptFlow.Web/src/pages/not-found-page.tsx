import { FileQuestion } from 'lucide-react';
import { Link } from 'react-router-dom';
import { EmptyState } from '@/components/shared/empty-state';
import { Button } from '@/components/ui/button';

export function Component() {
  return (
    <EmptyState
      icon={FileQuestion}
      title="Page not found"
      description="The page you requested does not exist or may have moved."
      action={
        <Button asChild>
          <Link to="/">Return to dashboard</Link>
        </Button>
      }
    />
  );
}
