import { useEffect, useRef } from 'react';

/** Fires `onIntersect` when a sentinel div (placed after the last loaded item) scrolls near the viewport - the "get more when scrolling near the bottom" mechanism, without a scroll-position listener. */
export function useInfiniteScrollSentinel(onIntersect: () => void, enabled: boolean) {
  const sentinelRef = useRef<HTMLDivElement | null>(null);
  const onIntersectRef = useRef(onIntersect);
  onIntersectRef.current = onIntersect;

  useEffect(() => {
    const sentinel = sentinelRef.current;
    if (!sentinel || !enabled) {
      return;
    }

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0]?.isIntersecting) {
          onIntersectRef.current();
        }
      },
      { rootMargin: '600px' }, // fetch the next page well before the sentinel is actually visible, so scrolling never visibly pauses
    );
    observer.observe(sentinel);
    return () => observer.disconnect();
  }, [enabled]);

  return sentinelRef;
}
