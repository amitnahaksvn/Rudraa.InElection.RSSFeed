import type { CrawlHistoryRun, CrawlPipelineName, CrawlReport } from './crawlTypes';
import { throwIfNotOk } from './httpUtils';

export interface CrawlHistoryQuery {
  pipeline: CrawlPipelineName;
  from: string;
  to: string;
  count?: number;
  skip?: number;
  provider?: string;
}

export async function fetchCrawlReport(pipeline: CrawlPipelineName, from: string, to: string): Promise<CrawlReport> {
  const params = new URLSearchParams({ pipeline, from, to });
  const response = await fetch(`/api/crawl/report?${params}`);
  await throwIfNotOk(response);
  return response.json();
}

export async function fetchCrawlHistory(query: CrawlHistoryQuery): Promise<CrawlHistoryRun[]> {
  const params = new URLSearchParams({
    pipeline: query.pipeline,
    from: query.from,
    to: query.to,
    count: String(query.count ?? 20),
    skip: String(query.skip ?? 0),
  });
  if (query.provider) {
    params.set('provider', query.provider);
  }
  const response = await fetch(`/api/crawl/history?${params}`);
  await throwIfNotOk(response);
  return response.json();
}

export async function fetchCrawlRunById(id: string): Promise<CrawlHistoryRun> {
  const response = await fetch(`/api/crawl/history/${encodeURIComponent(id)}`);
  await throwIfNotOk(response);
  return response.json();
}
