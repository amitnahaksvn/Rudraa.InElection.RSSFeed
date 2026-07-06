import { useQuery } from '@tanstack/react-query';
import { fetchRssProviders } from '../../api/providers';

export function useRssProviders() {
  return useQuery({ queryKey: ['rssProviders'], queryFn: fetchRssProviders });
}
