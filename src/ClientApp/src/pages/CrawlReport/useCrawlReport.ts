import { useQuery } from '@tanstack/react-query';
import { fetchCrawlReport } from '../../api/crawl';
import type { CrawlPipelineName } from '../../api/crawlTypes';

export function useCrawlReport(pipeline: CrawlPipelineName, from: string, to: string) {
  return useQuery({
    queryKey: ['crawlReport', pipeline, from, to],
    queryFn: () => fetchCrawlReport(pipeline, from, to),
    // Refetching keeps the previous render on screen while it loads (see the dataviz skill's
    // "refetch keeps the frame" rule) rather than flashing a skeleton on every date-range change -
    // but only within the *same* pipeline. TanStack Query's placeholderData otherwise happily
    // reuses the last successful query's data across a queryKey change of any kind, including a
    // pipeline switch - which would briefly show RSS data mislabeled under the "APIs" tab (or vice
    // versa) while the real fetch for the new pipeline is still in flight.
    placeholderData: (previous) => (previous?.pipeline === pipeline ? previous : undefined),
  });
}
