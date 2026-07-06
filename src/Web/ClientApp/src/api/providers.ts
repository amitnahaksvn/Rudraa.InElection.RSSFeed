import type { ApiProviderSummary, ProviderTestResult, RssProviderSummary } from './providerTypes';
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

export async function testRssFeed(country: string, provider: string, feedUrl: string): Promise<ProviderTestResult> {
  const response = await fetch('/api/providers/rss/test', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ country, provider, feedUrl }),
  });
  await throwIfNotOk(response);
  return response.json();
}

export async function testApiEndpoint(country: string, provider: string, endpointName: string): Promise<ProviderTestResult> {
  const response = await fetch('/api/providers/api/test', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ country, provider, endpointName }),
  });
  await throwIfNotOk(response);
  return response.json();
}
