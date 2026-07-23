import { useQuery } from '@tanstack/react-query';
import { fetchFilteredArticles } from '../../api/filteredArticles';

// `page` here is 0-based (matching MUI's TablePagination), converted to the 1-based page the
// backend expects (see WebPlatform.Endpoints.FilteredArticles.GetList).
export function useFilteredArticles(page: number, pageSize: number) {
  return useQuery({
    queryKey: ['filteredArticles', page, pageSize],
    queryFn: () => fetchFilteredArticles(page + 1, pageSize),
    placeholderData: (previous) => previous,
  });
}
