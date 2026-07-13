import { useInfiniteQuery } from '@tanstack/react-query';
import { fetchNewsFeed } from '../../api/news';
import type { ArticleSourceType, NewsFeedSortBy } from '../../api/newsTypes';

const PAGE_SIZE = 20;

export function useNewsFeed(sourceType: ArticleSourceType, country: string | null, sortBy: NewsFeedSortBy) {
  return useInfiniteQuery({
    // sortBy is part of the key (not just a param) so switching it starts the infinite scroll
    // over from page 1 instead of re-sorting whatever pages happened to already be loaded.
    queryKey: ['newsFeed', sourceType, country, sortBy],
    queryFn: ({ pageParam }) =>
      fetchNewsFeed({ sourceType, country: country ?? undefined, skip: pageParam, count: PAGE_SIZE, sortBy }),
    initialPageParam: 0,
    // A page shorter than requested means there's nothing left to fetch - the same "next button
    // disables once a page comes back short" signal used elsewhere in this app (see the crawl
    // report's RecentRunsTable), just driving infinite scroll instead of a pager.
    getNextPageParam: (lastPage, allPages) => (lastPage.length < PAGE_SIZE ? undefined : allPages.length * PAGE_SIZE),
  });
}
