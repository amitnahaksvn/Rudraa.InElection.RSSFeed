import type { ErrorLogDetail, ErrorLogFilters, PagedResult, ErrorLogSummary } from './types';

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

function buildQuery(page: number, filters: ErrorLogFilters): string {
  const params = new URLSearchParams();
  params.set('page', String(page));
  params.set('pageSize', String(PAGE_SIZE));

  if (filters.status === 'unresolved') params.set('isResolved', 'false');
  if (filters.status === 'resolved') params.set('isResolved', 'true');
  if (filters.provider.trim()) params.set('provider', filters.provider.trim());
  if (filters.country.trim()) params.set('country', filters.country.trim());
  if (filters.source.trim()) params.set('source', filters.source.trim());
  if (filters.search.trim()) params.set('search', filters.search.trim());

  return params.toString();
}

export async function fetchErrorLogs(page: number, filters: ErrorLogFilters): Promise<PagedResult<ErrorLogSummary>> {
  const response = await fetch(`/api/errors?${buildQuery(page, filters)}`);
  await throwIfNotOk(response);
  return response.json();
}

export async function fetchErrorLogDetail(id: string): Promise<ErrorLogDetail> {
  const response = await fetch(`/api/errors/${encodeURIComponent(id)}`);
  await throwIfNotOk(response);
  return response.json();
}

export async function setErrorResolved(id: string, resolved: boolean, comment: string): Promise<void> {
  const response = await fetch(`/api/errors/${encodeURIComponent(id)}/resolved`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ resolved, comment }),
  });
  await throwIfNotOk(response);
}

export async function addErrorLogComment(id: string, comment: string): Promise<void> {
  const response = await fetch(`/api/errors/${encodeURIComponent(id)}/comments`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ comment }),
  });
  await throwIfNotOk(response);
}
