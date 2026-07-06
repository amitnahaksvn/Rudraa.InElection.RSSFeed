// Mirrors Application/Providers/Dtos/*.cs - keep these in sync by hand (no shared schema
// generation in this project yet).

export interface RssFeedSummary {
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
  description: string;
  feeds: RssFeedSummary[];
}

export interface ApiEndpointSummary {
  name: string;
  endpoint: string;
  category: string;
  language: string;
  enabled: boolean;
}

export interface ApiProviderSummary {
  country: string;
  name: string;
  enabled: boolean;
  cron: string;
  baseUrl: string;
  authType: string;
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
}
