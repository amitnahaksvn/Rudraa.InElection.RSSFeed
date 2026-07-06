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
  isResolved: boolean;
  createdOn: string;
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
  provider: string;
  country: string;
  source: string;
  search: string;
}
