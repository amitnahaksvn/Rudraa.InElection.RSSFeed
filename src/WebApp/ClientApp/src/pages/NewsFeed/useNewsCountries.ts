import { useQuery } from '@tanstack/react-query';
import { fetchNewsCountries } from '../../api/news';
import type { ArticleSourceType } from '../../api/newsTypes';

export function useNewsCountries(sourceType: ArticleSourceType) {
  return useQuery({ queryKey: ['newsCountries', sourceType], queryFn: () => fetchNewsCountries(sourceType) });
}
