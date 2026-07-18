import { useMutation, useQueryClient, type InfiniteData } from '@tanstack/react-query';
import { deleteArticles } from '../../api/news';
import type { NewsArticle } from '../../api/newsTypes';

export function useDeleteArticles() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (ids: string[]) => deleteArticles(ids),
    onSuccess: (_deletedCount, ids) => {
      const idSet = new Set(ids);

      // Strip the deleted articles out of every cached News Feed page (whichever tab/country/sort
      // combination happens to be cached) instead of waiting on a refetch, so the cards vanish
      // immediately. The total-count tiles are just invalidated below rather than decremented
      // locally - a cached count entry doesn't carry enough info here to know which of several
      // simultaneously-cached country filters each deleted article actually belonged to.
      queryClient.setQueriesData<InfiniteData<NewsArticle[]>>({ queryKey: ['newsFeed'] }, (data) => {
        if (!data) return data;
        return { ...data, pages: data.pages.map((page) => page.filter((article) => !idSet.has(article.id))) };
      });

      queryClient.invalidateQueries({ queryKey: ['newsFeedCount'] });
    },
  });
}
