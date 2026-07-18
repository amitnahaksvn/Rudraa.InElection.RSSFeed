import { createContext, useCallback, useContext, useRef, useState, type ReactNode } from 'react';

export const TRANSITION_DELAY_MS = 10_000;

export interface PendingTransition {
  key: number;
  message: string;
}

interface PendingTransitionContextValue {
  transitions: PendingTransition[];
  startTransition: (message: string, onComplete: () => void) => void;
  leavingIds: Set<string>;
  markLeaving: (id: string) => void;
  unmarkLeaving: (id: string) => void;
}

const PendingTransitionContext = createContext<PendingTransitionContextValue | null>(null);

// Backs two related things: the "Marked as resolved - will move to the Resolved tab in 10
// seconds" toast (transitions), and which row ids are mid-exit-animation (leavingIds) so ErrorListRow
// can play a Collapse-out effect instead of a row just vanishing when the cache filters it out of
// a status tab it no longer matches - see errorLogCacheSync.removeFromMismatchedLists.
export function PendingTransitionProvider({ children }: { children: ReactNode }) {
  const [transitions, setTransitions] = useState<PendingTransition[]>([]);
  const [leavingIds, setLeavingIds] = useState<Set<string>>(new Set());
  const nextKey = useRef(0);

  const startTransition = useCallback((message: string, onComplete: () => void) => {
    const key = nextKey.current++;
    setTransitions((prev) => [...prev, { key, message }]);
    setTimeout(() => {
      setTransitions((prev) => prev.filter((t) => t.key !== key));
      onComplete();
    }, TRANSITION_DELAY_MS);
  }, []);

  const markLeaving = useCallback((id: string) => {
    setLeavingIds((prev) => (prev.has(id) ? prev : new Set(prev).add(id)));
  }, []);

  const unmarkLeaving = useCallback((id: string) => {
    setLeavingIds((prev) => {
      if (!prev.has(id)) return prev;
      const next = new Set(prev);
      next.delete(id);
      return next;
    });
  }, []);

  return (
    <PendingTransitionContext.Provider value={{ transitions, startTransition, leavingIds, markLeaving, unmarkLeaving }}>
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
