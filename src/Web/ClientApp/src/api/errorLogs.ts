import type { ErrorLogCounts, ErrorLogDetail, ErrorLogFilters, PagedResult, ErrorLogSummary } from './types';
import { ERROR_LOG_CLIENT_ID } from './errorLogClientId';

const PAGE_SIZE = 20;

// The backend returns RFC7807 ProblemDetails on failure - FluentValidation errors land in an
// "errors" extension (e.g. { Comment: ["A comment is required..."] }), everything else at least
// has a "title". Falling back through both keeps a real message on screen instead of a bare
// "Request failed: 400" whenever something is actually wrong (which is exactly the kind of
// silent-looking failure this was missing before).
async function throwIfNotOk(response: Response): Promise<Response> {
  if (response.ok) {
    return response;
  }

  let message = `Request failed: ${response.status} ${response.statusText}`;
  try {
    const body = await response.clone().json();
    const firstFieldError = body?.errors && Object.values(body.errors).flat()[0];
    message = (firstFieldError as string | undefined) ?? body?.title ?? message;
  } catch {
    // Body wasn't JSON (or was empty) - keep the status-based message above.
  }

  throw new Error(message);
}

function buildNonStatusFilterParams(filters: ErrorLogFilters): URLSearchParams {
  const params = new URLSearchParams();
  if (filters.provider.trim()) params.set('provider', filters.provider.trim());
  if (filters.country.trim()) params.set('country', filters.country.trim());
  if (filters.source.trim()) params.set('source', filters.source.trim());
  if (filters.search.trim()) params.set('search', filters.search.trim());
  return params;
}

function buildQuery(page: number, filters: ErrorLogFilters): string {
  const params = buildNonStatusFilterParams(filters);
  params.set('page', String(page));
  params.set('pageSize', String(PAGE_SIZE));

  // category and status are mutually exclusive in the sidebar - only ever send one.
  if (filters.category) {
    params.set('category', filters.category);
  } else {
    if (filters.status === 'unresolved') params.set('isResolved', 'false');
    if (filters.status === 'resolved') params.set('isResolved', 'true');
  }

  return params.toString();
}

export async function fetchErrorLogs(page: number, filters: ErrorLogFilters): Promise<PagedResult<ErrorLogSummary>> {
  const response = await fetch(`/api/errors?${buildQuery(page, filters)}`);
  await throwIfNotOk(response);
  return response.json();
}

export async function fetchErrorLogCounts(filters: ErrorLogFilters): Promise<ErrorLogCounts> {
  const response = await fetch(`/api/errors/counts?${buildNonStatusFilterParams(filters).toString()}`);
  await throwIfNotOk(response);
  return response.json();
}

export async function fetchErrorLogDetail(id: string): Promise<ErrorLogDetail> {
  const response = await fetch(`/api/errors/${encodeURIComponent(id)}`);
  await throwIfNotOk(response);
  return response.json();
}

export async function setErrorResolved(id: string, resolved: boolean, comment: string, description: string): Promise<void> {
  const response = await fetch(`/api/errors/${encodeURIComponent(id)}/resolved`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json', 'X-ErrorLog-Client-Id': ERROR_LOG_CLIENT_ID },
    body: JSON.stringify({ resolved, comment, description: description || null }),
  });
  await throwIfNotOk(response);
}

export async function addErrorLogComment(id: string, comment: string, description: string): Promise<void> {
  const response = await fetch(`/api/errors/${encodeURIComponent(id)}/comments`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-ErrorLog-Client-Id': ERROR_LOG_CLIENT_ID },
    body: JSON.stringify({ comment, description: description || null }),
  });
  await throwIfNotOk(response);
}
