import type { ArticleSourceType, NewsArticle } from './newsTypes';
import { throwIfNotOk } from './httpUtils';

export interface NewsFeedQuery {
  sourceType: ArticleSourceType;
  country?: string;
  skip: number;
  count: number;
}

export async function fetchNewsFeed(query: NewsFeedQuery): Promise<NewsArticle[]> {
  const params = new URLSearchParams({
    sourceType: query.sourceType,
    skip: String(query.skip),
    count: String(query.count),
  });
  if (query.country) {
    params.set('country', query.country);
  }
  const response = await fetch(`/api/news/feed?${params}`);
  await throwIfNotOk(response);
  return response.json();
}

export async function fetchNewsCountries(sourceType: ArticleSourceType): Promise<string[]> {
  const params = new URLSearchParams({ sourceType });
  const response = await fetch(`/api/news/countries?${params}`);
  await throwIfNotOk(response);
  return response.json();
}

export async function fetchNewsFeedCount(sourceType: ArticleSourceType, country: string | null): Promise<number> {
  const params = new URLSearchParams({ sourceType });
  if (country) {
    params.set('country', country);
  }
  const response = await fetch(`/api/news/feed/count?${params}`);
  await throwIfNotOk(response);
  return response.json();
}
