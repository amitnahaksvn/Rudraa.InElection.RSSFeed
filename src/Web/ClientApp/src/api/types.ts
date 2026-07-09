// Mirrors Application/ErrorLogs/Dtos/*.cs - keep these two in sync by hand (no shared schema
// generation in this project yet).

export interface ErrorLogSummary {
  id: string;
  createdOn: string;
  exceptionType: string;
  message: string;
  source: string;
  errorCode: string | null;
  provider: string | null;
  feedOrApiName: string | null;
  country: string | null;
  httpStatusCode: number | null;
  environment: string;
  applicationName: string;
  isResolved: boolean;
  resolvedOn: string | null;
}

export interface ErrorLogHistoryEntry {
  comment: string;
  description: string | null;
  isResolved: boolean;
  createdOn: string;
}

export interface ErrorLogCounts {
  all: number;
  unresolved: number;
  resolved: number;
  rss: number;
  api: number;
  social: number;
  http: number;
  critical: number;
  warning: number;
}

// Mirrors Application.Abstractions.ErrorLogCategory - the error-monitor sidebar's quick-filter
// shortcuts below the All/Unresolved/Resolved status group.
export type ErrorLogCategory = 'Rss' | 'Api' | 'Social' | 'Http' | 'Critical' | 'Warning';

// Mirrors Application.ErrorLogs.Dtos.ErrorLogProviderGroupDto/ErrorLogCategoryBreakdownDto - the
// feed/provider-wise breakdown shown nested under each pipeline (Rss/Api/Social/Http) in the
// sidebar, e.g. Rss -> [{ provider: 'AajTak', count: 6 }, { provider: 'ABPNews', count: 2 }].
export interface ErrorLogProviderGroup {
  provider: string;
  count: number;
  unresolvedCount: number;
}

export interface ErrorLogCategoryBreakdown {
  category: ErrorLogCategory;
  totalCount: number;
  unresolvedCount: number;
  providers: ErrorLogProviderGroup[];
}

export interface ErrorLogDetail extends ErrorLogSummary {
  stackTrace: string | null;
  innerException: string | null;
  requestPath: string | null;
  httpMethod: string | null;
  queryString: string | null;
  requestBody: string | null;
  ipAddress: string | null;
  userAgent: string | null;
  serviceName: string | null;
  machineName: string | null;
  assemblyVersion: string | null;
  traceId: string | null;
  correlationId: string | null;
  hangfireJobId: string | null;
  sourceUrl: string | null;
  responseBody: string | null;
  executionDuration: string | null;
  additionalData: string | null;
  isSent: boolean;
  sentOn: string | null;
  history: ErrorLogHistoryEntry[];
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  hasMore: boolean;
}

export type ResolvedFilter = 'unresolved' | 'resolved' | 'all';

export interface ErrorLogFilters {
  status: ResolvedFilter;
  // Set when a sidebar category (Rss/Api/Social/.../Critical/Warning) is selected instead of a
  // status - mutually exclusive with `status` in the UI (picking one resets the other to its
  // neutral default), but kept as two independent fields since the backend filter dimensions
  // (IsResolved vs Category) are independent too.
  category: ErrorLogCategory | null;
  provider: string;
  country: string;
  source: string;
  search: string;
}
