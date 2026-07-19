import { Component, type ErrorInfo, type PropsWithChildren } from 'react';
import { ErrorState } from '@/components/shared/error-state';

interface State {
  hasError: boolean;
}

export class GlobalErrorBoundary extends Component<PropsWithChildren, State> {
  public state: State = { hasError: false };

  public static getDerivedStateFromError(): State {
    return { hasError: true };
  }

  public componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('ReceiptFlow UI error', error, info.componentStack);
  }

  public render() {
    if (this.state.hasError) {
      return (
        <main className="grid min-h-screen place-items-center p-6">
          <ErrorState
            title="ReceiptFlow could not load"
            description="An unexpected interface error occurred. Refresh the page to try again."
            actionLabel="Refresh page"
            onAction={() => {
              window.location.reload();
            }}
          />
        </main>
      );
    }

    return this.props.children;
  }
}
