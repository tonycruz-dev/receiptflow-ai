import { Check, LoaderCircle, Mic, RotateCcw, ShieldCheck } from 'lucide-react';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { useSpeechRecognition } from '@/hooks/use-speech-recognition';

interface VoiceInputButtonProps {
  disabled?: boolean;
  onTranscript: (transcript: string) => void;
}

export function VoiceInputButton({
  disabled = false,
  onTranscript,
}: VoiceInputButtonProps) {
  const [hasUsedVoiceInput, setHasUsedVoiceInput] = useState(false);
  const { status, interimTranscript, errorMessage, start, finish } =
    useSpeechRecognition({ onFinalTranscript: onTranscript });
  const isRequesting = status === 'requesting';
  const isListening = status === 'listening';
  const isFinalizing = status === 'finalizing';
  const isUnavailable = status === 'unsupported';

  function startVoiceInput() {
    setHasUsedVoiceInput(true);
    start();
  }

  return (
    <div className="flex w-full min-w-0 flex-1 flex-col gap-2 sm:w-auto sm:items-end">
      <div className="flex w-full sm:w-auto">
        <Button
          type="button"
          variant={isListening ? 'destructive' : 'outline'}
          className={
            isListening
              ? 'h-10 w-full min-w-24 gap-2 rounded-md border border-destructive bg-destructive px-4 font-semibold text-destructive-foreground hover:bg-destructive/90 focus-visible:ring-2 focus-visible:ring-destructive focus-visible:ring-offset-2 disabled:border-destructive/40 disabled:bg-destructive/50 disabled:text-destructive-foreground/80 [&_svg]:size-4 sm:w-auto'
              : 'h-10 w-full min-w-24 gap-2 rounded-md border-primary/40 bg-primary/5 px-4 font-semibold text-primary hover:border-primary/60 hover:bg-primary/10 hover:text-primary focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2 disabled:border-border disabled:bg-muted/30 disabled:text-muted-foreground [&_svg]:size-4 sm:w-auto'
          }
          aria-label={isListening ? 'Finish voice input' : 'Start voice input'}
          aria-pressed={isListening}
          disabled={disabled || isUnavailable || isRequesting || isFinalizing}
          onClick={isListening ? finish : startVoiceInput}
        >
          {isRequesting || isFinalizing ? (
            <LoaderCircle
              className="animate-spin motion-reduce:animate-none"
              aria-hidden="true"
            />
          ) : isListening ? (
            <Check aria-hidden="true" />
          ) : (
            <Mic aria-hidden="true" />
          )}
          <span>
            {isRequesting
              ? 'Allowing…'
              : isFinalizing
                ? 'Finishing…'
                : isListening
                  ? 'Stop'
                  : 'Voice'}
          </span>
        </Button>
      </div>

      <div className="max-w-md text-left text-xs leading-5 sm:text-right">
        <div aria-live="polite" aria-atomic="true">
          {isListening ? (
            <p className="inline-flex items-center gap-2 font-semibold text-destructive">
              <span className="relative flex size-2.5" aria-hidden="true">
                <span className="absolute inline-flex size-full animate-ping rounded-full bg-destructive/60 motion-reduce:animate-none" />
                <span className="relative inline-flex size-2.5 rounded-full bg-destructive" />
              </span>
              Listening…
            </p>
          ) : null}
          {isRequesting ? (
            <p className="text-muted-foreground">
              Requesting microphone permission…
            </p>
          ) : null}
          {isListening && interimTranscript ? (
            <p className="text-muted-foreground">
              Listening… {interimTranscript}
            </p>
          ) : null}
          {isFinalizing ? (
            <p className="text-muted-foreground">Processing transcript…</p>
          ) : null}
        </div>

        {isUnavailable ? (
          <p className="text-muted-foreground" role="status">
            Voice input is not supported by this browser.
          </p>
        ) : null}

        {status === 'error' && errorMessage ? (
          <div className="space-y-1" role="alert">
            <p className="text-destructive">{errorMessage}</p>
            <button
              type="button"
              className="inline-flex items-center gap-1 font-semibold text-primary underline-offset-4 hover:underline"
              onClick={startVoiceInput}
            >
              <RotateCcw className="size-3" aria-hidden="true" />
              Try voice input again
            </button>
          </div>
        ) : null}

        {hasUsedVoiceInput ? (
          <p className="mt-1 flex items-start gap-1.5 text-muted-foreground sm:justify-end">
            <ShieldCheck
              className="mt-0.5 size-3 shrink-0"
              aria-hidden="true"
            />
            <span>
              Your browser processes speech recognition and may use an online
              recognition service. Review the transcript before asking.
            </span>
          </p>
        ) : null}
      </div>
    </div>
  );
}
