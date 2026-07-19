import { FileText } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';

interface SourceCitationCardProps {
  title: string;
  reference: string;
  excerpt?: string;
}

export function SourceCitationCard({
  title,
  excerpt,
  reference,
}: SourceCitationCardProps) {
  return (
    <Card>
      <CardContent className="flex gap-3 p-4">
        <div className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-muted text-muted-foreground">
          <FileText aria-hidden="true" className="size-4" />
        </div>
        <div className="min-w-0 flex-1">
          <div>
            <p className="font-semibold">{title}</p>
            <p className="mt-0.5 text-xs text-muted-foreground">{reference}</p>
          </div>
          {excerpt ? (
            <p className="mt-2 text-sm text-muted-foreground">{excerpt}</p>
          ) : null}
        </div>
      </CardContent>
    </Card>
  );
}
