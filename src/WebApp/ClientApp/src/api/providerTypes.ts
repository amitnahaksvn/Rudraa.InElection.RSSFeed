// Mirrors Application/Providers/Dtos/*.cs - keep these in sync by hand (no shared schema
// generation in this project yet).

export interface RssFeedSummary {
  id: string;
  name: string;
  url: string;
  category: string;
  language: string;
  enabled: boolean;
}

export interface RssProviderSummary {
  country: string;
  name: string;
  enabled: boolean;
  cron: string;
  timeZone: string;
  saveRawResponses: boolean;
  description: string;
  feeds: RssFeedSummary[];
}

export interface ApiEndpointSummary {
  id: string;
  name: string;
  endpoint: string;
  url: string;
  category: string;
  language: string;
  enabled: boolean;
}

export interface ApiProviderSummary {
  country: string;
  name: string;
  enabled: boolean;
  cron: string;
  timeZone: string;
  baseUrl: string;
  authType: string;
  authParamName: string;
  timeoutSeconds: number;
  description: string;
  endpoints: ApiEndpointSummary[];
}

export interface ProviderTestResult {
  success: boolean;
  httpStatusCode: number | null;
  articleCount: number;
  processingDurationMs: number;
  fetchedAt: string;
  error: string | null;
  exceptionType: string | null;
  rawResponseBody: string | null;
}

export type CrawlPipelineName = 'Rss' | 'Api';

export interface ProviderSchedule {
  pipeline: CrawlPipelineName;
  provider: string;
  country: string;
  enabled: boolean;
  cron: string;
  timeZone: string;
}

export interface Country {
  id: string;
  pipeline: CrawlPipelineName;
  name: string;
  enabled: boolean;
}

export interface CrawlFeed {
  id: string;
  provider: string;
  country: string;
  name: string;
  url: string;
  category: string;
  language: string;
  enabled: boolean;
  defaultImageUrl: string | null;
  queryParameters: Record<string, string> | null;
}
