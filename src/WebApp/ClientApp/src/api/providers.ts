import type {
  ApiProviderSummary,
  Country,
  CrawlFeed,
  CrawlPipelineName,
  ProviderSchedule,
  ProviderTestResult,
  RssProviderSummary,
} from './providerTypes';
import { throwIfNotOk } from './httpUtils';

export async function fetchRssProviders(): Promise<RssProviderSummary[]> {
  const response = await fetch('/api/providers/rss');
  await throwIfNotOk(response);
  return response.json();
}

export async function fetchApiProviders(): Promise<ApiProviderSummary[]> {
  const response = await fetch('/api/providers/api');
  await throwIfNotOk(response);
  return response.json();
}

export async function testRssFeed(feedId: string): Promise<ProviderTestResult> {
  const response = await fetch('/api/providers/rss/test', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ feedId }),
  });
  await throwIfNotOk(response);
  return response.json();
}

export async function testApiEndpoint(endpointId: string): Promise<ProviderTestResult> {
  const response = await fetch('/api/providers/api/test', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ endpointId }),
  });
  await throwIfNotOk(response);
  return response.json();
}

// Every field here is a full overwrite (UpdateProviderScheduleCommand upserts the whole catalog
// record) - callers that only mean to change one field (e.g. the Enabled toggle) must still pass
// every other field's current value, or they'll silently reset to that field's default.
export interface UpdateProviderScheduleFields {
  pipeline: CrawlPipelineName;
  provider: string;
  country: string;
  enabled: boolean;
  cron: string;
  timeZone: string;
  saveRawResponses?: boolean;
  baseUrl?: string | null;
  authType?: string | null;
  authParamName?: string | null;
  timeoutSeconds?: number | null;
}

export async function updateProviderSchedule(fields: UpdateProviderScheduleFields): Promise<ProviderSchedule> {
  const response = await fetch('/api/providers/schedule', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(fields),
  });
  await throwIfNotOk(response);
  return response.json();
}

export async function deleteProvider(pipeline: CrawlPipelineName, provider: string, country: string): Promise<void> {
  const response = await fetch(
    `/api/providers/schedule/${pipeline}/${encodeURIComponent(provider)}/${encodeURIComponent(country)}`,
    { method: 'DELETE' },
  );
  await throwIfNotOk(response);
}

export async function fetchCountries(pipeline: CrawlPipelineName): Promise<Country[]> {
  const response = await fetch(`/api/providers/countries?pipeline=${pipeline}`);
  await throwIfNotOk(response);
  return response.json();
}

export async function upsertCountry(pipeline: CrawlPipelineName, name: string, enabled: boolean): Promise<Country> {
  const response = await fetch('/api/providers/countries', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ pipeline, name, enabled }),
  });
  await throwIfNotOk(response);
  return response.json();
}

export async function deleteCountry(pipeline: CrawlPipelineName, name: string): Promise<void> {
  const response = await fetch(`/api/providers/countries/${pipeline}/${encodeURIComponent(name)}`, { method: 'DELETE' });
  await throwIfNotOk(response);
}

export interface CreateFeedFields {
  pipeline: CrawlPipelineName;
  provider: string;
  country: string;
  name: string;
  url: string;
  category: string;
  language: string;
  enabled: boolean;
  defaultImageUrl?: string | null;
  queryParameters?: Record<string, string> | null;
}

export async function createFeed(fields: CreateFeedFields): Promise<CrawlFeed> {
  const response = await fetch('/api/providers/feeds', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(fields),
  });
  await throwIfNotOk(response);
  return response.json();
}

export interface UpdateFeedFields {
  name: string;
  url: string;
  category: string;
  language: string;
  enabled: boolean;
  defaultImageUrl?: string | null;
  queryParameters?: Record<string, string> | null;
}

export async function updateFeed(id: string, fields: UpdateFeedFields): Promise<void> {
  const response = await fetch(`/api/providers/feeds/${encodeURIComponent(id)}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(fields),
  });
  await throwIfNotOk(response);
}

export async function deleteFeed(id: string): Promise<void> {
  const response = await fetch(`/api/providers/feeds/${encodeURIComponent(id)}`, { method: 'DELETE' });
  await throwIfNotOk(response);
}
