import { Plus } from 'lucide-react';
import { Link } from 'react-router-dom';
import { PageHeader } from '@/components/shared/page-header';
import { ReceiptCard } from '@/components/shared/receipt-card';
import { Button } from '@/components/ui/button';
import { recentReceiptFixtures } from '@/data/receipt-fixtures';

export function Component() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Receipts"
        description="Review uploaded documents and track their processing status."
        actions={
          <Button asChild>
            <Link to="/receipts/new">
              <Plus aria-hidden="true" />
              Upload receipt
            </Link>
          </Button>
        }
      />
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {recentReceiptFixtures.map((receipt) => (
          <ReceiptCard key={receipt.id} receipt={receipt} />
        ))}
      </div>
    </div>
  );
}
