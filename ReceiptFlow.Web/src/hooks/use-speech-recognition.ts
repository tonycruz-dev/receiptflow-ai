import { useCallback, useEffect, useRef, useState } from 'react';

interface BrowserSpeechRecognitionAlternative {
  transcript: string;
}

interface BrowserSpeechRecognitionResult {
  isFinal: boolean;
  length: number;
  [index: number]: BrowserSpeechRecognitionAlternative;
}

interface BrowserSpeechRecognitionResultList {
  length: number;
  [index: number]: BrowserSpeechRecognitionResult;
}

interface BrowserSpeechRecognitionEvent extends Event {
  resultIndex: number;
  results: BrowserSpeechRecognitionResultList;
}

interface BrowserSpeechRecognitionErrorEvent extends Event {
  error: string;
}

interface BrowserSpeechRecognition {
  continuous: boolean;
  interimResults: boolean;
  lang: string;
  onstart: ((event: Event) => void) | null;
  onresult: ((event: BrowserSpeechRecognitionEvent) => void) | null;
  onerror: ((event: BrowserSpeechRecognitionErrorEvent) => void) | null;
  onend: ((event: Event) => void) | null;
  start: () => void;
  stop: () => void;
  abort: () => void;
}

type BrowserSpeechRecognitionConstructor = new () => BrowserSpeechRecognition;

type SpeechWindow = Window &
  typeof globalThis & {
    SpeechRecognition?: BrowserSpeechRecognitionConstructor;
    webkitSpeechRecognition?: BrowserSpeechRecognitionConstructor;
  };

export type SpeechRecognitionStatus =
  'unsupported' | 'idle' | 'requesting' | 'listening' | 'finalizing' | 'error';

interface UseSpeechRecognitionOptions {
  onFinalTranscript: (transcript: string) => void;
}

const maximumConsecutiveRestarts = 3;

export function useSpeechRecognition({
  onFinalTranscript,
}: UseSpeechRecognitionOptions) {
  const [recognitionConstructor] = useState<
    BrowserSpeechRecognitionConstructor | undefined
  >(() => getSpeechRecognitionConstructor());
  const recognitionRef = useRef<BrowserSpeechRecognition | null>(null);
  const isSessionActiveRef = useRef(false);
  const explicitStopRef = useRef(false);
  const isMountedRef = useRef(true);
  const finalSegmentsRef = useRef<string[]>([]);
  const consecutiveRestartsRef = useRef(0);
  const onFinalTranscriptRef = useRef(onFinalTranscript);
  const [status, setStatus] = useState<SpeechRecognitionStatus>(
    recognitionConstructor ? 'idle' : 'unsupported',
  );
  const [interimTranscript, setInterimTranscript] = useState('');
  const [errorMessage, setErrorMessage] = useState<string>();

  useEffect(() => {
    onFinalTranscriptRef.current = onFinalTranscript;
  }, [onFinalTranscript]);

  const releaseRecognition = useCallback(
    (recognition: BrowserSpeechRecognition, abort: boolean) => {
      if (recognitionRef.current === recognition) {
        recognitionRef.current = null;
      }
      recognition.onstart = null;
      recognition.onresult = null;
      recognition.onerror = null;
      recognition.onend = null;
      if (abort) {
        try {
          recognition.abort();
        } catch {
          // The browser may already have ended and released this instance.
        }
      }
    },
    [],
  );

  const endWithError = useCallback((message: string) => {
    isSessionActiveRef.current = false;
    explicitStopRef.current = true;
    finalSegmentsRef.current = [];
    setInterimTranscript('');
    setStatus('error');
    setErrorMessage(message);
  }, []);

  const commitAndEndSession = useCallback(() => {
    if (!isSessionActiveRef.current) return;
    isSessionActiveRef.current = false;
    const transcript = finalSegmentsRef.current.join(' ').trim();
    finalSegmentsRef.current = [];
    setInterimTranscript('');
    setStatus('idle');
    if (transcript) onFinalTranscriptRef.current(transcript);
  }, []);

  const beginRecognition = useCallback(
    function beginRecognition(isRestart: boolean) {
      if (
        !recognitionConstructor ||
        !isMountedRef.current ||
        !isSessionActiveRef.current ||
        explicitStopRef.current ||
        recognitionRef.current
      ) {
        return;
      }

      let recognition: BrowserSpeechRecognition;
      try {
        recognition = new recognitionConstructor();
      } catch {
        endWithError('Voice recognition is unavailable. Please try again.');
        return;
      }

      const finalizedResultIndexes = new Set<number>();
      let isFirstFinalAfterRestart = isRestart;
      recognitionRef.current = recognition;
      recognition.continuous = true;
      recognition.interimResults = true;
      recognition.lang = navigator.language || 'en-GB';
      if (!isRestart) setStatus('requesting');

      recognition.onstart = () => {
        if (!isMountedRef.current || recognitionRef.current !== recognition)
          return;
        setStatus('listening');
      };
      recognition.onresult = (event) => {
        if (!isMountedRef.current || recognitionRef.current !== recognition)
          return;
        consecutiveRestartsRef.current = 0;
        let interim = '';
        for (
          let index = event.resultIndex;
          index < event.results.length;
          index += 1
        ) {
          const result = event.results[index];
          const transcript = result?.[0]?.transcript.trim() ?? '';
          if (result?.isFinal) {
            if (transcript && !finalizedResultIndexes.has(index)) {
              finalizedResultIndexes.add(index);
              if (
                isFirstFinalAfterRestart &&
                finalSegmentsRef.current.at(-1) === transcript
              ) {
                isFirstFinalAfterRestart = false;
                continue;
              }
              isFirstFinalAfterRestart = false;
              finalSegmentsRef.current.push(transcript);
            }
          } else {
            interim += `${transcript} `;
          }
        }
        setInterimTranscript(interim.trim());
      };
      recognition.onerror = (event) => {
        if (!isMountedRef.current || recognitionRef.current !== recognition)
          return;
        releaseRecognition(recognition, true);
        endWithError(toSafeSpeechError(event.error));
      };
      recognition.onend = () => {
        if (!isMountedRef.current || recognitionRef.current !== recognition)
          return;
        releaseRecognition(recognition, false);
        setInterimTranscript('');

        if (explicitStopRef.current) {
          commitAndEndSession();
          return;
        }

        consecutiveRestartsRef.current += 1;
        if (consecutiveRestartsRef.current > maximumConsecutiveRestarts) {
          endWithError(
            'Voice recognition ended unexpectedly. Please try again.',
          );
          return;
        }
        beginRecognition(true);
      };

      try {
        recognition.start();
      } catch (error) {
        releaseRecognition(recognition, true);
        endWithError(
          isInvalidStateError(error)
            ? 'Voice input is already running.'
            : 'Voice input could not start. Please try again.',
        );
      }
    },
    [
      commitAndEndSession,
      endWithError,
      recognitionConstructor,
      releaseRecognition,
    ],
  );

  useEffect(() => {
    isMountedRef.current = true;
    return () => {
      isMountedRef.current = false;
      isSessionActiveRef.current = false;
      explicitStopRef.current = true;
      finalSegmentsRef.current = [];
      const recognition = recognitionRef.current;
      if (recognition) releaseRecognition(recognition, true);
    };
  }, [releaseRecognition]);

  const start = useCallback(() => {
    if (!recognitionConstructor) {
      setStatus('unsupported');
      setErrorMessage('Voice input is not supported by this browser.');
      return;
    }
    if (isSessionActiveRef.current || recognitionRef.current) {
      setStatus('error');
      setErrorMessage('Voice input is already running.');
      return;
    }

    setErrorMessage(undefined);
    setInterimTranscript('');
    finalSegmentsRef.current = [];
    consecutiveRestartsRef.current = 0;
    explicitStopRef.current = false;
    isSessionActiveRef.current = true;
    beginRecognition(false);
  }, [beginRecognition, recognitionConstructor]);

  const finish = useCallback(() => {
    if (!isSessionActiveRef.current || explicitStopRef.current) return;
    explicitStopRef.current = true;
    setStatus('finalizing');
    setInterimTranscript('');
    const recognition = recognitionRef.current;
    if (!recognition) {
      commitAndEndSession();
      return;
    }
    try {
      recognition.stop();
    } catch {
      releaseRecognition(recognition, true);
      endWithError('Voice input could not be finished. Please try again.');
    }
  }, [commitAndEndSession, endWithError, releaseRecognition]);

  return {
    status,
    interimTranscript,
    errorMessage,
    start,
    finish,
  };
}

function getSpeechRecognitionConstructor() {
  if (typeof window === 'undefined') return undefined;
  const speechWindow = window as SpeechWindow;
  return speechWindow.SpeechRecognition ?? speechWindow.webkitSpeechRecognition;
}

function isInvalidStateError(error: unknown) {
  return error instanceof DOMException && error.name === 'InvalidStateError';
}

function toSafeSpeechError(error: string) {
  switch (error) {
    case 'not-allowed':
    case 'service-not-allowed':
      return 'Microphone permission was denied. Allow access and try again.';
    case 'audio-capture':
      return 'No microphone was found. Connect a microphone and try again.';
    case 'no-speech':
      return 'No speech was detected. Try again and speak clearly.';
    case 'network':
      return 'Voice recognition could not connect. Check your connection and try again.';
    case 'language-not-supported':
      return 'Voice recognition is unavailable for your browser language.';
    case 'aborted':
      return 'Voice input was stopped. You can try again.';
    default:
      return 'Voice input is temporarily unavailable. Please try again.';
  }
}
