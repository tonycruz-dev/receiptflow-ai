import { FileUp, Upload } from 'lucide-react';
import { PageHeader } from '@/components/shared/page-header';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';

export function Component() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Upload receipt"
        description="Add a receipt or document for extraction and review."
      />
      <Card className="max-w-3xl">
        <CardContent className="p-6 sm:p-8">
          <div className="rounded-xl border border-dashed p-8 text-center sm:p-12">
            <div className="mx-auto flex size-12 items-center justify-center rounded-xl bg-accent text-accent-foreground">
              <FileUp aria-hidden="true" />
            </div>
            <h2 className="mt-4 font-semibold">Choose a receipt document</h2>
            <p className="mx-auto mt-1 max-w-md text-sm text-muted-foreground">
              Upload handling will be connected to the receipt API in a later
              task. This shell does not send files anywhere.
            </p>
            <Button className="mt-5" disabled>
              <Upload aria-hidden="true" />
              Select file
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
