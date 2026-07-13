import { useQuery } from '@tanstack/react-query';
import { fetchNewsFeedCount } from '../../api/news';
import type { ArticleSourceType } from '../../api/newsTypes';

export function useNewsFeedCount(sourceType: ArticleSourceType, country: string | null) {
  return useQuery({
    queryKey: ['newsFeedCount', sourceType, country],
    queryFn: () => fetchNewsFeedCount(sourceType, country),
  });
}
