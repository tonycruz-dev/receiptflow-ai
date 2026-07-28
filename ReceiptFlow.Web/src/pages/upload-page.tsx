import { FileText, PackageOpen, ReceiptText } from 'lucide-react';
import { Link } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';

export function Component() {
  return (
    <div className="space-y-8">
      <header className="rounded-3xl border border-primary/15 bg-gradient-to-br from-primary/[0.12] via-card to-accent/40 px-6 py-9 shadow-sm sm:px-8">
        <div className="flex items-start gap-4">
          <div className="grid size-14 place-items-center rounded-2xl bg-primary text-primary-foreground">
            <FileText aria-hidden="true" />
          </div>
          <div>
            <h1 className="text-3xl font-bold tracking-tight">Upload</h1>
            <p className="mt-2 text-muted-foreground">
              Choose the document workflow you want to start.
            </p>
          </div>
        </div>
      </header>

      <div className="grid gap-6 md:grid-cols-2">
        <UploadChoice
          href="/receipts/new"
          icon={ReceiptText}
          title="Receipt"
          description="Extract merchant, purchase date, totals and line items."
        />
        <UploadChoice
          href="/products/manuals/new"
          icon={PackageOpen}
          title="Product manual"
          description="Attach a PDF manual to a product, extract its details and review them."
        />
      </div>
    </div>
  );
}

function UploadChoice({
  href,
  icon: Icon,
  title,
  description,
}: {
  href: string;
  icon: typeof ReceiptText;
  title: string;
  description: string;
}) {
  return (
    <Link to={href} className="group rounded-3xl">
      <Card className="h-full rounded-3xl border-border/70 transition group-hover:border-primary/40 group-hover:shadow-md">
        <CardContent className="flex h-full items-start gap-5 p-7">
          <div className="grid size-12 shrink-0 place-items-center rounded-2xl bg-primary/10 text-primary">
            <Icon aria-hidden="true" />
          </div>
          <div>
            <h2 className="text-xl font-semibold">{title}</h2>
            <p className="mt-2 leading-6 text-muted-foreground">
              {description}
            </p>
          </div>
        </CardContent>
      </Card>
    </Link>
  );
}
