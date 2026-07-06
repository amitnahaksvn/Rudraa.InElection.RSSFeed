import { createContext, useCallback, useContext, useRef, useState, type ReactNode } from 'react';

export const TRANSITION_DELAY_MS = 10_000;

export interface PendingTransition {
  key: number;
  message: string;
}

interface PendingTransitionContextValue {
  transitions: PendingTransition[];
  startTransition: (message: string, onComplete: () => void) => void;
}

const PendingTransitionContext = createContext<PendingTransitionContextValue | null>(null);

// Backs the "Marked as resolved - will move to the Resolved tab in 10 seconds" toast: the row's
// status is patched into the cache immediately (see useSetErrorResolved), but the query
// invalidation that actually removes it from the current status-filtered tab is deferred until
// this timer completes, so the user gets a grace period rather than an instant jump.
export function PendingTransitionProvider({ children }: { children: ReactNode }) {
  const [transitions, setTransitions] = useState<PendingTransition[]>([]);
  const nextKey = useRef(0);

  const startTransition = useCallback((message: string, onComplete: () => void) => {
    const key = nextKey.current++;
    setTransitions((prev) => [...prev, { key, message }]);
    setTimeout(() => {
      setTransitions((prev) => prev.filter((t) => t.key !== key));
      onComplete();
    }, TRANSITION_DELAY_MS);
  }, []);

  return (
    <PendingTransitionContext.Provider value={{ transitions, startTransition }}>
      {children}
    </PendingTransitionContext.Provider>
  );
}

export function usePendingTransitions() {
  const ctx = useContext(PendingTransitionContext);
  if (!ctx) {
    throw new Error('usePendingTransitions must be used within a PendingTransitionProvider');
  }
  return ctx;
}
