import { useMutation } from '@tanstack/react-query';
import { testRssFeed } from '../../api/providers';

export function useTestRssFeed() {
  return useMutation({
    mutationFn: ({ country, provider, feedUrl }: { country: string; provider: string; feedUrl: string }) =>
      testRssFeed(country, provider, feedUrl),
  });
}
