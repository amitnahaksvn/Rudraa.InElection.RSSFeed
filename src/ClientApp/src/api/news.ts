import type { ArticleSourceType, NewsArticle, NewsFeedSortBy, NewsFeedSortDirection } from './newsTypes';
import { throwIfNotOk } from './httpUtils';

export interface NewsFeedQuery {
  sourceType: ArticleSourceType;
  country?: string;
  skip: number;
  count: number;
  sortBy: NewsFeedSortBy;
  sortDirection: NewsFeedSortDirection;
}

export async function fetchNewsFeed(query: NewsFeedQuery): Promise<NewsArticle[]> {
  const params = new URLSearchParams({
    sourceType: query.sourceType,
    skip: String(query.skip),
    count: String(query.count),
    sortBy: query.sortBy,
    sortDirection: query.sortDirection,
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

/** Soft-deletes one or more articles by id - one call for both a single-card delete and a multi-select bulk delete. Returns how many were actually found and deleted. */
export async function deleteArticles(ids: string[]): Promise<number> {
  const response = await fetch('/api/news/articles', {
    method: 'DELETE',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ ids }),
  });
  await throwIfNotOk(response);
  return response.json();
}
