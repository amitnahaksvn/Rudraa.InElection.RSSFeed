import { useQuery } from '@tanstack/react-query';
import { fetchErrorLogCounts } from '../../api/errorLogs';
import type { ErrorLogFilters } from '../../api/types';

export function useErrorLogCounts(filters: ErrorLogFilters) {
  return useQuery({
    queryKey: ['errorLogCounts', filters.provider, filters.country, filters.source, filters.search],
    queryFn: () => fetchErrorLogCounts(filters),
  });
}
