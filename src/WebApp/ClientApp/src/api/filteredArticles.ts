import type { PagedResult } from './types';
import type { FilteredArticle } from './filteredArticlesTypes';
import { throwIfNotOk } from './httpUtils';

export async function fetchFilteredArticles(page: number, pageSize: number): Promise<PagedResult<FilteredArticle>> {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  const response = await fetch(`/api/filtered-articles?${params.toString()}`);
  await throwIfNotOk(response);
  return response.json();
}

export async function deleteFilteredArticle(id: string): Promise<void> {
  const response = await fetch(`/api/filtered-articles/${encodeURIComponent(id)}`, { method: 'DELETE' });
  await throwIfNotOk(response);
}
